<script lang="ts">
	import type { PageProps } from './$types';
	import { market } from '$lib/market.svelte';

	let { data }: PageProps = $props();

	$effect(() => {
		market.seed(data.quotes);
	});

	// Preserve the server's ordering; the store is keyed by symbol.
	const quotes = $derived(data.quotes.map((q) => market.quotes[q.symbol] ?? q));

	const money = (n: number) =>
		new Intl.NumberFormat('en-US', { style: 'currency', currency: 'USD' }).format(n);

	$effect(() => market.connect());
</script>

<section class="market">
	<header>
		<h1>Market</h1>
		<span class="status" class:live={market.connected}>
			{market.connected ? 'live' : 'connecting…'}
		</span>
	</header>

	<table>
		<thead>
			<tr>
				<th>Symbol</th>
				<th class="num">Price</th>
				<th class="num">Change</th>
			</tr>
		</thead>
		<tbody>
			{#each quotes as quote (quote.symbol)}
				<tr>
					<td><a href="/market/{quote.symbol}">{quote.symbol}</a></td>
					<td class="num">{money(quote.price)}</td>
					<td class="num" class:up={quote.change > 0} class:down={quote.change < 0}>
						{quote.change > 0 ? '▲' : quote.change < 0 ? '▼' : '·'}
						{money(Math.abs(quote.change))}
					</td>
				</tr>
			{/each}
		</tbody>
	</table>
</section>

<style>
	.market {
		max-width: 34rem;
		margin: 3rem auto;
		font-family: system-ui, sans-serif;
	}
	header {
		display: flex;
		align-items: baseline;
		gap: 0.75rem;
		margin-bottom: 1.5rem;
	}
	h1 {
		margin: 0;
	}
	.status {
		font-size: 0.75rem;
		text-transform: uppercase;
		letter-spacing: 0.05em;
		color: #b42318;
	}
	.status.live {
		color: #1a7f37;
	}
	table {
		width: 100%;
		border-collapse: collapse;
	}
	th,
	td {
		padding: 0.6rem 0.5rem;
		border-bottom: 1px solid #e2e2e2;
		text-align: left;
	}
	th {
		font-size: 0.75rem;
		text-transform: uppercase;
		letter-spacing: 0.05em;
		color: #888;
	}
	.num {
		text-align: right;
		font-variant-numeric: tabular-nums;
	}
	td.num {
		font-family: ui-monospace, monospace;
	}
	a {
		color: #1a7f37;
		font-weight: 600;
		text-decoration: none;
	}
	a:hover {
		text-decoration: underline;
	}
	.up {
		color: #1a7f37;
	}
	.down {
		color: #b42318;
	}
</style>
