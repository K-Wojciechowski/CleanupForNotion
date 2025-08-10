variable "id_prefix" {
  description = "The prefix to IDs/names of AWS resources"
  type        = string
}

variable "lambda_arn" {
  description = "The ARN of the Lambda function to execute"
  type        = string
}

variable "lambda_name" {
  description = "The name of the Lambda function to execute"
  type        = string
}

variable "schedule_expression" {
  description = "The schedule expression"
  type        = string
}
