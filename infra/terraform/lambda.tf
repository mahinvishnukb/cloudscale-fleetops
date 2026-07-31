data "aws_iam_policy_document" "lambda_assume_role" {
  statement {
    effect  = "Allow"
    actions = ["sts:AssumeRole"]

    principals {
      type        = "Service"
      identifiers = ["lambda.amazonaws.com"]
    }
  }
}

resource "aws_iam_role" "manifest_processor" {
  name               = "${local.name_prefix}-manifest-processor"
  assume_role_policy = data.aws_iam_policy_document.lambda_assume_role.json
}

# Least privilege: read only from incoming/, write only to processed/ and rejected/.
data "aws_iam_policy_document" "manifest_processor" {
  statement {
    sid       = "ReadIncomingManifests"
    effect    = "Allow"
    actions   = ["s3:GetObject"]
    resources = ["${aws_s3_bucket.manifests.arn}/incoming/*"]
  }

  statement {
    sid     = "WriteProcessedManifests"
    effect  = "Allow"
    actions = ["s3:PutObject"]
    resources = [
      "${aws_s3_bucket.manifests.arn}/processed/*",
      "${aws_s3_bucket.manifests.arn}/rejected/*",
    ]
  }

  statement {
    sid       = "ListBucket"
    effect    = "Allow"
    actions   = ["s3:ListBucket"]
    resources = [aws_s3_bucket.manifests.arn]
  }

  statement {
    sid    = "WriteLogs"
    effect = "Allow"
    actions = [
      "logs:CreateLogStream",
      "logs:PutLogEvents",
    ]
    resources = ["${aws_cloudwatch_log_group.manifest_processor.arn}:*"]
  }

  statement {
    sid       = "DeadLetterQueue"
    effect    = "Allow"
    actions   = ["sqs:SendMessage"]
    resources = [aws_sqs_queue.manifest_dlq.arn]
  }
}

resource "aws_iam_role_policy" "manifest_processor" {
  name   = "${local.name_prefix}-manifest-processor"
  role   = aws_iam_role.manifest_processor.id
  policy = data.aws_iam_policy_document.manifest_processor.json
}

# Created explicitly rather than letting Lambda auto-create it, so retention
# is set and logs do not accumulate forever.
resource "aws_cloudwatch_log_group" "manifest_processor" {
  name              = "/aws/lambda/${local.name_prefix}-manifest-processor"
  retention_in_days = 14
}

# Failed invocations land here instead of vanishing.
resource "aws_sqs_queue" "manifest_dlq" {
  name                      = "${local.name_prefix}-manifest-dlq"
  message_retention_seconds = 1209600 # 14 days
}

resource "aws_lambda_function" "manifest_processor" {
  function_name = "${local.name_prefix}-manifest-processor"
  role          = aws_iam_role.manifest_processor.arn

  # .NET 8 runs on Lambda via the custom runtime; the handler is the assembly name.
  runtime       = "provided.al2023"
  handler       = "FleetOps.ManifestProcessor"
  architectures = [var.lambda_architecture]

  filename         = var.lambda_zip_path
  source_code_hash = filebase64sha256(var.lambda_zip_path)

  memory_size = var.lambda_memory_mb
  timeout     = 120

  environment {
    variables = {
      AWS__ManifestBucket           = aws_s3_bucket.manifests.id
      AWS__Region                   = var.aws_region
      AWS__ServiceUrl               = var.use_localstack ? var.localstack_endpoint : ""
      ConnectionStrings__FleetOpsDb = var.database_connection_string
      # The Lambda never mints tokens, but the shared options validator requires a value.
      Jwt__Key = random_password.lambda_jwt_placeholder.result
    }
  }

  dead_letter_config {
    target_arn = aws_sqs_queue.manifest_dlq.arn
  }

  depends_on = [
    aws_iam_role_policy.manifest_processor,
    aws_cloudwatch_log_group.manifest_processor,
  ]
}

resource "random_password" "lambda_jwt_placeholder" {
  length  = 48
  special = false
}

resource "aws_lambda_permission" "allow_s3" {
  statement_id  = "AllowExecutionFromS3"
  action        = "lambda:InvokeFunction"
  function_name = aws_lambda_function.manifest_processor.function_name
  principal     = "s3.amazonaws.com"
  source_arn    = aws_s3_bucket.manifests.arn
}

# Alarm on repeated failures. In LocalStack this is a no-op; in real AWS it is the
# difference between noticing a broken pipeline in minutes versus in days.
resource "aws_cloudwatch_metric_alarm" "manifest_processor_errors" {
  count = var.use_localstack ? 0 : 1

  alarm_name          = "${local.name_prefix}-manifest-processor-errors"
  comparison_operator = "GreaterThanThreshold"
  evaluation_periods  = 1
  metric_name         = "Errors"
  namespace           = "AWS/Lambda"
  period              = 300
  statistic           = "Sum"
  threshold           = 3
  treat_missing_data  = "notBreaching"

  dimensions = {
    FunctionName = aws_lambda_function.manifest_processor.function_name
  }
}
