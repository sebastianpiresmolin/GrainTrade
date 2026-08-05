<script lang="ts">
	import { untrack } from 'svelte';
	import { enhance } from '$app/forms';
	import type { SubmitFunction } from '@sveltejs/kit';
	import type { PageProps } from './$types';
	import type { AccountSummary, PricePoint } from '$lib/types';
	import { market } from '$lib/market.svelte';
	import OrderBook from '$lib/OrderBook.svelte';

	let { data, form }: PageProps = $props();

	// Live account — background fills push it; SSR fallback until then.
	const account = $derived<AccountSummary>(market.account ?? data.account);
	const position = $derived(account.holdings.find((h) => h.symbol === data.quote.symbol));

	// My live pending orders for this symbol (store holds all symbols).
	const openOrders = $derived(
		(market.orders ?? data.orders).filter((o) => o.symbol === data.quote.symbol)
	);

	$effect(() => {
		market.seed([data.quote]);
		market.seedDepth(data.quote.symbol, data.depth);
		market.seedTrades(data.quote.symbol, data.trades);
		market.seedAccount(data.account, data.orders);
	});
	$effect(() => market.connect());

	// Apply a form action's account immediately, then let the live push refresh.
	const synced: SubmitFunction = () => async ({ update, result }) => {
		await update();
		if (result.type === 'success') {
			const acc = (result.data as { order?: { account: AccountSummary } } | undefined)?.order
				?.account;
			if (acc) market.applyAccount(acc);
		}
	};

	// Live values when the stream has them, SSR fallback until then.
	const quote = $derived(market.quotes[data.quote.symbol] ?? data.quote);
	const depth = $derived(market.depth[data.quote.symbol] ?? data.depth);
	const trades = $derived(market.trades[data.quote.symbol] ?? data.trades);

	// Extend the loaded history with ticks that arrive while we're on the page.
	let live = $state<PricePoint[]>([]);
	$effect(() => {
		const q = market.quotes[data.quote.symbol];
		if (!q) return;
		// Read/write `live` untracked: depending on it here would make the effect
		// retrigger itself (effect_update_depth_exceeded). It should fire only when
		// a new quote arrives, and append each tick once.
		untrack(() => {
			if (live.at(-1)?.asOf === q.asOf) return;
			live = [...live, { price: q.price, asOf: q.asOf }].slice(-120);
		});
	});

	const money = (n: number) =>
		new Intl.NumberFormat('en-US', { style: 'currency', currency: 'USD' }).format(n);

	// Sparkline path, recomputed whenever history changes.
	const path = $derived.by(() => {
		const points = [...data.history, ...live];
		if (points.length < 2) return '';

		const prices = points.map((p) => p.price);
		const min = Math.min(...prices);
		const max = Math.max(...prices);
		const span = max - min || 1;

		return prices
			.map((price, i) => {
				const x = (i / (prices.length - 1)) * 100;
				const y = 30 - ((price - min) / span) * 28;
				return `${i === 0 ? 'M' : 'L'}${x.toFixed(2)},${y.toFixed(2)}`;
			})
			.join(' ');
	});

	const rising = $derived(quote.change >= 0);
</script>

<a class="back" href="/">← Overview</a>

<section class="card head">
	<div class="row">
		<h1>{quote.symbol}</h1>
		<div class="price" class:up={rising} class:down={!rising}>
			{money(quote.price)}
			<span class="change">
				{rising ? '▲' : '▼'}
				{money(Math.abs(quote.change))}
			</span>
		</div>
	</div>

	{#if path}
		<svg class:up={rising} class:down={!rising} viewBox="0 0 100 30" preserveAspectRatio="none" aria-label="Price history">
			<path d={path} fill="none" stroke="currentColor" stroke-width="0.6" />
		</svg>
	{:else}
		<p class="empty">Not enough history yet.</p>
	{/if}

	<p class="asof">As of {new Date(quote.asOf).toLocaleTimeString()}</p>
</section>

<section class="card">
	<div class="wallet">
		<span>Cash <strong>{money(account.cashBalance)}</strong></span>
		{#if position}
			<span>Position <strong>{position.quantity}</strong> <small>avg {money(position.averageCost)}</small></span>
		{/if}
	</div>

	{#if form?.error}
		<p class="msg error" role="alert">{form.error}</p>
	{:else if form?.order}
		<p class="msg filled" role="status">
			{#if form.order.trade.quantity > 0}
				{form.order.trade.side === 'Buy' ? 'Bought' : 'Sold'}
				{form.order.trade.quantity} at {money(form.order.trade.price)}
			{:else}
				Order resting on the book.
			{/if}
		</p>
	{/if}

	<form method="POST" use:enhance={synced} class="market">
		<input name="quantity" type="number" min="1" step="1" value="1" aria-label="Quantity" />
		<button type="submit" formaction="?/buy" class="buy">Buy</button>
		<button type="submit" formaction="?/sell" class="sell" disabled={!position}>Sell</button>
	</form>

	<form method="POST" use:enhance={synced} class="limit">
		<input name="quantity" type="number" min="1" step="1" value="1" aria-label="Limit quantity" />
		<input name="limitPrice" type="number" min="0.01" step="0.01" placeholder="Limit" aria-label="Limit price" />
		<button type="submit" formaction="?/limitBuy" class="buy">Bid</button>
		<button type="submit" formaction="?/limitSell" class="sell" disabled={!position}>Ask</button>
	</form>
</section>

<section class="card">
	<h2>Order book</h2>
	<OrderBook {depth} orders={openOrders} />
</section>

{#if trades.length}
	<section class="card">
		<h2>Recent trades</h2>
		<ul class="trades">
			{#each trades.slice(0, 8) as trade (trade.tradeId)}
				<li>
					<span class:up={trade.side === 'Buy'} class:down={trade.side === 'Sell'}>{trade.side}</span>
					<span>{trade.quantity} @ {money(trade.price)}</span>
					<time>{new Date(trade.executedAt).toLocaleTimeString()}</time>
				</li>
			{/each}
		</ul>
	</section>
{/if}

<style>
	.back {
		display: inline-block;
		margin-bottom: 1rem;
		font-size: 0.85rem;
		color: var(--muted);
		text-decoration: none;
	}
	.back:hover {
		color: var(--brand);
	}
	.card {
		background: var(--surface);
		border: 1px solid var(--border);
		border-radius: var(--radius);
		box-shadow: var(--shadow);
		padding: 1.1rem 1.25rem;
		margin-bottom: 1rem;
	}
	.head .row {
		display: flex;
		align-items: baseline;
		justify-content: space-between;
		gap: 1rem;
	}
	h1 {
		margin: 0;
		font-size: 1.6rem;
		letter-spacing: -0.01em;
	}
	.price {
		font-size: 1.9rem;
		font-weight: 800;
		font-variant-numeric: tabular-nums;
		letter-spacing: -0.02em;
	}
	.change {
		font-size: 0.85rem;
		font-weight: 600;
	}
	.up {
		color: var(--up);
	}
	.down {
		color: var(--down);
	}
	svg {
		width: 100%;
		height: 7rem;
		margin: 1rem 0 0.25rem;
	}
	.empty,
	.asof {
		font-size: 0.78rem;
		color: var(--muted);
		margin: 0.5rem 0 0;
	}
	.wallet {
		display: flex;
		gap: 1.5rem;
		font-size: 0.85rem;
		color: var(--muted);
		margin-bottom: 0.85rem;
	}
	.wallet strong {
		color: var(--text);
	}
	.wallet small {
		color: var(--muted);
	}
	/* Both rows share one grid so the action buttons line up in fixed columns:
	   two flexible input columns, then equal-width primary/secondary buttons. */
	form {
		display: grid;
		grid-template-columns: 1fr 1fr 4.75rem 4.75rem;
		gap: 0.5rem;
	}
	.limit {
		margin-top: 0.5rem;
	}
	/* Market order has no price, so its quantity spans both input columns. */
	.market input {
		grid-column: 1 / 3;
	}
	input {
		width: 100%;
		min-width: 0;
		padding: 0.5rem 0.7rem;
		border: 1px solid var(--border);
		border-radius: 0.5rem;
		font-size: 0.95rem;
		background: var(--surface-2);
	}
	button {
		padding: 0.5rem 0;
		border: 0;
		border-radius: 0.5rem;
		color: #fff;
		font-weight: 700;
		cursor: pointer;
	}
	button:disabled {
		opacity: 0.4;
		cursor: not-allowed;
	}
	.buy {
		background: var(--brand);
	}
	.buy:hover:not(:disabled) {
		background: var(--brand-dark);
	}
	.sell {
		background: var(--down);
	}
	.msg {
		padding: 0.5rem 0.7rem;
		border-radius: 0.5rem;
		font-size: 0.85rem;
		margin: 0 0 0.75rem;
	}
	.error {
		background: #fdeceb;
		color: var(--down);
	}
	.filled {
		background: #e7f6ee;
		color: var(--brand-dark);
	}
	h2 {
		margin: 0 0 0.6rem;
		font-size: 0.75rem;
		text-transform: uppercase;
		letter-spacing: 0.06em;
		color: var(--muted);
	}
	.trades {
		list-style: none;
		padding: 0;
		margin: 0;
		font-size: 0.85rem;
	}
	.trades li {
		display: grid;
		grid-template-columns: 3rem 1fr auto;
		gap: 0.75rem;
		padding: 0.4rem 0;
		border-top: 1px solid var(--border);
		font-variant-numeric: tabular-nums;
	}
	.trades li:first-child {
		border-top: 0;
	}
	.trades span:first-child {
		font-weight: 700;
	}
	time {
		color: var(--muted);
	}

	@media (max-width: 560px) {
		.card {
			padding: 1rem;
		}
		.head .row {
			flex-wrap: wrap;
			gap: 0.25rem 1rem;
		}
		.price {
			font-size: 1.6rem;
		}
		/* Narrower action-button columns so the number inputs keep room. */
		form {
			grid-template-columns: 1fr 1fr 3.4rem 3.4rem;
			gap: 0.4rem;
		}
	}
</style>
