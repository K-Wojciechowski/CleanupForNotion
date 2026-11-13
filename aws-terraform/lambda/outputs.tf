output "arn" {
  description = "ARN of the lambda"
  value       = aws_lambda_function.lambda.arn
}

output "name" {
  description = "Name of the lambda"
  value       = aws_lambda_function.lambda.function_name
}

output "function_url" {
  description = "Function URL of the lambda"
  value       = var.function_url ? aws_lambda_function_url.lambda_url[0].function_url : null
}
