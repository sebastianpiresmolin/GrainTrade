<script lang="ts">
	import { untrack } from 'svelte';
	import { enhance } from '$app/forms';
	import type { PageProps } from './$types';
	import type { AccountSummary, PricePoint } from '$lib/types';
	import { market } from '$lib/market.svelte';
	import OrderBook from '$lib/OrderBook.svelte';

	let { data, form }: PageProps = $props();

	// The action returns the settled account; fall back to the loaded one.
	const account = $derived<AccountSummary>(form?.order?.account ?? data.account);
	const position = $derived(account.holdings.find((h) => h.symbol === data.quote.symbol));

	$effect(() => {
		market.seed([data.quote]);
		market.seedDepth(data.quote.symbol, data.depth);
		market.seedTrades(data.quote.symbol, data.trades);
	});
	$effect(() => market.connect());

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

<section class="ticker">
	<a class="back" href="/market">← Market</a>

	<h1>{quote.symbol}</h1>

	<div class="price" class:up={rising} class:down={!rising}>
		{money(quote.price)}
		<span class="change">
			{rising ? '▲' : '▼'}
			{money(Math.abs(quote.change))}
		</span>
	</div>

	{#if path}
		<svg viewBox="0 0 100 30" preserveAspectRatio="none" aria-label="Price history">
			<path d={path} fill="none" stroke={rising ? '#1a7f37' : '#b42318'} stroke-width="0.6" />
		</svg>
	{:else}
		<p class="empty">Not enough history yet.</p>
	{/if}

	<p class="asof">As of {new Date(quote.asOf).toLocaleTimeString()}</p>

	<div class="trade">
		<div class="wallet">
			<span>Cash <strong>{money(account.cashBalance)}</strong></span>
			{#if position}
				<span>
					Position <strong>{position.quantity}</strong>
					<small>avg {money(position.averageCost)}</small>
				</span>
			{/if}
		</div>

		{#if form?.error}
			<p class="error" role="alert">{form.error}</p>
		{:else if form?.order}
			<p class="filled" role="status">
				{#if form.order.trade.quantity > 0}
					{form.order.trade.side === 'Buy' ? 'Bought' : 'Sold'}
					{form.order.trade.quantity} at {money(form.order.trade.price)}
				{:else}
					Order resting on the book.
				{/if}
			</p>
		{/if}

		<form method="POST" use:enhance>
			<input name="quantity" type="number" min="1" step="1" value="1" aria-label="Quantity" />
			<button type="submit" formaction="?/buy" class="buy">Buy</button>
			<button type="submit" formaction="?/sell" class="sell" disabled={!position}>Sell</button>
		</form>

		<form method="POST" use:enhance class="limit">
			<input name="quantity" type="number" min="1" step="1" value="1" aria-label="Limit quantity" />
			<input
				name="limitPrice"
				type="number"
				min="0.01"
				step="0.01"
				placeholder="Limit"
				aria-label="Limit price"
			/>
			<button type="submit" formaction="?/limitBuy" class="buy">Bid</button>
			<button type="submit" formaction="?/limitSell" class="sell" disabled={!position}>Ask</button>
		</form>
	</div>

	<h2>Order book</h2>
	<OrderBook {depth} />

	{#if trades.length}
		<h2>Recent trades</h2>
		<ul class="trades">
			{#each trades.slice(0, 8) as trade (trade.tradeId)}
				<li>
					<span class:buy-side={trade.side === 'Buy'} class:sell-side={trade.side === 'Sell'}>
						{trade.side}
					</span>
					<span>{trade.quantity} @ {money(trade.price)}</span>
					<time>{new Date(trade.executedAt).toLocaleTimeString()}</time>
				</li>
			{/each}
		</ul>
	{/if}
</section>

<style>
	.ticker {
		max-width: 34rem;
		margin: 3rem auto;
		font-family: system-ui, sans-serif;
	}
	.back {
		font-size: 0.85rem;
		color: #888;
		text-decoration: none;
	}
	h1 {
		margin: 0.5rem 0 0.25rem;
		font-family: ui-monospace, monospace;
	}
	.price {
		font-size: 2.5rem;
		font-weight: 700;
		font-variant-numeric: tabular-nums;
	}
	.change {
		font-size: 1rem;
		font-weight: 600;
	}
	.up {
		color: #1a7f37;
	}
	.down {
		color: #b42318;
	}
	svg {
		width: 100%;
		height: 8rem;
		margin: 1.5rem 0 0.5rem;
	}
	.empty,
	.asof {
		font-size: 0.8rem;
		color: #888;
	}
	.trade {
		margin-top: 1.5rem;
		padding: 1rem;
		border: 1px solid #e2e2e2;
		border-radius: 0.75rem;
	}
	.wallet {
		display: flex;
		gap: 1.5rem;
		font-size: 0.85rem;
		color: #555;
		margin-bottom: 0.75rem;
	}
	.wallet small {
		color: #888;
	}
	form {
		display: flex;
		gap: 0.5rem;
	}
	.limit {
		margin-top: 0.5rem;
	}
	input {
		flex: 1;
		padding: 0.5rem;
		border: 1px solid #ccc;
		border-radius: 0.4rem;
		font-size: 1rem;
	}
	button {
		padding: 0.5rem 1.25rem;
		border: 0;
		border-radius: 0.4rem;
		color: white;
		font-weight: 600;
		cursor: pointer;
	}
	button:disabled {
		opacity: 0.4;
		cursor: not-allowed;
	}
	.buy {
		background: #1a7f37;
	}
	.sell {
		background: #b42318;
	}
	.error,
	.filled {
		padding: 0.5rem 0.75rem;
		border-radius: 0.4rem;
		font-size: 0.85rem;
		margin: 0 0 0.75rem;
	}
	.error {
		background: #ffeaea;
		color: #b42318;
	}
	.filled {
		background: #e8f5ec;
		color: #1a7f37;
	}
	h2 {
		font-size: 0.75rem;
		text-transform: uppercase;
		letter-spacing: 0.05em;
		color: #888;
		margin: 1.5rem 0 0.5rem;
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
		border-bottom: 1px solid #f0f0f0;
		font-variant-numeric: tabular-nums;
	}
	.buy-side {
		color: #1a7f37;
		font-weight: 600;
	}
	.sell-side {
		color: #b42318;
		font-weight: 600;
	}
	time {
		color: #888;
	}
</style>
