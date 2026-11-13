output "lambda_arn" {
  description = "ARN of the Lambda function"
  value       = module.lambda.arn
}

output "lambda_function_url" {
  description = "Function URL of the Lambda function (for receiving webhooks)"
  value       = var.enable_webhooks ? module.lambda.function_url : null
}

output "s3_url" {
  description = "S3 URL of appsettings.json file"
  value       = "s3://${module.s3.name}/${module.s3.key}"
}
