<script lang="ts">
	import { enhance } from '$app/forms';
	import type { PageProps } from './$types';
	import { market } from '$lib/market.svelte';

	let { data, form }: PageProps = $props();

	// P&L shows as a percentage by default; the toggle flips it to a cash value.
	let asPercent = $state(true);

	$effect(() => market.seed(data.quotes));
	$effect(() => market.connect());

	const money = (n: number) =>
		new Intl.NumberFormat('en-US', { style: 'currency', currency: 'USD' }).format(n);

	// Live price for a held symbol, falling back to the SSR quote, then to the
	// average cost (so a symbol without a quote reads as flat, not nonsense).
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
			return { ...h, price, pnl, pct: cost > 0 ? (pnl / cost) * 100 : 0 };
		})
	);

	const totals = $derived.by(() => {
		const cost = rows.reduce((s, r) => s + r.averageCost * r.quantity, 0);
		const pnl = rows.reduce((s, r) => s + r.pnl, 0);
		return { pnl, pct: cost > 0 ? (pnl / cost) * 100 : 0 };
	});

	// One P&L value formatted per the toggle, with an explicit sign.
	function fmtPnl(pnl: number, pct: number): string {
		const sign = pnl >= 0 ? '+' : '−';
		return sign + (asPercent ? `${Math.abs(pct).toFixed(2)}%` : money(Math.abs(pnl)));
	}
</script>

<section class="account">
	<h1>Account</h1>
	<p class="id">#{data.account.accountId}</p>

	<div class="balance">
		<span class="label">Cash balance</span>
		<span class="value">{money(data.account.cashBalance)}</span>
	</div>

	{#if rows.length}
		<div class="pnl-head">
			<span class="label">Unrealized P&amp;L</span>

			<!-- Sliding toggle: % (default) ⇄ cash value. -->
			<div class="seg" data-mode={asPercent ? 'pct' : 'val'}>
				<button type="button" class:active={asPercent} onclick={() => (asPercent = true)}>%</button>
				<button type="button" class:active={!asPercent} onclick={() => (asPercent = false)}>$</button>
			</div>

			<span class="total" class:up={totals.pnl >= 0} class:down={totals.pnl < 0}>
				{fmtPnl(totals.pnl, totals.pct)}
			</span>
		</div>

		<ul class="holdings">
			{#each rows as h (h.symbol)}
				<li>
					<a href="/market/{h.symbol}">{h.symbol}</a>
					<span class="qty">{h.quantity}</span>
					<span class="cost">avg {money(h.averageCost)}</span>
					<span class="price">{money(h.price)}</span>
					<span class="pnl" class:up={h.pnl >= 0} class:down={h.pnl < 0}>
						{fmtPnl(h.pnl, h.pct)}
					</span>
				</li>
			{/each}
		</ul>
	{/if}

	{#if form?.error}
		<p class="error" role="alert">{form.error}</p>
	{/if}

	<div class="forms">
		<form method="POST" action="?/deposit" use:enhance>
			<label>
				Deposit
				<input name="amount" type="number" min="0" step="0.01" placeholder="0.00" />
			</label>
			<button type="submit">Deposit</button>
		</form>

		<form method="POST" action="?/withdraw" use:enhance>
			<label>
				Withdraw
				<input name="amount" type="number" min="0" step="0.01" placeholder="0.00" />
			</label>
			<button type="submit">Withdraw</button>
		</form>
	</div>
</section>

<style>
	.account {
		max-width: 32rem;
		margin: 3rem auto;
		font-family: system-ui, sans-serif;
	}
	h1 {
		margin: 0 0 0.25rem;
	}
	.id {
		margin: 0 0 1.5rem;
		font-size: 0.8rem;
		color: #888;
		font-family: ui-monospace, monospace;
	}
	.balance {
		display: flex;
		flex-direction: column;
		gap: 0.25rem;
		padding: 1.25rem;
		border: 1px solid #e2e2e2;
		border-radius: 0.75rem;
		margin-bottom: 1.5rem;
	}
	.balance .label {
		font-size: 0.8rem;
		text-transform: uppercase;
		letter-spacing: 0.05em;
		color: #888;
	}
	.balance .value {
		font-size: 2rem;
		font-weight: 700;
		color: #1a7f37;
	}
	.pnl-head {
		display: flex;
		align-items: center;
		gap: 0.75rem;
		margin-bottom: 0.5rem;
	}
	.pnl-head .label {
		font-size: 0.75rem;
		text-transform: uppercase;
		letter-spacing: 0.05em;
		color: #888;
	}
	.pnl-head .total {
		margin-left: auto;
		font-weight: 700;
		font-variant-numeric: tabular-nums;
	}
	/* Segmented sliding toggle. */
	.seg {
		position: relative;
		display: inline-grid;
		grid-template-columns: 1fr 1fr;
		background: #f0f0f0;
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
		background: #fff;
		box-shadow: 0 1px 2px rgba(0, 0, 0, 0.15);
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
		padding: 0.15rem 0.6rem;
		font-size: 0.85rem;
		font-weight: 600;
		color: #888;
		cursor: pointer;
	}
	.seg button.active {
		color: #1a1a1a;
	}
	.holdings {
		list-style: none;
		padding: 0;
		margin: 0 0 1.5rem;
		font-size: 0.85rem;
	}
	.holdings li {
		display: grid;
		grid-template-columns: 4.5rem 2rem 1fr 1fr auto;
		align-items: baseline;
		gap: 0.6rem;
		padding: 0.5rem 0;
		border-bottom: 1px solid #f0f0f0;
		font-variant-numeric: tabular-nums;
	}
	.holdings a {
		color: #1a7f37;
		font-weight: 600;
		text-decoration: none;
	}
	.cost {
		color: #888;
		text-align: right;
	}
	.price {
		text-align: right;
	}
	.pnl {
		text-align: right;
		font-weight: 600;
	}
	.up {
		color: #1a7f37;
	}
	.down {
		color: #b42318;
	}
	.forms {
		display: flex;
		gap: 1rem;
	}
	form {
		display: flex;
		flex-direction: column;
		gap: 0.5rem;
		flex: 1;
	}
	label {
		display: flex;
		flex-direction: column;
		gap: 0.25rem;
		font-size: 0.85rem;
	}
	input {
		padding: 0.5rem;
		border: 1px solid #ccc;
		border-radius: 0.4rem;
		font-size: 1rem;
	}
	button[type='submit'] {
		padding: 0.5rem;
		border: 0;
		border-radius: 0.4rem;
		background: #1a7f37;
		color: white;
		font-weight: 600;
		cursor: pointer;
	}
	button[type='submit']:hover {
		background: #156f2f;
	}
	.error {
		padding: 0.6rem 0.75rem;
		border-radius: 0.4rem;
		background: #ffeaea;
		color: #b42318;
		font-size: 0.9rem;
		margin-bottom: 1rem;
	}
</style>
