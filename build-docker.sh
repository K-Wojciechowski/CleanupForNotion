#!/bin/bash
set -euo pipefail

commit=$(git rev-parse --short HEAD)
tag_commit="cfn:$commit"
tag_latest="cfn:latest"

docker build -t $tag_commit -t $tag_latest .

echo "Docker image built and tagged as:"
echo "  $tag_commit"
echo "  $tag_latest"
