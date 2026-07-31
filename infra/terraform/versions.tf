terraform {
  required_version = ">= 1.6.0"

  required_providers {
    aws = {
      source  = "hashicorp/aws"
      version = "~> 5.40"
    }
    random = {
      source  = "hashicorp/random"
      version = "~> 3.6"
    }
  }

  # Remote state is intentionally NOT configured by default: an S3 backend costs money
  # and this stack is meant to run against LocalStack for free. See backend.tf.example
  # for the production-shaped configuration.
}
