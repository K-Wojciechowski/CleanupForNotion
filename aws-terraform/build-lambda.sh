#!/bin/bash
scriptRoot=$(dirname "$0")
dotnet publish -c Release -r linux-x64 -p:PublishReadyToRun=true -o $scriptRoot/publish $scriptRoot/../src/CleanupForNotion.Aws/CleanupForNotion.Aws.csproj

if [[ $? -ne 0 ]]; then
  echo "dotnet publish failed with exit code $?"
  rm -rf $scriptRoot/publish 2> /dev/null
  exit $?
fi

(cd "$scriptRoot/publish"; zip -r ../lambda.zip *)
rm -rf $scriptRoot/publish
