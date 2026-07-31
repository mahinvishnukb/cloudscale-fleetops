locals {
  name_prefix     = "${var.project_name}-${var.environment}"
  # Must match AWS__ManifestBucket used by the API and docker-compose, otherwise the
  # direct-upload path and the Lambda pipeline end up on two different buckets.
  manifest_bucket = "${var.project_name}-manifests-upload-${var.environment}"
}

resource "aws_s3_bucket" "manifests" {
  bucket        = local.manifest_bucket
  force_destroy = var.environment != "prod"
}

# Block every form of public access. S3 buckets left open are the single most
# common cause of data leaks; this is not optional.
resource "aws_s3_bucket_public_access_block" "manifests" {
  bucket = aws_s3_bucket.manifests.id

  block_public_acls       = true
  block_public_policy     = true
  ignore_public_acls      = true
  restrict_public_buckets = true
}

resource "aws_s3_bucket_versioning" "manifests" {
  bucket = aws_s3_bucket.manifests.id

  versioning_configuration {
    # A reprocessed manifest must never silently overwrite the original filing.
    status = "Enabled"
  }
}

resource "aws_s3_bucket_server_side_encryption_configuration" "manifests" {
  bucket = aws_s3_bucket.manifests.id

  rule {
    apply_server_side_encryption_by_default {
      sse_algorithm = "AES256"
    }
    bucket_key_enabled = true
  }
}

resource "aws_s3_bucket_lifecycle_configuration" "manifests" {
  bucket = aws_s3_bucket.manifests.id

  rule {
    id     = "archive-processed"
    status = "Enabled"

    filter {
      prefix = "processed/"
    }

    transition {
      days          = var.manifest_retention_days
      storage_class = "STANDARD_IA"
    }

    noncurrent_version_expiration {
      noncurrent_days = 90
    }
  }

  rule {
    id     = "abort-incomplete-uploads"
    status = "Enabled"

    filter {}

    abort_incomplete_multipart_upload {
      days_after_initiation = 7
    }
  }
}

# Fire the Lambda whenever a manifest lands under incoming/.
# Scoped by prefix AND suffix so the processed/ and rejected/ copies the function
# itself writes cannot retrigger it — an infinite-loop bill is a classic S3 mistake.
resource "aws_s3_bucket_notification" "manifest_uploaded" {
  bucket = aws_s3_bucket.manifests.id

  lambda_function {
    lambda_function_arn = aws_lambda_function.manifest_processor.arn
    events              = ["s3:ObjectCreated:*"]
    filter_prefix       = "incoming/"
    filter_suffix       = ".csv"
  }

  depends_on = [aws_lambda_permission.allow_s3]
}
