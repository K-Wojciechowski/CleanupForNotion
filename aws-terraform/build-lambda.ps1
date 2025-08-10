$ErrorActionPreference = "Stop"
try {
  dotnet publish -c Release -r linux-x64 -p:PublishReadyToRun=true -o $PSScriptRoot/publish $PSScriptRoot/../src/CleanupForNotion.Aws/CleanupForNotion.Aws.Csproj
  if ($LASTEXITCODE -ne 0) {
    Write-Error "dotnet publish failed with exit code $LASTEXITCODE"
    Exit $LASTEXITCODE
  }
  Compress-Archive -Force -Path $PSScriptRoot/publish/* -DestinationPath $PSScriptRoot/lambda.zip
} finally {
  Remove-Item -Recurse $PSScriptRoot/publish
}
