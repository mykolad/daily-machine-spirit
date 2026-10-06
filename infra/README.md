# Infrastructure

Everything the site runs on in Azure is in Bicep: `main.bicep` creates the resource group `machinespirit-rg` (Sweden
Central) and everything in it. This page covers the few things Bicep can't do: the first deployment, the secret values,
and what lives outside Azure.

| Resource | Name | Notes |
|---|---|---|
| Resource group | `machinespirit-rg` | |
| AI Services | `machinespirit-ai` | `gpt-6-sol` writes the items, `gpt-6-luna` is the fallback; keys off |
| Cosmos DB | `machinespirit-cosmos` | free tier (1000 RU/s, 25 GB for the account's lifetime); databases `machinespirit` and `machinespirit-staging` at 400 RU/s each, container `items`; keys off |
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

## Later changes

Into the existing group, with `resources.bicep`: that's all the infrastructure identity (Owner of this group only) can do,
so a deploy workflow will use the same command. Preview first with `what-if`:

```powershell
az deployment group what-if -g machinespirit-rg --template-file infra/resources.bicep `
  --parameters ownerPrincipalId=$owner stagingAllowedIp=$ip
az deployment group create -g machinespirit-rg --name machinespirit --template-file infra/resources.bicep `
  --parameters ownerPrincipalId=$owner stagingAllowedIp=$ip
```

(In bash, the same with `\` line breaks and `"$owner"`, `"$ip"`.) When your IP changes, deploy again with the new one.

## Secrets

Bicep creates the vaults but never their values. Put each value in a file outside the repository and set it from there,
so it stays out of your shell history:

```powershell
az keyvault secret set --vault-name machinespirit-kv-staging --name JevApiKey --file $HOME\.machinespirit\jev-key.txt
az keyvault secret set --vault-name machinespirit-kv --name JevApiKey --file $HOME\.machinespirit\jev-key.txt
```

| Secret | Used for | Added in |
|---|---|---|
| `JevApiKey` | scoring items for "More rites" | the generation PR |
| `OtlpHeaders` | telemetry to Grafana Cloud (this site's own access-policy token) | the telemetry PR |

## Outside Azure

- **Cloudflare** (`dailymachinespirit.fyi`): the DNS records and the origin certificate, added with the custom domain.
- **GitHub**: the environments `staging`, `production` and `infrastructure`, each with its identity's client id
  (`githubClientIds` in the deployment's outputs), added with the deploy workflows.

## Deleting it

`az group delete -n machinespirit-rg` removes everything. The vaults have purge protection, so their names stay taken
for 90 days.
