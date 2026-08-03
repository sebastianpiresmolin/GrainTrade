<script lang="ts">
	import type { PageProps } from './$types';
	import type { TickerQuote } from '$lib/types';

	let { data }: PageProps = $props();

	// Polled prices, if any have arrived. Falls back to the server-loaded quotes
	// so a client-side navigation re-renders fresh data rather than a snapshot.
	let polled = $state<TickerQuote[] | null>(null);
	let failed = $state(false);

	const quotes = $derived(polled ?? data.quotes);

	const money = (n: number) =>
		new Intl.NumberFormat('en-US', { style: 'currency', currency: 'USD' }).format(n);

	// Polling — Slice 3 swaps this for a push stream. Note what it costs: a
	// fixed interval regardless of whether anything changed, and every client
	// paying for its own round trip.
	$effect(() => {
		const id = setInterval(async () => {
			try {
				const res = await fetch('/api/market');
				if (!res.ok) throw new Error();
				polled = await res.json();
				failed = false;
			} catch {
				failed = true;
			}
		}, 2000);

		return () => clearInterval(id);
	});
</script>

<section class="market">
	<header>
		<h1>Market</h1>
		{#if failed}
			<span class="stale">connection lost — showing last known prices</span>
		{/if}
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
	.stale {
		font-size: 0.8rem;
		color: #b42318;
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
