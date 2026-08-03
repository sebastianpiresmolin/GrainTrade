<script lang="ts">
	import type { PageProps } from './$types';

	let { data }: PageProps = $props();

	const money = (n: number) =>
		new Intl.NumberFormat('en-US', { style: 'currency', currency: 'USD' }).format(n);

	// Sparkline path, recomputed whenever history changes.
	const path = $derived.by(() => {
		const points = data.history;
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

	const rising = $derived(data.quote.change >= 0);
</script>

<section class="ticker">
	<a class="back" href="/market">← Market</a>

	<h1>{data.quote.symbol}</h1>

	<div class="price" class:up={rising} class:down={!rising}>
		{money(data.quote.price)}
		<span class="change">
			{rising ? '▲' : '▼'}
			{money(Math.abs(data.quote.change))}
		</span>
	</div>

	{#if path}
		<svg viewBox="0 0 100 30" preserveAspectRatio="none" aria-label="Price history">
			<path d={path} fill="none" stroke={rising ? '#1a7f37' : '#b42318'} stroke-width="0.6" />
		</svg>
	{:else}
		<p class="empty">Not enough history yet.</p>
	{/if}

	<p class="asof">As of {new Date(data.quote.asOf).toLocaleTimeString()}</p>
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
</style>
