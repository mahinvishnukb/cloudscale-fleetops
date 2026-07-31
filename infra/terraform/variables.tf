variable "aws_region" {
  description = "Region for all resources."
  type        = string
  default     = "ca-central-1"
}

variable "environment" {
  description = "Environment name; suffixes every resource so environments never collide."
  type        = string
  default     = "dev"

  validation {
    condition     = contains(["dev", "staging", "prod"], var.environment)
    error_message = "environment must be one of: dev, staging, prod."
  }
}

variable "use_localstack" {
  description = "Target LocalStack instead of real AWS. Keep true unless you intend to spend money."
  type        = bool
  default     = true
}

variable "localstack_endpoint" {
  description = "LocalStack edge endpoint."
  type        = string
  default     = "http://localhost:4566"
}

variable "project_name" {
  description = "Resource name prefix."
  type        = string
  default     = "fleetops"
}

variable "lambda_zip_path" {
  description = "Path to the manifest-processor archive built by scripts/build-lambda.sh."
  type        = string
  default     = "../../lambda/dist/manifest-processor.zip"
}

variable "lambda_architecture" {
  description = "Lambda CPU architecture. arm64 (Graviton) is cheaper than x86_64."
  type        = string
  default     = "arm64"
}

variable "lambda_memory_mb" {
  description = "Memory for the manifest processor. CPU scales with memory on Lambda."
  type        = number
  default     = 512
}

variable "manifest_retention_days" {
  description = "Days before processed manifests transition out of standard storage."
  type        = number
  default     = 30
}

variable "database_connection_string" {
  description = <<-EOT
    Postgres connection string handed to the Lambda.
    Never hardcode this. Supply it at apply time:
      TF_VAR_database_connection_string=... terraform apply
    In real AWS this should come from Secrets Manager instead.
  EOT
  type        = string
  sensitive   = true
  default     = ""
}
