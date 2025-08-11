#!/bin/bash
set -euo pipefail

releaseJson=$(curl -L \
  -H "Accept: application/vnd.github+json" \
  -H "X-GitHub-Api-Version: 2022-11-28" \
  "https://api.github.com/repos/K-Wojciechowski/CleanupForNotion/releases/latest")

tag=$(echo "$releaseJson" | jq -r .tag_name)

echo "Latest release: $tag"

zipUrl=$(echo "$releaseJson" | jq -r '.assets[] | select(.name=="lambda.zip") | .browser_download_url')

if [ -z "$zipUrl" ]; then
    echo "No assets found in the latest release."
    exit 1
fi

targetPath="$(dirname "$0")/lambda.zip"
echo "Downloading $zipUrl to $targetPath"
curl -L -o $targetPath $zipUrl
ls -l $targetPath
