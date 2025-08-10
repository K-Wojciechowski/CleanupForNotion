resource "aws_dynamodb_table" "dynamodb" {
  name         = "${var.id_prefix}-dynamodb"
  billing_mode = "PAY_PER_REQUEST"
  hash_key     = "Plugin"
  range_key    = "ConfigKey"

  attribute {
    name = "Plugin"
    type = "S"
  }

  attribute {
    name = "ConfigKey"
    type = "S"
  }
}
