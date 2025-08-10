resource "aws_s3_bucket" "bucket" {
  bucket_prefix = "${var.id_prefix}-s3-"
}

resource "aws_s3_object" "appsettings" {
  bucket = aws_s3_bucket.bucket.id
  key    = "appsettings.json"
  source = var.appsettings_source
  etag   = filemd5(var.appsettings_source)
}
