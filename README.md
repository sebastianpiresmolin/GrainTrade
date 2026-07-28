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

## Running locally

Three processes, started in this order (the client needs the cluster up first):

```sh
# 1. Silo — hosts the grains
dotnet run --project silo/GrainTrade.Silo

# 2. API host — Orleans client + REST, on http://localhost:5080
dotnet run --project api-host --urls http://localhost:5080

# 3. Web — SvelteKit dev server, on http://localhost:5173
cd web && npm install && npm run dev
```

Then open <http://localhost:5173/account>.

## Status

Built as thin vertical slices, each touching both stacks.

- [x] **Slice 1** — `AccountGrain` deposit/withdraw → REST → Svelte account page
- [ ] **Slice 2** — `TickerGrain` with a timer-driven random-walk price
- [ ] **Slice 3** — push price updates live (Orleans Streams + SignalR/SSE)
- [ ] **Slice 4** — `OrderBookGrain` with market-order matching
- [ ] **Slice 5** — durable persistence (swap memory storage for Postgres)

## License

[MIT](LICENSE)
