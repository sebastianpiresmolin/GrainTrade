# GrainTrade

A mock commodities-trading platform built on **Microsoft Orleans** (grains,
persistence, timers, reminders, streams) and **SvelteKit** (Svelte 5 runes, load
functions, form actions).

Trading is a natural fit for the actor model. Each account, ticker, and order book
is a grain, and a grain handles one message at a time, so an account's balance can't
be corrupted by concurrent orders and the book keeps price-time priority, both
without a single lock.

Every card in the UI carries an Orleans icon. Click it to read how the grain behind
that card works, with a snippet of the real source.

## Features

- **Accounts**: deposit, withdraw, and hold cash and positions, one `AccountGrain` per user.
- **Live prices**: each `TickerGrain` walks a random-walk price on a timer and pushes ticks over Orleans Streams to the browser.
- **Market orders**: settle against the account immediately, then record on the book.
- **Limit orders**: rest on an `OrderBookGrain`, match by price-time priority, reserve cash or shares while pending, and expire on a reminder.
- **Live account**: background fills update your balance, holdings, and pending orders without a reload.
- **Demo login**: pick a username and you get a portfolio. No password; it exists so people can try the demo with separate accounts.

## Architecture

```
┌─────────────┐   HTTP    ┌──────────────────┐   Orleans   ┌─────────────────┐
│  SvelteKit  │──────────▶│   API host       │   client    │   Silo         │
│  (web)      │◀──────────│  (ASP.NET Core,  │───────────▶│  (grain host)   │
│             │           │   Orleans client)│             │                 │
└─────────────┘           └──────────────────┘             │  AccountGrain   │
   :5173                       :5080                        │  TickerGrain    │
                                                            │  OrderBookGrain │
                                                            └────────┬────────┘
                                                                     │
                                                            IPersistentState
                                                              (memory + SQL)
```

The API host is an Orleans **client**, not a silo. It talks to grains only through
the interfaces in `grains-abstractions` and never references a grain implementation.
Price ticks and order fills reach the browser over Server-Sent Events, bridged from
Orleans Streams in the API host.

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
- **PostgreSQL**: optional. Without it the stack runs fully in memory.

## Durable storage (optional)

With no connection string configured, the silo uses localhost clustering and
memory storage, and everything works. State just doesn't survive a restart.

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
| `accounts` | Postgres | Money. A restart must not lose it |
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

To run them by hand instead, in this order:

```sh
# 1. Silo: hosts the grains
dotnet run --project silo/GrainTrade.Silo

# 2. API host: Orleans client + REST, on http://localhost:5080
dotnet run --project api-host --urls http://localhost:5080

# 3. Web: SvelteKit dev server, on http://localhost:5173
cd web && npm install && npm run dev
```

Then open <http://localhost:5173> and pick a username.

## License

[MIT](LICENSE)
