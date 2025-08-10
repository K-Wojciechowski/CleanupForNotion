resource "aws_lambda_function" "lambda" {
  filename         = var.lambda_source
  source_code_hash = filemd5(var.lambda_source)
  function_name    = var.lambda_name
  role             = aws_iam_role.lambda_role.arn
  handler          = "CleanupForNotion.Aws::CleanupForNotion.Aws.LambdaHandler::Handle"
  runtime          = "dotnet8"
  timeout          = 300

  logging_config {
    log_format = "Text"
    log_group  = var.cloudwatch_log_group_name
  }

  environment {
    variables = {
      CFN_S3_BUCKET           = var.s3_name
      CFN_S3_KEY              = var.s3_key
      CFN_DYNAMODB_TABLE_NAME = var.dynamodb_name
    }
  }
}

resource "aws_iam_role" "lambda_role" {
  name = "${var.lambda_name}-role"

  assume_role_policy = jsonencode({
    Version = "2012-10-17"
    Statement = [
      {
        Action = "sts:AssumeRole"
        Effect = "Allow"
        Principal = {
          Service = "lambda.amazonaws.com"
        }
      }
    ]
  })
}

resource "aws_iam_policy" "lambda_policies" {
  name        = "${var.lambda_name}-policies"
  path        = "/"
  description = "IAM policies granting required permissions for Lambda"

  policy = jsonencode({
    Version = "2012-10-17"
    Statement = [
      {
        Effect = "Allow"
        Action = [
          "s3:GetObject"
        ]
        Resource = ["${var.s3_arn}/${var.s3_key}"]
      },
      {
        Effect = "Allow"
        Action = [
          "dynamodb:BatchWriteItem",
          "dynamodb:GetItem"
        ]
        Resource = ["${var.dynamodb_arn}", "${var.dynamodb_arn}/*"]
      },
      {
        Effect = "Allow"
        Action = [
          "logs:CreateLogGroup",
          "logs:CreateLogStream",
          "logs:PutLogEvents"
        ]
        Resource = ["arn:aws:logs:*:*:*"]
      }
    ]
  })
}

resource "aws_iam_role_policy_attachment" "lambda_policies_attachment" {
  role       = aws_iam_role.lambda_role.name
  policy_arn = aws_iam_policy.lambda_policies.arn
}
