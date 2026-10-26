# CleanupForNotion Changelog

## 2.0.1

* Upgrade Lambda to .NET 10

## 2.0.0

* Upgrade to Notion.Net 5.0.0, which removes the deprecated database query API.
  Existing configurations must be manually migrated to use `DataSourceId`s.
  Data source IDs can be retrieved from Notion's UI.

## 1.5.0

* Upgrade to .NET 10

## 1.4.1

* Improve logging configuration

## 1.4.0

* 100% code coverage for Web app, 100% tests for Console app components (fix #4)

## 1.3.0

* AWS deployment model (fix #2)

## 1.2.0

* Set up CI using GitHub Actions to run tests; build and publish Docker image (fix #1)

## 1.1.0

* Add EnsureStaticRelatedPage plugin (fix #3)

## 1.0.0

Initial public release.
