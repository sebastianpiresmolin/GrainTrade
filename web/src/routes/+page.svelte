<script lang="ts">
  import { enhance } from "$app/forms";
  import type { SubmitFunction } from "@sveltejs/kit";
  import type { PageProps } from "./$types";
  import type { AccountSummary } from "$lib/types";
  import { market } from "$lib/market.svelte";

  let { data, form }: PageProps = $props();

  let asPercent = $state(true);

  // Live account/orders — background fills push these; SSR values until then.
  const account = $derived(market.account ?? data.account);
  const orders = $derived(market.orders ?? data.orders);

  $effect(() => market.seed(data.quotes));
  $effect(() => market.seedAccount(data.account, data.orders));
  $effect(() => market.connect());

  // Apply a form action's account immediately, then let the live push refresh.
  const synced: SubmitFunction = () => async ({ update, result }) => {
    await update();
    if (result.type === "success") {
      const d = result.data as
        | { account?: AccountSummary; order?: { account: AccountSummary } }
        | undefined;
      const acc = d?.account ?? d?.order?.account;
      if (acc) market.applyAccount(acc);
    }
  };

  const money = (n: number) =>
    new Intl.NumberFormat("en-US", {
      style: "currency",
      currency: "USD",
    }).format(n);

  function priceOf(symbol: string, avg: number): number {
    return (
      market.quotes[symbol]?.price ??
      data.quotes.find((q) => q.symbol === symbol)?.price ??
      avg
    );
  }

  const rows = $derived(
    account.holdings.map((h) => {
      const price = priceOf(h.symbol, h.averageCost);
      const cost = h.averageCost * h.quantity;
      const pnl = price * h.quantity - cost;
      return {
        ...h,
        price,
        value: price * h.quantity,
        pnl,
        pct: cost > 0 ? (pnl / cost) * 100 : 0,
      };
    }),
  );

  const holdingsValue = $derived(rows.reduce((s, r) => s + r.value, 0));
  const totalValue = $derived(account.cashBalance + holdingsValue);

  const totals = $derived.by(() => {
    const cost = rows.reduce((s, r) => s + r.averageCost * r.quantity, 0);
    const pnl = rows.reduce((s, r) => s + r.pnl, 0);
    return { pnl, pct: cost > 0 ? (pnl / cost) * 100 : 0 };
  });

  // Live market rows, keeping the server's ordering.
  const marketRows = $derived(
    data.quotes.map((q) => market.quotes[q.symbol] ?? q),
  );

  function fmtPnl(pnl: number, pct: number): string {
    const sign = pnl >= 0 ? "+" : "−";
    return (
      sign + (asPercent ? `${Math.abs(pct).toFixed(2)}%` : money(Math.abs(pnl)))
    );
  }
</script>

<!-- Account value -->
<section class="card hero">
  <span class="label">Account value</span>
  <span class="big">{money(totalValue)}</span>
  <div class="sub">
    <span>Cash <strong>{money(account.cashBalance)}</strong></span>
    {#if rows.length}
      <span class="pnl" class:up={totals.pnl >= 0} class:down={totals.pnl < 0}>
        {fmtPnl(totals.pnl, totals.pct)}
      </span>
    {/if}
  </div>
</section>

<!-- Funds -->
<section class="card funds">
  {#if form?.error}
    <p class="error" role="alert">{form.error}</p>
  {/if}
  <form method="POST" action="?/deposit" use:enhance={synced}>
    <input
      name="amount"
      type="number"
      min="0"
      step="0.01"
      placeholder="Amount"
      aria-label="Amount"
    />
    <button class="ghost" type="submit">Deposit</button>
    <button class="ghost" type="submit" formaction="?/withdraw">Withdraw</button
    >
  </form>
</section>

<!-- Holdings -->
{#if rows.length}
  <section class="card">
    <div class="card-head">
      <h2>Holdings</h2>
      <div class="seg" data-mode={asPercent ? "pct" : "val"}>
        <button
          type="button"
          class:active={asPercent}
          onclick={() => (asPercent = true)}>%</button
        >
        <button
          type="button"
          class:active={!asPercent}
          onclick={() => (asPercent = false)}>$</button
        >
      </div>
    </div>
    <ul class="rows holdings">
      {#each rows as h (h.symbol)}
        <li>
          <div class="left">
            <a class="sym" href="/market/{h.symbol}">{h.symbol}</a>
            <span class="detail">
              {h.quantity} @ {money(h.averageCost)} · {money(h.price)}
            </span>
          </div>
          <div class="right">
            <span class="value">{money(h.value)}</span>
            <span class="pnl" class:up={h.pnl >= 0} class:down={h.pnl < 0}>
              {fmtPnl(h.pnl, h.pct)}
            </span>
          </div>
        </li>
      {/each}
    </ul>
  </section>
{/if}

<!-- Pending orders -->
{#if orders.length}
  <section class="card">
    <div class="card-head">
      <h2><span class="pending-dot">●</span> Pending orders</h2>
    </div>
    <ul class="rows orders">
      {#each orders as o (o.orderId)}
        <li>
          <a class="sym" href="/market/{o.symbol}">{o.symbol}</a>
          <span
            class="side"
            class:up={o.side === "Buy"}
            class:down={o.side === "Sell"}>{o.side}</span
          >
          <span class="detail">{o.remaining} @ {money(o.limitPrice)}</span>
          <span class="num">{money(o.remaining * o.limitPrice)}</span>
          <form method="POST" action="?/cancel" use:enhance={synced}>
            <input type="hidden" name="symbol" value={o.symbol} />
            <input type="hidden" name="orderId" value={o.orderId} />
            <button class="cancel" type="submit" aria-label="Cancel order" title="Cancel order"
              >✕</button
            >
          </form>
        </li>
      {/each}
    </ul>
  </section>
{/if}

<!-- Market -->
<section class="card">
  <div class="card-head"><h2>Market</h2></div>
  <ul class="rows market">
    {#each marketRows as q (q.symbol)}
      <li>
        <a class="sym" href="/market/{q.symbol}">{q.symbol}</a>
        <span class="num">{money(q.price)}</span>
        <span class="chg" class:up={q.change > 0} class:down={q.change < 0}>
          {q.change > 0 ? "▲" : q.change < 0 ? "▼" : "·"}
          {money(Math.abs(q.change))}
        </span>
      </li>
    {/each}
  </ul>
</section>

<style>
  .card {
    background: var(--surface);
    border: 1px solid var(--border);
    border-radius: var(--radius);
    box-shadow: var(--shadow);
    padding: 1.1rem 1.25rem;
    margin-bottom: 1rem;
  }
  .hero {
    display: flex;
    flex-direction: column;
    gap: 0.15rem;
  }
  .hero .label {
    font-size: 0.72rem;
    text-transform: uppercase;
    letter-spacing: 0.06em;
    color: var(--muted);
  }
  .hero .big {
    font-size: 2.4rem;
    font-weight: 800;
    letter-spacing: -0.02em;
    font-variant-numeric: tabular-nums;
  }
  .hero .sub {
    display: flex;
    align-items: center;
    gap: 1rem;
    margin-top: 0.35rem;
    font-size: 0.9rem;
    color: var(--muted);
  }
  .hero .sub strong {
    color: var(--text);
    font-weight: 600;
  }

  .card-head {
    display: flex;
    align-items: center;
    justify-content: space-between;
    margin-bottom: 0.5rem;
  }
  h2 {
    margin: 0;
    font-size: 0.78rem;
    text-transform: uppercase;
    letter-spacing: 0.06em;
    color: var(--muted);
  }

  .rows {
    list-style: none;
    margin: 0;
    padding: 0;
    font-size: 0.9rem;
    font-variant-numeric: tabular-nums;
  }
  .rows li {
    padding: 0.55rem 0;
    border-top: 1px solid var(--border);
  }
  .rows li:first-child {
    border-top: 0;
  }

  /* Holdings: instrument on the left, total value + P&L on the right. */
  .holdings li {
    display: flex;
    align-items: center;
    justify-content: space-between;
    gap: 1rem;
  }
  .holdings .left {
    display: flex;
    flex-direction: column;
    gap: 0.1rem;
    min-width: 0;
  }
  .holdings .detail {
    font-size: 0.78rem;
    color: var(--muted);
  }
  .holdings .right {
    display: flex;
    flex-direction: column;
    align-items: flex-end;
    gap: 0.1rem;
  }
  .holdings .value {
    font-weight: 700;
  }

  /* Pending orders: symbol, side, qty @ price, value, cancel. */
  .orders li {
    display: grid;
    grid-template-columns: 4rem 2.5rem 1fr auto auto;
    gap: 0.6rem;
    align-items: center;
  }
  .orders .side {
    font-weight: 700;
    font-size: 0.85rem;
  }
  .orders .detail {
    color: var(--muted);
  }
  .pending-dot {
    color: #e0a419;
    font-size: 0.7rem;
    margin-right: 0.15rem;
  }
  .cancel {
    border: 0;
    background: transparent;
    color: var(--muted);
    font-size: 0.9rem;
    line-height: 1;
    padding: 0.15rem 0.25rem;
    cursor: pointer;
  }
  .cancel:hover {
    color: var(--down);
  }
  .orders form {
    margin: 0;
    justify-self: end;
  }

  /* Market: symbol, price, change. */
  .market li {
    display: grid;
    grid-template-columns: 1fr auto auto;
    gap: 1rem;
    align-items: baseline;
  }
  .sym {
    color: var(--text);
    font-weight: 700;
    text-decoration: none;
  }
  .sym:hover {
    color: var(--brand);
  }
  .num {
    text-align: right;
    font-weight: 500;
  }
  .pnl {
    text-align: right;
    font-weight: 700;
  }
  .chg {
    text-align: right;
    font-weight: 600;
    min-width: 6rem;
  }
  .up {
    color: var(--up);
  }
  .down {
    color: var(--down);
  }

  /* Sliding %/$ toggle */
  .seg {
    position: relative;
    display: inline-grid;
    grid-template-columns: 1fr 1fr;
    background: var(--surface-2);
    border: 1px solid var(--border);
    border-radius: 999px;
    padding: 2px;
  }
  .seg::before {
    content: "";
    position: absolute;
    top: 2px;
    bottom: 2px;
    width: calc(50% - 2px);
    border-radius: 999px;
    background: var(--surface);
    box-shadow: var(--shadow);
    transition: transform 0.15s ease;
  }
  .seg[data-mode="val"]::before {
    transform: translateX(100%);
  }
  .seg button {
    position: relative;
    z-index: 1;
    border: 0;
    background: transparent;
    padding: 0.1rem 0.7rem;
    font-size: 0.8rem;
    font-weight: 700;
    color: var(--muted);
    cursor: pointer;
  }
  .seg button.active {
    color: var(--text);
  }

  .funds form {
    display: flex;
    gap: 0.5rem;
  }
  .funds input {
    flex: 1;
    padding: 0.5rem 0.7rem;
    border: 1px solid var(--border);
    border-radius: 0.5rem;
    font-size: 0.95rem;
    background: var(--surface-2);
  }
  .ghost {
    padding: 0.5rem 0.9rem;
    border: 1px solid var(--border);
    border-radius: 0.5rem;
    background: var(--surface);
    color: var(--text);
    font-weight: 600;
    cursor: pointer;
  }
  .ghost:hover {
    border-color: var(--brand);
    color: var(--brand);
  }
  .error {
    margin: 0 0 0.6rem;
    padding: 0.5rem 0.7rem;
    border-radius: 0.5rem;
    background: #fdeceb;
    color: var(--down);
    font-size: 0.85rem;
  }
</style>
