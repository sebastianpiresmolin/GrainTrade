<script lang="ts">
	import { enhance } from '$app/forms';
	import type { PageProps } from './$types';
	import { market } from '$lib/market.svelte';

	let { data, form }: PageProps = $props();

	let asPercent = $state(true);

	$effect(() => market.seed(data.quotes));
	$effect(() => market.connect());

	const money = (n: number) =>
		new Intl.NumberFormat('en-US', { style: 'currency', currency: 'USD' }).format(n);

	function priceOf(symbol: string, avg: number): number {
		return (
			market.quotes[symbol]?.price ??
			data.quotes.find((q) => q.symbol === symbol)?.price ??
			avg
		);
	}

	const rows = $derived(
		data.account.holdings.map((h) => {
			const price = priceOf(h.symbol, h.averageCost);
			const cost = h.averageCost * h.quantity;
			const pnl = price * h.quantity - cost;
			return { ...h, price, value: price * h.quantity, pnl, pct: cost > 0 ? (pnl / cost) * 100 : 0 };
		})
	);

	const holdingsValue = $derived(rows.reduce((s, r) => s + r.value, 0));
	const totalValue = $derived(data.account.cashBalance + holdingsValue);

	const totals = $derived.by(() => {
		const cost = rows.reduce((s, r) => s + r.averageCost * r.quantity, 0);
		const pnl = rows.reduce((s, r) => s + r.pnl, 0);
		return { pnl, pct: cost > 0 ? (pnl / cost) * 100 : 0 };
	});

	// Live market rows, keeping the server's ordering.
	const marketRows = $derived(data.quotes.map((q) => market.quotes[q.symbol] ?? q));

	function fmtPnl(pnl: number, pct: number): string {
		const sign = pnl >= 0 ? '+' : '−';
		return sign + (asPercent ? `${Math.abs(pct).toFixed(2)}%` : money(Math.abs(pnl)));
	}
</script>

<!-- Account value -->
<section class="card hero">
	<span class="label">Account value</span>
	<span class="big">{money(totalValue)}</span>
	<div class="sub">
		<span>Cash <strong>{money(data.account.cashBalance)}</strong></span>
		{#if rows.length}
			<span class="pnl" class:up={totals.pnl >= 0} class:down={totals.pnl < 0}>
				{fmtPnl(totals.pnl, totals.pct)}
			</span>
		{/if}
	</div>
</section>

<!-- Holdings -->
{#if rows.length}
	<section class="card">
		<div class="card-head">
			<h2>Holdings</h2>
			<div class="seg" data-mode={asPercent ? 'pct' : 'val'}>
				<button type="button" class:active={asPercent} onclick={() => (asPercent = true)}>%</button>
				<button type="button" class:active={!asPercent} onclick={() => (asPercent = false)}>$</button>
			</div>
		</div>
		<ul class="rows">
			{#each rows as h (h.symbol)}
				<li>
					<a class="sym" href="/market/{h.symbol}">{h.symbol}</a>
					<span class="qty">{h.quantity}</span>
					<span class="muted">{money(h.averageCost)}</span>
					<span class="num">{money(h.price)}</span>
					<span class="pnl" class:up={h.pnl >= 0} class:down={h.pnl < 0}>
						{fmtPnl(h.pnl, h.pct)}
					</span>
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
					{q.change > 0 ? '▲' : q.change < 0 ? '▼' : '·'}
					{money(Math.abs(q.change))}
				</span>
			</li>
		{/each}
	</ul>
</section>

<!-- Funds -->
<section class="card funds">
	{#if form?.error}
		<p class="error" role="alert">{form.error}</p>
	{/if}
	<form method="POST" action="?/deposit" use:enhance>
		<input name="amount" type="number" min="0" step="0.01" placeholder="Amount" aria-label="Amount" />
		<button class="ghost" type="submit">Deposit</button>
		<button class="ghost" type="submit" formaction="?/withdraw">Withdraw</button>
	</form>
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
		display: grid;
		grid-template-columns: 4.5rem 2rem 1fr 1fr auto;
		align-items: baseline;
		gap: 0.6rem;
		padding: 0.55rem 0;
		border-top: 1px solid var(--border);
	}
	.rows li:first-child {
		border-top: 0;
	}
	.rows.market li {
		grid-template-columns: 1fr auto auto;
		gap: 1rem;
	}
	.sym {
		color: var(--text);
		font-weight: 700;
		text-decoration: none;
	}
	.sym:hover {
		color: var(--brand);
	}
	.qty {
		color: var(--muted);
	}
	.muted {
		color: var(--muted);
		text-align: right;
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
		content: '';
		position: absolute;
		top: 2px;
		bottom: 2px;
		width: calc(50% - 2px);
		border-radius: 999px;
		background: var(--surface);
		box-shadow: var(--shadow);
		transition: transform 0.15s ease;
	}
	.seg[data-mode='val']::before {
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
