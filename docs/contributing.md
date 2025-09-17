# Contribute to CollectServiceFabricData

## Submitting issues

To report issue with collectsfdata, create a new issue in github repo [new issue](https://github.com/microsoft/CollectServiceFabricData/issues/new/choose)

## GitHub Actions and event policy

[CI CollectSFData](../.github/workflows/workflow.yml) builds pull requests targeting
`main` or `master` on opening, reopening, and subsequent updates with a read-only
contents token and no persisted checkout credentials. The existing documentation,
configuration, and Kusto path exclusions still apply.

Pushes to `main` or `master` build the exact pushed commit independently of PR
artifacts. A release is published only after that build succeeds and the GitHub
API identifies a merged PR whose merge commit, base repository, and base branch
match the push. The PR must have the `Release` or `Pre-release` label at the time
of the metadata check; `Pre-release` takes precedence if both labels are present.
Direct pushes and unlabelled merges build without publishing. Set release labels
before merging. Squash, merge-commit, and rebase merges rely on GitHub reporting
the pushed commit as the PR's merge commit. Pushes containing multiple merges
consider only the PR matching the pushed tip.

Only the release job receives `contents: write`. It downloads artifacts from its
own trusted push run, never from a pull request run, and targets the pushed commit
when creating a release tag.

Automatic PR labeling has been removed. Maintainers apply labels manually.
No workflow uses `pull_request_target`, so the November 2, 2026 restriction on
that event does not require a workflow exception, JIT administrator action, or
the exception-specific CodeQL/security approval process. This does not waive
any other repository security requirements. The changes must be merged into
the upstream repository's default branch to take effect there.

Actions are pinned to immutable commit SHAs. Dependabot checks action updates
weekly; review those updates before merging.

For local validation, run `actionlint` against the CI workflow and
`node --test .github\tests\release-policy.test.cjs` to exercise the release
eligibility script directly from the workflow without publishing anything.
These checks do not replace live GitHub validation.

## Building

### CollectSFData / CollectSFDataDll

Visual Studio 2019 with .net 4.6.2 / net6.0 / net8.0
or  
Visual Studio Code with .net 4.6.2 / net6.0 / net8.0

### **launch.json**

```json
{
    "version": "0.2.0",
    "configurations": [
        {
            "name": "PowerShell Launch Current File",
            "type": "PowerShell",
            "request": "launch",
            "script": "${file}",
            "cwd": "${file}"
        },
        {
            "name": ".NET Core Launch (console)",
            "type": "coreclr",
            "request": "launch",
            "preLaunchTask": "build",
            "program": "${workspaceFolder}/src/bin/Debug/netcoreapp3.1/CollectSFData.dll",
            "args": [],
            "cwd": "${workspaceFolder}/src/bin/Debug/netcoreapp3.1",
            "console": "internalConsole",
            "stopAtEntry": false
        },
        {
            "name": ".NET Core Attach",
            "type": "coreclr",
            "request": "attach",
            "processId": "${command:pickProcess}"
        }
    ]
}
```

### commandline

```powershell
help .\dotnet-build.ps1

dotnet-build.ps1 [[-targetFrameworks] <string[]>] [[-configuration] <Object>] [[-runtimeIdentifier] <Object>] [[-projectDir] <string>] [[-nugetFallbackFolder] <string>] [-publish] [-clean] [-replace]
```

#### **example**

to build all configured .net versions debug and release:

```powershell
.\dotnet-build.ps1
```

to build a specific configuration:

```powershell
.\dotnet-build.ps1 -targetFrameworks net462 -configuration debug -runtimeIdentifier win-x64
```

Visual Studio Code with .net6.0 / net8.0 and powershell 7.0+
or
Visual Studio 2019 with .net6.0 / net8.0 and powershell 7.0+

### App registration (user) setup

azure application registration ids are required for certain tests.  
msal confidentialclient does not require replyurls.  
**NOTE: msal confidentialclient does require that api permissions be set on app registration in portal after creation**

#### **powershell test script app registration id setup**

the following script will create an app registration using certificate logon for confidentialclient msal .net core authentication.
This client is used for creating resources in azure for testing collectsfdata.

```powershell
.\scripts\azure-az-create-aad-application-spn.ps1 `
  -aadDisplayName collectSFDataDllTestClient `
  -uri http://collectSFDataDllTestClient `
  -logontype cert `
  -password {{cert password}}

...
application id: 59c41f0c-fb6c-43e7-a070-480e2af83838
tenant id: 1a4b5850-4150-4da6-9d0e-4cfcc078292b
application identifier Uri: http://collectSFDataDllTestClient
cert and key base64: MIIDHjCCAgagAwIBAgIQFgKT81w9vapAjxN...
thumbprint: C124CE6208B0547CB576019104FDDF97B01A37A8
pfx path: C:\Users\user\AppData\Local\Temp\collectSFDataDllTestClient.pfx
clientid / applicationid saved in $global:applicationId
clientsecret / base64 thumb saved in $global:clientSecret

```

collectSfDataDllTestProperties.json diff:

```diff
{
+  "testAzClientId": "59c41f0c-fb6c-43e7-a070-480e2af83838",
+  "testAzClientCertificate": "MIIDHjCCAgagAwIBAgIQFgKT81w9vapAjxN...",
+  "testAzClientName": "collectSFDataDllTestClient",
}
```

#### **powershell collectsfdata app registration id setup**

the following script will create an app registration using certificate logon for confidentialclient msal collectsfdata testing.
this client is used for testing azure client authentication in collectsfdata.

```powershell
.\scripts\azure-az-create-aad-application-spn.ps1 `
  -aadDisplayName collectsfdataApp `
  -uri http://collectsfdataApp `
  -logontype cert `
  -password {{cert password}}

...
application id: 14b3dd02-66ec-46b4-b7aa-b65abc9bbb4d
tenant id: 1a4b5850-4150-4da6-9d0e-4cfcc078292b
application identifier Uri: http://collectsfdata
cert and key base64: MIIDEjCCAfqgAwIBAgIQPnmXz4qmKIpHlu...
thumbprint: 8C1AD1A0DBA04F78F7EE86FBDBC6E9CF06DB79E3
pfx path: C:\Users\user\AppData\Local\Temp\collectsfdataApp.pfx
clientid / applicationid saved in $global:applicationId
clientsecret / base64 thumb saved in $global:clientSecret

```

collectSfDataDllTestProperties.json diff:

```diff
{
+  "AzureClientId": "14b3dd02-66ec-46b4-b7aa-b65abc9bbb4d",
+  "AzureClientCertificate": "MIIDEjCCAfqgAwIBAgIQPnmXz4qmKIpHlu...",
+  "AzureClientSecret": "{{private key}}",
+  "AzureTenantId": "1a4b5850-4150-4da6-9d0e-4cfcc078292b",
}
```

### Environment Setup

- .\scripts\setup-test-env.ps1

```powershell
.\scripts\setup-test-env.ps1 creates the following configuration file: $env:LocalAppData\collectsfdata\CollectSFDataDllTestProperties.json
```

using output from .\scripts\azure-az-create-aad-application-spn.ps1, enter:
- AzureClientId = 'application id'
- AzureClientCertificate = 'clientsecret'
- AzureTenantId = 'tenant id'

example using values from above:

```json
{
  "testAzClientId": "59c41f0c-fb6c-43e7-a070-480e2af83838",
  "testAzClientCertificate": "MIIDHjCCAgagAwIBAgIQFgKT81w9vapAjxN...",
  "testAzStorageAccount": "CollectSFDataDllTests",
  "testAzClientName": "collectSFDataDllTestClient",
  "testCertificateNoPasswordBase64": "",
  "testCertificateWithPasswordBase64": "",
  "testCertificatePassword": "",
  "testAdminUserName": null,
  "testAdminPassword": null,
  "AzureClientId": "14b3dd02-66ec-46b4-b7aa-b65abc9bbb4d",
  "AzureClientCertificate": "MIIDEjCCAfqgAwIBAgIQPnmXz4qmKIpHlu...",
  "AzureClientName": "collectsfdataDllTestApp",
  "AzureClientSecret": "{{private key}}",
  "AzureResourceGroup": "collectsfdataunittest",
  "AzureResourceGroupLocation": "eastus",
  "AzureSubscriptionId": "a79c1c37-7eba-4378-8000-abd3a23e66d8",
  "AzureTenantId": "1a4b5850-4150-4da6-9d0e-4cfcc078292b",
  "KustoCluster": "https://ingest-sfcluster.eastus.kusto.windows.net/sfdatabase",
  "SasKey": "{{sas key}}"
}
```

clean example:

```json
{
  "testAzClientId": "{{test client app registration}}",
  "testAzClientCertificate": "{{test client base64 certificate}}",
  "testAzStorageAccount": "{{test storage account name}}",
  "testAzClientName": "collectSFDataDllTestClient",
  "testCertificateNoPasswordBase64": "",
  "testCertificateWithPasswordBase64": "",
  "testCertificatePassword": "",
  "testAdminUserName": null,
  "testAdminPassword": null,
  "AzureClientId": "{{collectsfdata app registration}}",
  "AzureClientCertificate": "{{collectsfdata client base64 certificate}}",
  "AzureClientName": "collectsfdataDllTestApp",
  "AzureClientSecret": "{{private key}}",
  "AzureResourceGroup": "{{azure resource group name}}",
  "AzureResourceGroupLocation": "{{azure resource group location}}",
  "AzureSubscriptionId": "{{azure subscription id}}",
  "AzureTenantId": "{{azure tenant id}}",
  "KustoCluster": "{{kusto ingest url and database}}",
  "SasKey": "{{sas key}}"
}
```

## Kusto Setup

to create a test kusto cluster for testing, the below script can be used. Pass resource group location and application registration id (azureclientid) to script.  
script will create dev kusto cluster and database. Script will add app registration id as a database admin.  
output will display 'kustocluster' kusto ingest url with database.

.\scripts\azure-az-create-kusto-cluster.ps1 -resourceGroupLocation {{location}} -appRegistrationId {{app registration id for 'collectsfdata'}}

example output:

```text
finished. object stored in `$global:cluster
data ingestion uri: https://ingest-collectsfdataunittest.eastus.kusto.windows.net/collectsfdatadb
```

## Testing

### Test Project Targets

The test project multi-targets: `net462; net48; net6.0; net8.0`. Modern language features and nullable are enabled only for non-full-framework targets via conditional properties.

### Running Tests (Quick Start)

```powershell
# Fast run for latest framework only
dotnet test src/CollectSFData.Tests/CollectSFData.Tests.csproj -f net8.0

# Run all frameworks (slower)
dotnet test src/CollectSFData.Tests/CollectSFData.Tests.csproj

# Use custom runsettings (stabilizes host & disables parallelization)
dotnet test src/CollectSFData.Tests/CollectSFData.Tests.csproj -f net8.0 --settings src/CollectSFData.Tests/CollectSFData.Tests.runsettings

# Filter to a subset (example new process tests)
dotnet test src/CollectSFData.Tests/CollectSFData.Tests.csproj -f net8.0 --settings src/CollectSFData.Tests/CollectSFData.Tests.runsettings --filter "FullyQualifiedName~FileManagerProcessTests"
```

### Runsettings Rationale

`CollectSFData.Tests.runsettings` was added to mitigate intermittent file locking and PowerShell runspace errors by:

- Disabling AppDomain reuse (`DisableAppDomain=true`) so each run uses a clean test host.
- Forcing single-threaded execution (`MaxCpuCount=1`, xUnit parallelization off) to avoid simultaneous copies of large dependent assemblies.
- Increasing session timeout to allow slower PowerShell-invoking tests to complete without abrupt host termination.

Enable code coverage later by uncommenting the `XPlat code coverage` collector block inside the runsettings file.

### File Lock (MSB3021 / MSB3027) Mitigation Guide

Symptoms:

- Build warnings `MSB3026` escalating to errors `MSB3027` / `MSB3021` mentioning `CollectSFDataDll.dll` locked by `testhost`.
- Subsequent builds for the same TFM fail until the locking process exits.

Root Causes:

- Parallel test executions across target frameworks holding open file handles.
- Post-build PowerShell scripts executing while the prior testhost still loads the DLL.

Mitigations (apply in order):

1. Use single target with `--framework net8.0` while iterating.
2. Add `--no-build` when re-running quickly after a successful build.
3. Use the provided runsettings to disable parallelization & AppDomain reuse.
4. If locks persist, close IDE test sessions or manually terminate `testhost.exe` processes (outside constrained environments).
5. For CI: run target frameworks sequentially (`dotnet test -f net6.0` then `-f net8.0`) instead of simultaneous matrix on a single workspace if locks reappear.

### PowerShell Runspace Exception

Error: `System.Management.Automation.PSInvalidOperationException: There is no Runspace available…`

Causes & Remedies:

- Host teardown while a timer in a PowerShell-invoking component still references a disposed runspace.
- Long-running or asynchronous PowerShell script triggered during test cleanup.

Workarounds implemented:

- Longer `TestSessionTimeout` (10 minutes) to reduce abrupt cancellation.
- Serial execution eliminating overlapping teardown phases.

### Adding New Tests – Guidelines

Focus areas still needing coverage:

- Serialization chunking thresholds (CSV & JSON) for large collections.
- Compression branches (when configuration enables compression).
- Observer/ETL timeout & error pathways.
- Ingestion mode branching (Kusto vs Log Analytics vs none) with mocked dependencies.
- Concurrency safety of collection modifications under simulated parallel enumeration.

Use patterns already present (FluentAssertions + xUnit). Prefer small, fast unit tests; only integrate PowerShell/IO when asserting critical behaviors.

### Traits / Filtering (Optional Enhancement)

Introduce `[Trait("Category","Fast")]` vs `"Integration"` on future tests to enable:

```powershell
dotnet test --filter "Category=Fast"
```

### Coverage (Future)

Once stable, enable coverage collection:

```powershell
dotnet test -f net8.0 --settings src/CollectSFData.Tests/CollectSFData.Tests.runsettings /p:CollectCoverage=true
```

Or uncomment the coverage collector in runsettings for portable output formats (cobertura, lcov, opencover).

## Creating pull requests

Fork repo and create pull request as normal across forks.
Maintainers apply labels manually; there is no automatic labeler.
If executable changes, git actions will build.

## Creating Releases

### Build without publishing

1. Open a PR targeting `main` or `master` and include a change outside the
   excluded `docs/`, `configurationFiles/`, and `kusto/` paths.
2. In the PR checks, open **CI CollectSFData** and wait for the `build` job to
   succeed. It builds with the `Release` configuration and packs the NuGet
   package; it cannot publish a GitHub release.
3. Open the run summary under **Actions > CI CollectSFData** and download the
   `Release` artifact to inspect the build output.
4. To merge without publishing, leave both release labels off the PR. The
   resulting push build also uploads a `Release` artifact.

A `Release` configuration build or an artifact named `Release` does not by
itself publish a release.

### Publish a release or prerelease

1. Ensure the migrated workflow is present on the upstream repository's default
   branch and Actions is enabled. Use the upstream repository, not a fork, to
   publish the official release.
2. Prepare a PR targeting `main` or `master`, including a change outside the
   excluded paths. Documentation-, configuration-, or Kusto-only changes do not
   trigger this workflow, even when the PR has a release label.
3. Review and validate the PR. Before merging, use the PR's **Labels** control
   to apply exactly one of the following labels (create it under the
   repository's **Issues > Labels** page if it does not exist):
   - `Release`: publish a stable GitHub release.
   - `Pre-release`: publish a GitHub prerelease.
   Label names are case-sensitive. If both are present, prerelease takes
   precedence. These labels must be applied manually; no labeler is required.
4. Merge the approved PR into `main` or `master`. Do not use a direct push as a
   release trigger. For a push containing multiple merged PRs, only the PR
   matching the pushed tip is considered.
5. Under **Actions > CI CollectSFData**, open the **push** run for that merge,
   not the PR run. Verify:
   - `build` succeeds and uploads the `Release` artifact.
   - `release_metadata` succeeds and identifies the matching merged,
     release-labelled PR.
   - `release` succeeds after both jobs complete.
6. Open the repository's **Releases** page. Verify the release/prerelease
   designation, commit/tag, generated notes, `CollectSFData.zip`, and NuGet
   package attachments.

The tag is `v` followed by the built executable's file version. That version is
generated from UTC build time by
[CollectSFData.csproj](../src/CollectSFData/CollectSFData.csproj), so rebuilding
the same commit later can produce a different version. The workflow attaches
the NuGet package to the GitHub release; it does not publish to NuGet.org.
The executable is strong-name signed, which is not an Authenticode signature.

### Troubleshooting a release build

- **No workflow run:** check the target branch and path exclusions. Adding a
  label after merging does not start a workflow.
- **Release skipped:** check that the matching merged PR has the exact release
  label and that its merge commit matches the pushed SHA. A successful
  unlabelled build or direct push intentionally does not publish.
- **Metadata check failed:** inspect the job's GitHub API error. API failures
  and ambiguous merged-PR matches fail explicitly rather than publishing.
- **Build or publication failed:** fix the reported failure and follow the
  normal reviewed-PR process. For a transient failure, use GitHub's **Re-run
  jobs** control on the original push run after checking the labels and any
  existing release/tag. Reruns read current labels, and a full rebuild may
  generate a new version; inspect existing assets before retrying publication.

Live publication must be verified on GitHub after deployment. Local linting and
release-policy tests do not prove a release has been published.