output "arn" {
  description = "ARN of the bucket"
  value       = aws_s3_bucket.bucket.arn
}

output "name" {
  description = "Name (ID) of the bucket"
  value       = aws_s3_bucket.bucket.id
}

output "key" {
  description = "S3 key for the appsettings.json file"
  value       = aws_s3_object.appsettings.key
}
