resource "local_file" "bash" {
  content         = "#!/bin/bash\naws lambda invoke --region ${var.aws_region} --function-name ${var.lambda_name} /dev/null\n"
  filename        = "${path.root}/invoke.sh"
  file_permission = "0755"
}

resource "local_file" "cmd" {
  content         = "aws lambda invoke --region ${var.aws_region} --function-name ${var.lambda_name} NUL\r\n"
  filename        = "${path.root}/invoke.cmd"
  file_permission = "0755"
}
