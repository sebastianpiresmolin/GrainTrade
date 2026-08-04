# Configuration

Every setting the hosts read, what it does, and what a deployed value looks
like. `appsettings.json` holds the non-secret defaults; anything with a
credential in it belongs in user-secrets locally and in the platform's secret
store when deployed.

## Settings

### `ConnectionStrings:Orleans` — silo and api-host

Postgres for grain storage (`accounts`, `orderbooks`) and cluster membership.

- **Empty (default)** — localhost clustering + memory storage. The whole stack
  runs with no database; state is lost on restart.
- **Set** — durable storage and a real membership table.

```
Host=db.example.com;Port=5432;Database=graintrade;Username=graintrade;Password=…
```

Both hosts must use the **same** value: the client finds the silo through the
membership table, so a mismatch means the api-host never sees the cluster.

Contains a password — never commit it. See [Secrets](#secrets).

### `Cors:AllowedOrigins` — api-host

Origins the browser may call the API from: the SvelteKit host's public URL,
scheme and port included, no trailing slash.

```json
"Cors": { "AllowedOrigins": [ "https://graintrade.example.com" ] }
```

Defaults to `http://localhost:5173` when absent. Getting this wrong doesn't
fail at startup — requests just get blocked in the browser.

### `Logging:LogLevel` — both

Standard ASP.NET Core logging. `Information` in development; `Warning` is a
reasonable production default.

## Not yet configuration

Two values are still hardcoded, both in [web/src/lib/server/api.ts](web/src/lib/server/api.ts):

| Value | Why it's not settled |
| --- | --- |
| `API_BASE` | The api-host URL the SvelteKit server calls. Should become `$env/dynamic/private` before deploying. |
| `ACCOUNT_ID` | A single hardcoded account. Goes away with authentication. |

## Secrets

Local development uses user-secrets, which live outside the repo in
`%APPDATA%\Microsoft\UserSecrets\<UserSecretsId>\secrets.json`:

```powershell
$cs = "Host=localhost;Port=5432;Database=graintrade;Username=postgres;Password=YOURS"
dotnet user-secrets set "ConnectionStrings:Orleans" $cs --project silo/GrainTrade.Silo
dotnet user-secrets set "ConnectionStrings:Orleans" $cs --project api-host

dotnet user-secrets list --project silo/GrainTrade.Silo
```

They're plaintext — user-secrets keeps credentials out of the repo, not off the
disk.

When deploying, environment variables override `appsettings.json` without
editing it. `:` becomes `__`, and arrays are indexed:

```bash
ConnectionStrings__Orleans="Host=…;Password=…"
Cors__AllowedOrigins__0="https://graintrade.example.com"
```

## Precedence

Later wins:

```
appsettings.json → appsettings.{Environment}.json → user-secrets (dev only) → environment variables
```

So the empty `ConnectionStrings:Orleans` in `appsettings.json` documents that
the key exists and marks memory mode as the fallback; every other layer
overrides it.

## Deploy checklist

- [ ] `ConnectionStrings:Orleans` set on **both** silo and api-host, same value
- [ ] Orleans schema applied to the target database (`db/orleans/*.sql`, in filename order)
- [ ] `Cors:AllowedOrigins` set to the web host's real URL
- [ ] `API_BASE` in the web app pointed at the deployed api-host
- [ ] Silo reachable by the api-host — they share a cluster, not just a database
