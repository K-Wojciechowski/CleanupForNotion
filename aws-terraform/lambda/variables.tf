variable "cloudwatch_log_group_name" {
  description = "The name of the CloudWatch log group for the Lambda function"
  type        = string
}

variable "dynamodb_arn" {
  description = "The ARN of the DynamoDB table"
  type        = string
}

variable "dynamodb_name" {
  description = "The name of the DynamoDB table"
  type        = string
}

variable "id_prefix" {
  description = "The prefix to IDs/names of AWS resources"
  type        = string
}

variable "lambda_name" {
  description = "The name of the Lambda function"
  type        = string
}

variable "lambda_source" {
  description = "The source path of the lambda.zip file"
  type        = string
}

variable "s3_arn" {
  description = "The ARN of the S3 bucket"
  type        = string
}

variable "s3_name" {
  description = "The name of the S3 bucket"
  type        = string
}

variable "s3_key" {
  description = "The S3 key for the appsettings.json file"
  type        = string
}
