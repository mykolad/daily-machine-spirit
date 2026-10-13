# Infrastructure

Everything the site runs on in Azure is in Bicep: `main.bicep` creates the resource group `machinespirit-rg` (Sweden
Central) and everything in it. This page covers the few things Bicep can't do: the first deployment, the secret values,
and what lives outside Azure.

Every infrastructure change goes to staging first: production takes it only with `deployProduction=true` (false by
default). Production is live since #50.

| Resource | Name | Notes |
|---|---|---|
| Resource group | `machinespirit-rg` | |
| AI Services | `machinespirit-ai` | `gpt-6-sol` writes the rites, `gpt-6-luna` is the fallback; keys off |
| Cosmos DB | `machinespirit-cosmos` | free tier (1000 RU/s, 25 GB for the account's lifetime); databases `machinespirit` and `machinespirit-staging` at 400 RU/s each, container `rites` (partition key `/partition`); keys off |
| Key Vault | `machinespirit-kv`, `machinespirit-kv-staging` | one per environment, so staging can't read production's secrets |
| Storage | `machinespiritprod`, `machinespiritstaging` | the Functions host's storage; keys off |
| Functions app | `machinespirit-app`, `machinespirit-app-staging` | Flex Consumption, .NET 10 isolated, 512 MB instances, at most 10; staging admits only the owner's IP, production only Cloudflare |
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

Add `deployProduction=true` to create production too (it was, with #50). A deployment without it leaves production as
it is: nothing there is deleted, but nothing is updated either.

## Later changes

Into the existing group, with `resources.bicep`: that's all the infrastructure identity (Owner of this group only) can do,
so a deploy workflow will use the same command. Staging first, previewed with `what-if`:

```powershell
$owner = az ad signed-in-user show --query id -o tsv
$ip = Invoke-RestMethod https://api.ipify.org
az deployment group what-if -g machinespirit-rg --template-file infra/resources.bicep `
  --parameters ownerPrincipalId=$owner stagingAllowedIp=$ip
az deployment group create -g machinespirit-rg --name machinespirit --template-file infra/resources.bicep `
  --parameters ownerPrincipalId=$owner stagingAllowedIp=$ip
```

Then, once staging works, production: the same two commands with `deployProduction=true` added to `--parameters`. Its
`what-if` lists production's resources; without the flag they're marked ignored (`*`), and a change meant for
production doesn't reach it. A change to production alone (its domain, its Cloudflare zone) needs the second step only.

(In bash, the same with `\` line breaks and `"$owner"`, `"$ip"`.) When your IP changes, deploy again with the new one.

## Secrets

Bicep creates the vaults but never their values. Put each value in a file outside the repository and set it from there,
so it stays out of your shell history. `-o none` matters: without it, `az keyvault secret set` prints the value back.
Production's vault exists only from launch (`deployProduction`); set its secrets then.

| Secret | Used for | The app reads it from |
|---|---|---|
| `JevApiKey` | judging drafts: their quality for the Augury, their similarity for "More rites" | `Jev__ApiKey`, a Key Vault reference |
| `OtlpHeaders` | telemetry to Grafana Cloud (this site's own access-policy token) | `OTEL_EXPORTER_OTLP_HEADERS`, a Key Vault reference |
| `GitHubClientSecret` | readers signing in with GitHub to vote: the OAuth app's client secret | `GitHub__ClientSecret`, a Key Vault reference |
| `VoterKeySecret` | keying each account's votes. Set once: a new value makes every account a new voter. At least 32 characters | `Voters__KeySecret`, a Key Vault reference |
| `VoterSessionSecret` | signing the sign-in cookie. A new value only signs everyone out. At least 32 characters | `Voters__SessionSecret`, a Key Vault reference |
| `CloudflareApiToken` | production only: purging pages from Cloudflare's cache when they change | `Cloudflare__ApiToken`, a Key Vault reference (only where `zoneId` is set) |

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

### Readers' sign-in with GitHub

Readers sign in with GitHub to vote. Each environment has its own GitHub OAuth app, because an app has one callback URL.
Until the app and all three secrets are set, the sign-in pages answer 404 and nobody is signed in.

1. On GitHub, **Settings → Developer settings → OAuth Apps → New OAuth App**, under the account that should own it:
   - Homepage URL: `https://machinespirit-app-staging.azurewebsites.net` (production: `https://dailymachinespirit.fyi`)
   - Authorization callback URL: `https://machinespirit-app-staging.azurewebsites.net/auth/github/callback`
     (production: `https://dailymachinespirit.fyi/auth/github/callback`). It must match `signInCallbackUrl` in
     `resources.bicep` exactly.
   - No device flow. The app asks for no scopes: its token only reads the account's id, and is revoked at once.
2. Put the **Client ID** in `signInGitHubClientIds` in `resources.bicep` (it isn't secret) and deploy the infrastructure.
3. **Generate a new client secret**, save it to `$HOME\.machinespirit\github-client-secret.txt`, and set it with the
   two random secrets:

```powershell
foreach ($name in 'voter-key', 'voter-session') {
  $bytes = [byte[]]::new(32); [Security.Cryptography.RandomNumberGenerator]::Fill($bytes)
  Set-Content -Path "$HOME\.machinespirit\$name.txt" -Value ([Convert]::ToBase64String($bytes)) -NoNewline
}
az keyvault secret set --vault-name machinespirit-kv-staging --name GitHubClientSecret `
  --file $HOME\.machinespirit\github-client-secret.txt -o none
az keyvault secret set --vault-name machinespirit-kv-staging --name VoterKeySecret `
  --file $HOME\.machinespirit\voter-key.txt -o none
az keyvault secret set --vault-name machinespirit-kv-staging --name VoterSessionSecret `
  --file $HOME\.machinespirit\voter-session.txt -o none
az functionapp restart -g machinespirit-rg -n machinespirit-app-staging
```

Keep `voter-key.txt`: it's what makes an account the same voter, and a new one would let every account vote again.

### Purging Cloudflare's cache

Cloudflare keeps Today and each rite's page for up to an hour, and the app purges them when a vote or a new rite changes
them. Set this up before the Cache Rule (Outside Azure); without it, a vote would show elsewhere only an hour later.

1. Put the zone's id (Cloudflare dashboard → `dailymachinespirit.fyi` → Overview → Zone ID; not secret) in
   `productionDomain.zoneId` in `resources.bicep`, and deploy the infrastructure.
2. In the Cloudflare dashboard, **My Profile → API Tokens → Create Token → Custom token**: permission **Zone → Cache
   Purge → Purge**, zone resources **Include → Specific zone → dailymachinespirit.fyi**, nothing else. Save it to
   `$HOME\.machinespirit\cloudflare-purge-token.txt`.

```powershell
az keyvault secret set --vault-name machinespirit-kv --name CloudflareApiToken `
  --file $HOME\.machinespirit\cloudflare-purge-token.txt -o none
az functionapp restart -g machinespirit-rg -n machinespirit-app
```

Purges are counted (`dms.pages.purges`, `purged` or `failed`); a failed one is also logged, and the page refreshes
within the hour.

## Deploying the app

GitHub Actions deploys the app (`.github/workflows/`), signed in as the environment's GitHub identity:

- **Deploy Master**: every merge to `master` is built and tested again, then that package goes to staging, and once
  staging passes its smoke tests, to production. No approval: staging is the gate, and the `production` environment
  admits deployments from `master` only.
- **Deploy Branch to Staging**: Actions → Deploy Branch to Staging → Run workflow, with a branch name, to try a PR
  on staging before merging. Staging keeps it until the next deployment.

Each deployment ends with smoke tests (`tests/DailyMachineSpirit.SmokeTests`), and rolls back to the last good master
if they fail. For them, the workflow briefly adds the runner's address to the app's access rules (`smoke-tests`), since
staging admits only your address and production only Cloudflare: each GitHub identity is Website Contributor on its own
app, which is enough.

`https://machinespirit-app-staging.azurewebsites.net/healthz` shows the deployed commit (only from your address).
Production's, `https://machinespirit-app.azurewebsites.net/healthz`, answers only Cloudflare: through the domain, once it
points there (#31).

By hand, from a checkout (Azure Functions Core Tools v4, signed in with `az login`):

```powershell
cd src/DailyMachineSpirit.Functions
func azure functionapp publish machinespirit-app-staging --dotnet-isolated
```

## Outside Azure

- **Cloudflare** (`dailymachinespirit.fyi`) sits in front of production (set up 2026-10-12):
  - **SSL/TLS** in **Full (strict)** mode, with a **Cloudflare Origin CA certificate** (SSL/TLS → Origin Server) for
    `dailymachinespirit.fyi` and `*.dailymachinespirit.fyi`, valid 15 years. It lives in production's vault as the
    secret `dailymachinespirit-origin`: a PFX with Cloudflare's origin root
    (`https://developers.cloudflare.com/ssl/static/origin_ca_rsa_root.pem`), content type `application/x-pkcs12`.
    Bicep imports it into the app (`productionDomain` in `resources.bicep`) and binds the domain. When it nears its
    end, make a new one, set the secret again, and deploy the infrastructure.
  - **DNS**: a proxied CNAME `@` → `machinespirit-app.azurewebsites.net`, and a TXT record `asuid` with the app's
    `customDomainVerificationId`, which Azure checks before binding the domain.
  - **A Cache Rule** (Caching → Cache Rules), after the purge is set up (Secrets): hostname equals
    `dailymachinespirit.fyi` and the URI path doesn't start with `/api/`, `/auth/`, `/scriptorium` or `/healthz`;
    **Eligible for cache**, Edge TTL **Use cache-control header if present, bypass cache if not**. The app's
    `Cache-Control` decides the rest: Today and a rite's page an hour at the edge (`s-maxage`), purged on change, the
    archive a minute, errors and the API never. `cf-cache-status: HIT` on a second request for `/` shows it works.
  - **A rate limit rule** (Security → WAF → Rate limiting rules, set up 2026-10-13) for votes and searches (each
    search reads every rite), counted by IP, 20 in 10 seconds, blocked for 10 seconds. The Free plan has one such rule,
    so every limited path shares it:
    `(http.request.uri.path wildcard r"/api/rites/*/vote") or (http.request.uri.path eq "/search")`. With `eq`, the
    `*` would be taken literally, and match nothing.

  Production admits only Cloudflare's address ranges (`cloudflare-ips.json`), so its cache and rate limit can't be gone
  around. **Check Cloudflare's Addresses** (a weekly workflow) opens an issue when Cloudflare publishes different
  ranges: copy them into the file, and deploy the infrastructure (Later changes).
- **Cloudflare Access** guards production's Scriptorium (set up 2026-10-12). In the Cloudflare One dashboard:
  - **GitHub as a login method** (Integrations → Identity providers): its own GitHub OAuth app, separate from the
    readers' sign-in apps, with the callback `https://<team>.cloudflareaccess.com/cdn-cgi/access/callback`. Its client
    secret stays in Cloudflare.
  - **A self-hosted application** (Access controls → Applications) with two destinations:
    - `dailymachinespirit.fyi/scriptorium`, the page;
    - `dailymachinespirit.fyi/scriptorium/*`, its actions (`/scriptorium/summon`, `/scriptorium/drafts/...`).

    A wildcard doesn't cover its parent path, and listing both doesn't depend on a path covering its children. GitHub
    is its only login method, and an Allow policy lists the Scribes' emails. A new Scribe is a new email there.
  - **Its Application Audience (AUD) tag** and the team domain (Settings → Team domain) are in `productionAccess` in
    `resources.bicep`: neither is secret, and there every deployment keeps them. Without both, production's
    Scriptorium lets nobody in.
- **GitHub**: one environment per deploy identity, holding its ids as variables (`AZURE_CLIENT_ID` from
  `githubClientIds` in the deployment's outputs, `AZURE_TENANT_ID`, `AZURE_SUBSCRIPTION_ID`). `staging` exists;
  `production` and `infrastructure` come with their workflows.

## Deleting it

`az group delete -n machinespirit-rg` removes everything. The vaults have purge protection, so their names stay taken
for 90 days.
