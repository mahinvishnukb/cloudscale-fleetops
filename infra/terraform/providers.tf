provider "aws" {
  region = var.aws_region

  # ---- LocalStack mode ----------------------------------------------------
  # When use_localstack = true, everything below points the provider at the local
  # container instead of AWS. No credentials, no account, no spend.
  access_key                  = var.use_localstack ? "test" : null
  secret_key                  = var.use_localstack ? "test" : null
  skip_credentials_validation = var.use_localstack
  skip_metadata_api_check     = var.use_localstack
  skip_requesting_account_id  = var.use_localstack
  s3_use_path_style           = var.use_localstack

  dynamic "endpoints" {
    for_each = var.use_localstack ? [1] : []
    content {
      s3         = var.localstack_endpoint
      lambda     = var.localstack_endpoint
      iam        = var.localstack_endpoint
      sts        = var.localstack_endpoint
      logs       = var.localstack_endpoint
      apigateway = var.localstack_endpoint
      sqs        = var.localstack_endpoint
    }
  }

  default_tags {
    tags = {
      Project     = "CloudScale FleetOps"
      Environment = var.environment
      ManagedBy   = "Terraform"
    }
  }
}
