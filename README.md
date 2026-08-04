# GrainTrade

A mock stock-trading platform built to explore **Microsoft Orleans** (the actor
model — grains, persistence, timers, streams) and **SvelteKit** (Svelte 5 runes,
load functions, form actions).

Each user account and each ticker maps almost one-to-one onto a grain, which makes
trading a natural domain for Orleans: a grain processes one message at a time, so
an account's balance can't be corrupted by concurrent orders without any locking.

## Architecture

```
┌─────────────┐   HTTP    ┌──────────────────┐   Orleans   ┌─────────────────┐
│  SvelteKit  │──────────▶│   API host       │   client    │   Silo         │
│  (web)      │◀──────────│  (ASP.NET Core,  │───────────▶│  (grain host)   │
│             │           │   Orleans client)│             │                 │
└─────────────┘           └──────────────────┘             │  AccountGrain   │
   :5173                       :5080                        │  …future grains │
                                                            └────────┬────────┘
                                                                     │
                                                            IPersistentState
                                                              (memory → SQL)
```

The API host is an Orleans **client**, not a silo — it talks to grains only through
the interfaces in `grains-abstractions`, never referencing grain implementations.
That client⇄silo seam is the "distributed backend ↔ reactive frontend" boundary
the project is built to exercise.

## Repository layout

| Path                  | Project                     | Role                                                    |
| --------------------- | --------------------------- | ------------------------------------------------------- |
| `grains-abstractions` | `GrainTrade.Abstractions`   | Grain interfaces + shared DTOs (referenced by both host and client) |
| `silo/GrainTrade.Grains` | `GrainTrade.Grains`      | Grain implementations                                   |
| `silo/GrainTrade.Silo`   | `GrainTrade.Silo`        | Orleans silo host (localhost clustering, memory storage) |
| `api-host`            | `GrainTrade.ApiHost`        | ASP.NET Core minimal API + Orleans client               |
| `web`                 | —                           | SvelteKit frontend                                      |

## Prerequisites

- **.NET SDK 9**
- **Node.js ≥ 20.19** (Vite 8 requirement)
- **PostgreSQL** — optional; without it the stack runs fully in memory

## Durable storage (optional)

With no connection string configured, the silo uses localhost clustering and
memory storage, and everything works — state just doesn't survive a restart.

To go durable, create the database and apply Orleans' schema:

```powershell
$psql = "C:\Program Files\PostgreSQL\17\bin\psql.exe"
& $psql -U postgres -h localhost -c "CREATE DATABASE graintrade;"
Get-ChildItem db\orleans\*.sql | Sort-Object Name | ForEach-Object {
    & $psql -U postgres -h localhost -d graintrade -v ON_ERROR_STOP=1 -f $_.FullName
}
```

Then point both hosts at it. The connection string holds a password, so it
lives in user-secrets rather than `appsettings.json`, which is committed:

```powershell
$cs = "Host=localhost;Port=5432;Database=graintrade;Username=postgres;Password=YOURS"
dotnet user-secrets set "ConnectionStrings:Orleans" $cs --project silo/GrainTrade.Silo
dotnet user-secrets set "ConnectionStrings:Orleans" $cs --project api-host
```

Both hosts must agree: they find each other through the same membership table.

Every setting, its shape, and a deploy checklist: [CONFIGURATION.md](CONFIGURATION.md).

What goes where, and why:

| Provider | Store | Rationale |
| --- | --- | --- |
| `accounts` | Postgres | Money — a restart must not lose it |
| `orderbooks` | Postgres | Executed trades are a permanent record |
| `tickers` | Memory | A simulated price is meaningless after a restart; it reseeds deterministically per symbol |
| clustering | Postgres | Real membership table, so a second silo could join |
| streams / `PubSubStore` | Memory | Price ticks are worthless a second later |

`db/orleans/04-*.sql` is ours, not Orleans': the 10.2.2 runtime requires a
`CleanupDefunctSiloEntriesKey` query that none of the schema scripts published
at the `v10.2.2` tag define, so the silo won't start without it.

## Running locally

All three processes, in order, with one command:

```powershell
.\run.ps1              # add -NoWeb for backend only
```

It waits for the silo to report ready before starting the API host (an
Orleans client can't connect to a cluster that isn't up yet), writes per-process
logs to `.logs/`, and stops everything on Ctrl+C.

To run them by hand instead — the order matters:

```sh
# 1. Silo — hosts the grains
dotnet run --project silo/GrainTrade.Silo

# 2. API host — Orleans client + REST, on http://localhost:5080
dotnet run --project api-host --urls http://localhost:5080

# 3. Web — SvelteKit dev server, on http://localhost:5173
cd web && npm install && npm run dev
```

Then open <http://localhost:5173/market>.

## Status

Built as thin vertical slices, each touching both stacks.

- [x] **Slice 1** — `AccountGrain` deposit/withdraw → REST → Svelte account page
- [x] **Slice 2** — `TickerGrain` with a timer-driven random-walk price, polled by `/market`
- [x] **Slice 3** — push price updates live (Orleans Streams → SSE)
- [x] **Slice 4** — market orders: `AccountGrain` settles cash + holdings, `OrderBookGrain` records fills
- [x] **Slice 5** — durable persistence: accounts, order books, and clustering on Postgres

## License

[MIT](LICENSE)
