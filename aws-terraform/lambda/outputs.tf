output "arn" {
  description = "ARN of the lambda"
  value       = aws_lambda_function.lambda.arn
}

output "name" {
  description = "Name of the lambda"
  value       = aws_lambda_function.lambda.function_name
}
