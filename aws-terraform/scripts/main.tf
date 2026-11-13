resource "local_file" "bash" {
  content         = "#!/bin/bash\naws lambda invoke --region ${var.aws_region} --function-name ${var.lambda_name} --payload fileb://invoke.json /dev/null\n"
  filename        = "${path.root}/invoke.sh"
  file_permission = "0755"
}

resource "local_file" "cmd" {
  content         = "aws lambda invoke --region ${var.aws_region} --function-name ${var.lambda_name} --payload fileb://invoke.json NUL\r\n"
  filename        = "${path.root}/invoke.cmd"
  file_permission = "0755"
}


resource "local_file" "json" {
  content  = "{\"version\": \"cfn-script\"}"
  filename = "${path.root}/invoke.json"
}
