output "log_group_name" {
  description = "Name of the log group"
  value       = aws_cloudwatch_log_group.log_group.name
}
