# Infrastructure

Everything the site runs on in Azure is in Bicep: `main.bicep` creates the resource group `machinespirit-rg` (Sweden
Central) and everything in it. This page covers the few things Bicep can't do: the first deployment, the secret values,
and what lives outside Azure.

Production is off until launch (`deployProduction`, false by default): staging comes first. The production names below
are what it gets then.

| Resource | Name | Notes |
|---|---|---|
| Resource group | `machinespirit-rg` | |
| AI Services | `machinespirit-ai` | `gpt-6-sol` writes the rites, `gpt-6-luna` is the fallback; keys off |
| Cosmos DB | `machinespirit-cosmos` | free tier (1000 RU/s, 25 GB for the account's lifetime); databases `machinespirit` and `machinespirit-staging` at 400 RU/s each, container `rites` (partition key `/partition`); keys off |
| Key Vault | `machinespirit-kv`, `machinespirit-kv-staging` | one per environment, so staging can't read production's secrets |
| Storage | `machinespiritprod`, `machinespiritstaging` | the Functions host's storage; keys off |
| Functions app | `machinespirit-app`, `machinespirit-app-staging` | Flex Consumption, .NET 10 isolated, 512 MB instances, at most 10; staging admits only the owner's IP |
| GitHub identities | `machinespirit-github-staging`, `-production`, `-infrastructure` | OIDC from this repository's GitHub environments of the same names |

Each app has its own managed identity, with access to its own database, vault and storage, and to the models. Nothing in
Azure is reached with a key or a password: account keys are off, and so is basic-auth publishing, so deploys sign in with
Entra ID. The only credentials are for services outside Azure (Jev, Grafana Cloud), and they live in the vaults
(see Secrets).

## First deployment

Once, from your machine, as an Owner of the subscription: `main.bicep` creates the resource group, then everything in it.

```powershell
az login
$owner = az ad signed-in-user show --query id -o tsv
$ip = Invoke-RestMethod https://api.ipify.org   # staging admits only this address
az deployment sub create --location swedencentral --name machinespirit --template-file infra/main.bicep `
  --parameters ownerPrincipalId=$owner stagingAllowedIp=$ip
```

The same in bash:

```bash
az login
owner=$(az ad signed-in-user show --query id -o tsv)
ip=$(curl -s https://api.ipify.org)
az deployment sub create --location swedencentral --name machinespirit --template-file infra/main.bicep \
  --parameters ownerPrincipalId="$owner" stagingAllowedIp="$ip"
```

The Cosmos DB free tier can only be chosen when the account is created, and a subscription gets one free account.

At launch, add `deployProduction=true` to the same command, and the launch PR makes `true` the default. Once production
exists, a deployment without it leaves production alone: nothing is deleted, but nothing there is updated either.

## Later changes

Into the existing group, with `resources.bicep`: that's all the infrastructure identity (Owner of this group only) can do,
so a deploy workflow will use the same command. Preview first with `what-if`:

```powershell
$owner = az ad signed-in-user show --query id -o tsv
$ip = Invoke-RestMethod https://api.ipify.org
az deployment group what-if -g machinespirit-rg --template-file infra/resources.bicep `
  --parameters ownerPrincipalId=$owner stagingAllowedIp=$ip
az deployment group create -g machinespirit-rg --name machinespirit --template-file infra/resources.bicep `
  --parameters ownerPrincipalId=$owner stagingAllowedIp=$ip
```

(In bash, the same with `\` line breaks and `"$owner"`, `"$ip"`.) When your IP changes, deploy again with the new one.

## Secrets

Bicep creates the vaults but never their values. Put each value in a file outside the repository and set it from there,
so it stays out of your shell history. `-o none` matters: without it, `az keyvault secret set` prints the value back.
Production's vault exists only from launch (`deployProduction`); set its secrets then.

| Secret | Used for | The app reads it from |
|---|---|---|
| `JevApiKey` | scoring drafts: their quality for the Augury, their similarity for "More rites" | `Jev__ApiKey`, a Key Vault reference |
| `OtlpHeaders` | telemetry to Grafana Cloud (this site's own access-policy token) | `OTEL_EXPORTER_OTLP_HEADERS`, a Key Vault reference |

### Jev

```powershell
az keyvault secret set --vault-name machinespirit-kv-staging --name JevApiKey `
  --file $HOME\.machinespirit\jev-key.txt -o none
```

The app picks up a new value within a day, or at once after a restart (`az functionapp restart -g machinespirit-rg -n
machinespirit-app-staging`).

### Grafana Cloud

The site sends its telemetry to an existing Grafana Cloud stack, with a token of its own, so it can be revoked without
touching anything else.

1. In the Grafana Cloud portal, open the stack's **OpenTelemetry** tile (Configure): note the **instance ID** and the
   OTLP endpoint.
2. Under **Administration → Cloud access policies**, create a policy for that stack with the scopes `metrics:write`,
   `logs:write` and `traces:write`, then add a token to it. Save the token to `$HOME\.machinespirit\grafana-token.txt`.
3. Turn it into the OTLP header and set it. Name `-Path` and `-Value`: PowerShell 7.6 swaps them in
   `Set-Content -NoNewline <path> <value>`, which writes the header into a file *name* in the current directory.

```powershell
$token = (Get-Content -Path $HOME\.machinespirit\grafana-token.txt -Raw).Trim()
$basic = [Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes("<instance ID>:$token"))
Set-Content -Path $HOME\.machinespirit\otlp-headers.txt -Value "Authorization=Basic%20$basic" -NoNewline
az keyvault secret set --vault-name machinespirit-kv-staging --name OtlpHeaders `
  --file $HOME\.machinespirit\otlp-headers.txt -o none
```

The dashboard is in `observability/` (its README says how to import it).

## Deploying the app

GitHub Actions deploys the app (`.github/workflows/`), signed in as the environment's GitHub identity:

- **Deploy Master**: after every merge to `master` that passes Build and Test, that commit goes to staging.
- **Deploy Branch to Staging**: Actions → Deploy Branch to Staging → Run workflow, with a branch name, to try a PR
  on staging before merging. Staging keeps it until the next deployment.

`https://machinespirit-app-staging.azurewebsites.net/healthz` shows the deployed commit (only from your address).

By hand, from a checkout (Azure Functions Core Tools v4, signed in with `az login`):

```powershell
cd src/DailyMachineSpirit.Functions
func azure functionapp publish machinespirit-app-staging --dotnet-isolated
```

## Outside Azure

- **Cloudflare** (`dailymachinespirit.fyi`): the DNS records and the origin certificate, added with the custom domain.
- **Cloudflare Access** guards production's Scriptorium. In Zero Trust → Access → Applications, add a self-hosted
  application for `dailymachinespirit.fyi/scriptorium` with GitHub as the login method and a policy that allows the
  Scribes' accounts. Its overview shows the **Application Audience (AUD) tag**; the team domain is in Settings → Custom
  Pages (`<team>.cloudflareaccess.com`). Put both in `productionAccess` in `resources.bicep` (neither is secret, and
  there every deployment keeps them), set `scriptoriumEnabled: true` for production there, and deploy. Until both are
  set, production's Scriptorium lets nobody in, even if it's turned on.
- **GitHub**: one environment per deploy identity, holding its ids as variables (`AZURE_CLIENT_ID` from
  `githubClientIds` in the deployment's outputs, `AZURE_TENANT_ID`, `AZURE_SUBSCRIPTION_ID`). `staging` exists;
  `production` and `infrastructure` come with their workflows.

## Deleting it

`az group delete -n machinespirit-rg` removes everything. The vaults have purge protection, so their names stay taken
for 90 days.
