variable "aws_region" {
  description = "The AWS region to deploy resources in"
  type        = string
  default     = "eu-north-1"
}

variable "id_prefix" {
  description = "The prefix to IDs/names of AWS resources"
  type        = string
  default     = "cfn"
}

variable "appsettings_source" {
  description = "The source path of the appsettings.json file"
  default     = "./appsettings.json"
  type        = string
}

variable "lambda_source" {
  description = "The source path of the lambda.zip file"
  default     = "./lambda.zip"
  type        = string
}

variable "schedule_expression" {
  description = "The schedule expression used to trigger the Lambda function"
  default     = "rate(30 minutes)"
  type        = string
}

variable "tags" {
  description = "A map of tags to assign to AWS resources"
  type        = map(string)
  default = {
    Project = "CleanupForNotion"
  }
}
