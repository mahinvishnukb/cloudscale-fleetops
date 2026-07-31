output "manifest_bucket" {
  description = "Bucket manifests are uploaded to."
  value       = aws_s3_bucket.manifests.id
}

output "manifest_upload_prefix" {
  description = "Key convention the Lambda trigger expects."
  value       = "incoming/{IMO}/{VOYAGE}.csv"
}

output "lambda_function_name" {
  value = aws_lambda_function.manifest_processor.function_name
}

output "lambda_dlq_url" {
  value = aws_sqs_queue.manifest_dlq.url
}

output "log_group" {
  value = aws_cloudwatch_log_group.manifest_processor.name
}

output "smoke_test_command" {
  description = "Drop a manifest into the bucket and watch the Lambda pick it up."
  value = join(" ", [
    "awslocal s3 cp ./sample-manifest.csv",
    "s3://${aws_s3_bucket.manifests.id}/incoming/9074729/V-2026-014.csv"
  ])
}
