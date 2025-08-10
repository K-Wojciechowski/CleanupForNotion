FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY src ./
WORKDIR /src/CleanupForNotion.Web
RUN dotnet restore
RUN dotnet publish -c Release -o out -p:PublishReadyToRun=true

FROM mcr.microsoft.com/dotnet/aspnet:9.0
WORKDIR /app
COPY --from=build /src/CleanupForNotion.Web/out .
ENTRYPOINT ["dotnet", "CleanupForNotion.Web.dll"]
