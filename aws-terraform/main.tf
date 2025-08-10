provider "aws" {
  region = var.aws_region
}

module "cloudwatch" {
  source = "./cloudwatch"

  lambda_name = local.lambda_name
}

module "dynamodb" {
  source = "./dynamodb"

  id_prefix = var.id_prefix
}

module "lambda" {
  source = "./lambda"

  cloudwatch_log_group_name = module.cloudwatch.log_group_name
  dynamodb_arn              = module.dynamodb.arn
  dynamodb_name             = module.dynamodb.name
  id_prefix                 = var.id_prefix
  lambda_name               = local.lambda_name
  lambda_source             = var.lambda_source
  s3_arn                    = module.s3.arn
  s3_name                   = module.s3.name
  s3_key                    = module.s3.key
}

module "s3" {
  source = "./s3"

  id_prefix          = var.id_prefix
  appsettings_source = var.appsettings_source
}

module "scheduler" {
  source = "./scheduler"

  id_prefix           = var.id_prefix
  lambda_arn          = module.lambda.arn
  lambda_name         = module.lambda.name
  schedule_expression = var.schedule_expression
}

module "scripts" {
  source = "./scripts"

  lambda_name = module.lambda.name
}
