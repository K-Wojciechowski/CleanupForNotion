# CleanupForNotion

An app (in a few deployment flavors) to periodically clean up Notion databases.

Fully configurable, with plugins for all the use cases I need.

Can be deployed to AWS (for approximately $0/month), your own server, or possibly even a PC.

Fully tested: 100% code coverage, enforced by the CI pipeline.

[![CleanupForNotion CI](https://github.com/K-Wojciechowski/CleanupForNotion/actions/workflows/ci.yml/badge.svg)](https://github.com/K-Wojciechowski/CleanupForNotion/actions/workflows/ci.yml)

## Table of Contents

* [Plugins](#plugins)
  * [DeleteByCheckbox](#deletebycheckbox)
  * [DeleteOnNewMonthlyCycle](#deleteonnewmonthlycycle)
  * [DeleteWithoutRelationships](#deletewithoutrelationships)
  * [DeleteZeroSum](#deletezerosum)
  * [EnsureStaticRelatedPage](#ensurestaticrelatedpage)
  * [EstimateExpensesWithSubscriptions](#estimateexpenseswithsubscriptions)
* [Setup in Notion](#setup-in-notion)
  * [Create integration](#create-integration)
  * [Add access from integration settings](#add-access-from-integration-settings)
  * [Add access from page settings](#add-access-from-page-settings)
  * [Get data source IDs](#get-data-source-ids)
  * [Get page IDs](#get-page-ids)
* [Configuration](#configuration)
  * [General options](#general-options)
  * [Defining and configuring plugins](#defining-and-configuring-plugins)
  * [State storage](#state-storage)
  * [Webhooks](#webhooks)
  * [Putting it all together](#putting-it-all-together)
* [Deployment](#deployment)
  * [Web application (in Docker)](#web-application-in-docker)
  * [AWS](#aws)
  * [Console](#console)
* [Roadmap](#roadmap)
* [Legal](#legal)

## Plugins

### DeleteByCheckbox

This plugin deletes items from databases which have a checkbox property checked.

While it is quite trivial to right-click and click Delete, this allows keeping the items to be deleted in sight for a while longer, so that the user can know what is about to be deleted and can make decisions based on that.

#### Plugin-specific configuration for DeleteByCheckbox

* **DeleteIfChecked** (boolean, defaults to `true`) — If true, items with the property checked will be deleted. If false, items with the propertry unchecked will be deleted.

### DeleteOnNewMonthlyCycle

This plugin deletes items from databases that belong to previous monthly cycles.

The database I use this with tracks purchases made with my secondary bank’s debit card. I use it to ensure I reach the magic number required to waive the fees at that bank.

I need to reach the threshold every month, and I only need the current month’s data in the database. So this plugin deletes pages after the month ends. (And the cycle doesn’t have to start on the first day of the month.)

#### Plugin-specific configuration for DeleteOnNewMonthlyCycle

* **CycleResetDay** (int, mandatory) — The day of the month on which the cycle resets.
* **MonthOverflowResetsOnFirstDayOfNextMonth** (boolean, defaults to `true`) — Useful only if the CycleResetDay is 29 or greater. If this is true and CycleResetDay is greater than the number of days in the month, the reset will happen on the 1st of the next month. Otherwise, the day will be (NumberOfDaysInMonth - CycleResetDay). For example, if CycleResetDay = 30 and the month of February has 28 days, `true` will reset the cycle on the 1st of March, and `false` will do it on the 2nd.
* **TimeZoneName** (string, defaults to `null` == UTC) — The IANA name of the time zone used to determine the cut-off point (midnight in that time zone).

### DeleteWithoutRelationships

This plugin deletes items which have a relation property with no related pages attached to it.

This plugin has no special configuration options.

### DeleteZeroSum

This plugin implements a simple “debt settlement” mechanism for items. It looks for pairs of items in a database which have numeric or formula properties whose values are opposite numbers (such as 2 and -2) and deletes both items from the pair. It also deletes items with a value of zero. The sum of the property values will not change after this plugin is executed.

Note that the algorithm is a bit simplistic, and it won’t consider (2, 2, -4) to be removable.

This plugin has no special configuration options.

### EnsureStaticRelatedPage

This plugin ensures that all pages in a database have a relation field set to a single, specified page.

This is another part of the “secondary bank’s credit card” database (as described alongside [DeleteOnNewMonthlyCycle](#deleteonnewmonthlycycle)).

I actually have two databases: one with the purchases, and another that shows the remaining amount until the target by using this terrible formula:

```javascript
if(prop("Total")>=TARGET, "✅",
let(x, add(subtract(TARGET, prop("Total")), 0.000001), style("PLN " + replace(format(x), "(\d+\.\d\d)(.+)", "$1"), "b", "red_background")))
```

For this to work, all purchases must have a relationship to the single row in the summary database. I use a Button to do it, but I occassionally forget and insert data directly into the transactions database. This plugin fixes that.

#### Plugin-specific configuration for EnsureStaticRelatedPage

* **RelatedPageId** (string, mandatory) — The Notion ID of the page that all pages in the database must be related to.

### EstimateExpensesWithSubscriptions

This plugin estimates the total spend in a database of expenses, but assuming all subscriptions (or other constant purchases) have been charged to the card.

This plugin assumes that you have two databases. The monthly expenses database has at least two columns, one for the vendor name, and another for the amount. The second database contains your subscriptions, with four fields: Vendor, Amount, Expected Total (filled in by the plugin), Alternate Names (optional, semicolon-separated). There must also be one row with a null or zero Amount - this is where expenses not associated with a subscription are placed.

For each subscription, expenses with matching vendors are totalled. If the subscription total is less than the expected monthly amount, the expected amount is considered to be the total. Those augmented totals are then added, along with the total of expenses which don’t match any subscription. In short:

```math
EstimatedTotal = \max_{Subscription} \left(Subscription.ExpectedAmount, \left(\sum_{Expense \in Subscription} Expense.Amount\right) \right)
```

The sum of the estimated totals is the predicted spend for the month.

This is another part of the “secondary bank’s credit card” database (as described alongside [DeleteOnNewMonthlyCycle](#deleteonnewmonthlycycle)).

#### Plugin-specific configuration for EstimateExpensesWithoutSubscriptions

* **DataSourceId** (string, mandatory) — ID of the data source with subscriptions.
* **PropertyName** (string, mandatory) — The name of the subscription property to which the estimated total will be written.
* **ExpensesDataSourceId** (string, mandatory) — ID of the data source with expenses.
* **VendorPropertyName** (string, mandatory) — name of the vendor property in both data sources.
* **AmountPropertyName** (string, mandatory) — name of the amount property in both data sources.
* **AlternateVendorPropertyName** (string, optional) — name of the alternate vendor property in the subscriptions data source.
* **ActualTotalPropertyName** (string, optional) — The name of the subscription property to which the actual total will be written.
* **CurrencyDecimalPlaces** (integer, defaults to 2) — the number of decimal places used by the currency.

*Note: the **GracePeriod** property is ignored by this plugin.*

## Setup in Notion

### Create integration

An **integration** must be created in Notion. Open the [Integrations](https://www.notion.so/profile/integrations) page and create an internal integration associated with the workspace.

In the integration settings, locate the **Internal Integration Secret**. Click the **Show** link on the right side of the text box, then choose **Copy**.

Access must then be granted to the pages that need to be edited.

### Add access from integration settings

Navigate to the integration settings on the [Integrations](https://www.notion.so/profile/integrations) page. Open the **Access** tab. Select pages to add using the search box, which can also be used to select subpages.

### Add access from page settings

Open the target page or database for CleanupForNotion. Click the three dot menu in the top right corner, then choose Connections, and add the integration.

### Get data source IDs

1. Click on the settings button for the database
2. Choose "Manage data sources"
3. Click the ellipsis icon next to your data source
4. Click "Copy data source ID"

Note that those data source IDs are distinct from previously used database IDs.

### Get page IDs

Open the target page in full view and look at the URL:

```text
https://www.notion.so/workspace-name/Page-Name-22222222222222222222222222222222
                                               ^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^
```

## Configuration

### General options

* **AuthToken** (string, mandatory) — the Notion authentication token. See the [Setup in Notion](#setup-in-notion) section for details.
* **StateEngine** (string, optional) — the engine used persist state between runs. Defaults to `InMemory`. See below for supported engines.
* **StateAccessDetails** (string, optional) — the engine-specific identifier used to access the state store.
* **RunFrequency** (string, `"HH:MM:SS"`, optional) — the frequency of automated cleanups. Behavior depends on the deployment model.
* **DryRun** (boolean, defaults to `false`) — if true, no actual changes will be made to the Notion databases (but checks will still be done and items that would have been changed will be logged).

### Defining and configuring plugins

Plugins are configured in a **Plugins** array. Every plugin instance has its own configuration JSON object. The configuration JSON object contains:

* **PluginName** (string, mandatory) — the name of the plugin. Valid values: `DeleteByCheckbox`, `DeleteOnNewMonthlyCycle`, `DeleteWithoutRelationships`, `DeleteZeroSum`, `EnsureStaticRelatedPage`, `EstimateExpensesWithSubscriptions`. One plugin may be used multiple times in the configuration.
* **PluginDescription** (string, mandatory) — the description of the plugin instance. Should be unique and human-readable.
* **PluginId** (string, optional) — the ID of the plugin. Optional, used for webhooks or for executing a single plugin.
* **DataSourceId** (string, mandatory) — ID of the data source to work on. See [Setup in Notion](#setup-in-notion) for retrieval instructions.
* **PropertyName** (string, mandatory) — name of the property to base deletion checks on. This is the human-readable name visible in the database.
* **GracePeriod** (string, `"HH:MM:SS"`, optional) — defines the time after the last edit for which the page will not be modified. Defaults to 1 hour.

### State storage

There are four state storage engines available:

* `InMemory` — stores the state in memory. Data is not persisted between runs. This engine is recommended for testing only, although it may work well enough for long-running processes (Console/Web deployment with non-null `RunFrequency`). This engine is used when the **StateEngine** option is not defined.
* `File` — stores the state in a flat file. The **StateAccessDetails** setting should contain the path of the state file.
* `PostgreSQL` — stores the state in a PostgreSQL database. The **StateAccessDetails** setting should contain the connection string for Npgsql, e.g. `"Host=localhost;Username=postgres;Password=postgres;Database=cfn`
* `DynamoDB` — stores the state in a DynamoDB table. This engine is only available in the AWS deployment model, and it is the only engine supported in that model (the configuration is ignored).

### Webhooks

Webhooks are currently supported in the *Web application* and *AWS* deployment models. The webhook endpoint needs to be publicly accessible over HTTPS.

#### Exposing the webhook

If using the *Web application* deployment model, expose the `webhooks` endpoint publicly. You probably don’t want to expose the entire server, as it allows running cleanup without any authentication.

If using the *AWS* deployment model, set the `enable_webhooks` variable to `true`. After deploying, you can then read the function URL by using `terraform output lambda_function_url`.

#### Setup

Webhook actions can be configured under the **Webhooks** key in `appsettings.json`. It is a list of objects with **DataSourceId** (string) and **PluginIds** (array of string) keys. See below for an example.

1. To start receiving events from Notion, [set up the webhook in your Notion integration](https://developers.notion.com/reference/webhooks#getting-started-with-webhooks). CleanupForNotion currently supports page and data source change events; Notion appears to send all updates to a database as page creation, deletion, and property update events.
2. Notion will make a request to your webhook endpoint with a `verification_token`. The token will appear in CleanupForNotion logs (you can search for `Received new webhook request with body`).
3. In Notion, click **Verify**, paste your `verification_token`, and click **Verify subscription**.
4. In `appsettings.json`, set **WebhookSecret** to the value of the `verification_token`. Don't forget to restart the web application / upload the updated configuration to S3.

### Putting it all together

Here is an example CleanupForNotion configuration:

```json
{
  "AuthToken": "ntn_XXX",
  "StateEngine": "File",
  "StateAccessDetails": "/srv/CleanupForNotion/cfnstate.json",
  "RunFrequency": "01:00:00",
  "Plugins": [
    {
      "PluginName": "DeleteOnNewMonthlyCycle",
      "PluginDescription": "Secondary Bank Card Fee cycle enforcement",
      "DataSourceId": "00000000000000000000000000000000",
      "PropertyName": "Date",
      "TimeZoneName": "Europe/Warsaw",
      "CycleResetDay": 10
    },
    {
      "PluginName": "DeleteByCheckbox",
      "PluginDescription": "Delete checked items",
      "DataSourceId": "11111111111111111111111111111111",
      "PropertyName": "Delete",
      "PluginId": "one"
    },
    {
      "PluginName": "DeleteWithoutRelationships",
      "PluginDescription": "Delete log rows with no items",
      "DataSourceId": "22222222222222222222222222222222",
      "PropertyName": "Items",
      "GracePeriod": "00:10:00",
      "PluginId": "two"
    },
    {
      "PluginName": "DeleteZeroSum",
      "PluginDescription": "Delete zero sum entries from Settlement Sheet",
      "DataSourceId": "33333333333333333333333333333333",
      "PropertyName": "Amount"
    },
    {
      "PluginName": "EnsureStaticRelatedPage",
      "PluginDescription": "Ensure all entries in Secondary Bank Card Fee point to target row",
      "DataSourceId": "44444444444444444444444444444444",
      "PropertyName": "SummaryRef",
      "RelatedPageId": "45454545454545454545454545454545",
      "GracePeriod": "00:01:00"
    },
    {
      "PluginName": "EstimateExpensesWithSubscriptions",
      "PluginDescription": "Estimate total for Secondary Bank Card Fee assuming all subscriptions are paid for",
      "DataSourceId": "55555555555555555555555555555555",
      "PropertyName": "Total with Subscriptions",
      "ExpensesDataSourceId": "56565656565656565656565656565656",
      "VendorPropertyName": "Vendor",
      "AmountPropertyName": "Amount",
      "AlternateVendorPropertyName": "Alternate Names",
      "ActualTotalPropertyName": "Actual Total",
      "CurrencyDecimalPlaces": 2
    }
  ],
  "WebhookSecret": "XXX",
  "Webhooks": [
    {"DataSourceId": "11111111111111111111111111111111", "PluginIds": ["one", "two"]}
    {"DataSourceId": "22222222222222222222222222222222", "PluginIds": ["two"]}
  ]
}
```

For the Web and Console apps, an `appsettings.json` file is required in the same folder as the application `.dll`. Put the configuration created above in a `CleanupForNotion` key, like so:

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "CleanupForNotion": "Trace"
    }
  },
  "AllowedHosts": "*",
  "CleanupForNotion": {
    "AuthToken": "ntn_XXX",
    "...": "..."
  }
}
```

## Deployment

There are currently three supported deployment methods.

### Web application (in Docker)

In this model, CleanupForNotion runs as an ASP.NET Core web application.

If `RunFrequency` is configured, cleanup will be automatically executed on a timer. If it is null, cleanups need to be manually triggered over HTTP.

The HTTP API is simple:

* `POST /` — trigger a full cleanup. With the default port, call `curl -XPOST http://localhost:2836/`.
* `POST /plugin/{pluginId}` — trigger a single plugin by its ID.
* `POST /webhook` — react to changes sent by Notion.

The entire API should not be exposed on the public Internet. If using webhooks, expose only the webhook endpoint.

The easiest way to run the application in this model is with Docker and Docker Compose. The Docker image is hosted on [GitHub Container Registry](https://github.com/K-Wojciechowski/CleanupForNotion/pkgs/container/cleanupfornotion), with the latest release available as `docker pull ghcr.io/k-wojciechowski/cleanupfornotion:latest`.

```console
# mkdir /srv/CleanupForNotion
# cp docker-compose.yml /srv/CleanupForNotion
# vim /srv/CleanupForNotion/appsettings.json
# touch /srv/CleanupForNotion/cfnstate.json
# docker compose -f /srv/CleanupForNotion/docker-compose.yml up -d
```

The Docker Compose file is configured to expose the API on port 2836 on localhost (it won’t be accessible from other machines).

### AWS

In this model, CleanupForNotion uses various services offered by [AWS](https://aws.amazon.com/):

* Configuration (`appsettings.json`) is stored in **S3**
* Plugin state is stored in **DynamoDB**
* Code runs in **Lambda**
* Logs are stored in **CloudWatch**
* Periodic execution is handled by **EventBridge Scheduler**
* Optional webhook support using **Lambda Function URLs**

All of the above services have very generous free tiers. As long as you keep the execution frequency reasonable (15 minutes should be fine), the AWS costs related to using this should be $0/month (or close enough that AWS won’t charge your card).

To deploy this, all you need is [Terraform](https://developer.hashicorp.com/terraform). Here are the deployment steps:

1. Clone the repository and go into the `aws-terraform` directory
2. To get a pre-built package, run the `download-lambda.sh` / `download-lambda.ps1` script (Bash version requires `curl` and `jq`).
   If you prefer to build the code yourself and have the .NET 8 SDK, run the `build-lambda.sh` / `build-lambda.ps1` script (Bash version requires `zip`).
3. Place your appsettings.json in the `aws-terraform` directory (note that `StateFilePath` and `RunFrequency` are ignored)
4. Make sure you are happy with the configuration in `variables.tf`
5. Ensure you have AWS access keys configured in a way Terraform understands (e.g. `~/.aws/credentials`).
6. Run `terraform init`
7. Run `terraform apply`

After the deployment completes, `invoke.sh` and `invoke.cmd` scripts will be generated to run your new lambda using the AWS CLI.

### Console

In this model, CleanupForNotion runs as a console application. It can be run in the usual .NET way (`dotnet run`).

If `RunFrequency` is configured, the application will run forever, and cleanup will be automatically executed on a timer. If it is null, the application will perform one cleanup and exit.

## Roadmap

This is more-or-less feature-complete, at least for my requirements. (Although I reserve the right to come up with some other ways to abuse Notion that will require automated cleanup.)

Some planned features may be found in the [GitHub Issues][] for the project.

I accept feature requests (with no guarantee for their delivery) as [GitHub Issues][] as well.

[GitHub Issues]: https://github.com/K-Wojciechowski/CleanupForNotion/issues

## Legal

This code is licensed under the MIT license.

None of the code in the `src` folder was created with AI/LLM assistance. Some tests (especially the most mundane ones) were written with the assistance of GitHub Copilot.
