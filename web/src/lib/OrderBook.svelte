<script lang="ts">
	import type { BookDepth, RestingOrder } from '$lib/types';

	let { depth, orders = [] }: { depth: BookDepth; orders?: RestingOrder[] } = $props();

	const money = (n: number) =>
		new Intl.NumberFormat('en-US', { style: 'currency', currency: 'USD' }).format(n);

	// Bar width is relative to the deepest single level on either side.
	const max = $derived(
		Math.max(1, ...depth.bids.map((l) => l.quantity), ...depth.asks.map((l) => l.quantity))
	);
	const pct = (q: number) => `${(q / max) * 100}%`;

	// My resting quantity per price, per side — used to flag pending orders.
	function mineByPrice(side: 'Buy' | 'Sell') {
		const m = new Map<number, number>();
		for (const o of orders) {
			if (o.side === side) m.set(o.limitPrice, (m.get(o.limitPrice) ?? 0) + o.remaining);
		}
		return m;
	}
	const myBids = $derived(mineByPrice('Buy'));
	const myAsks = $derived(mineByPrice('Sell'));

	const empty = $derived(depth.bids.length === 0 && depth.asks.length === 0);
	const hasPending = $derived(orders.length > 0);
</script>

{#if empty}
	<p class="none">No resting orders on the book.</p>
{:else}
	<div class="book">
		<div class="col bids">
			<div class="head"><span>Qty</span><span>Bid</span></div>
			{#each depth.bids as level (level.price)}
				{@const mine = myBids.get(level.price)}
				<div class="row" class:pending={mine}>
					<div class="bar" style:width={pct(level.quantity)}></div>
					<span class="qty">{level.quantity}</span>
					<span class="price-cell">
						<span class="price">{money(level.price)}</span>
						{#if mine}<span class="mine" title="Your pending order: {mine}">●</span>{/if}
					</span>
				</div>
			{/each}
		</div>

		<div class="col asks">
			<div class="head"><span>Ask</span><span>Qty</span></div>
			{#each depth.asks as level (level.price)}
				{@const mine = myAsks.get(level.price)}
				<div class="row" class:pending={mine}>
					<div class="bar" style:width={pct(level.quantity)}></div>
					<span class="price-cell">
						{#if mine}<span class="mine" title="Your pending order: {mine}">●</span>{/if}
						<span class="price">{money(level.price)}</span>
					</span>
					<span class="qty">{level.quantity}</span>
				</div>
			{/each}
		</div>
	</div>

	{#if hasPending}
		<p class="legend"><span class="mine">●</span> your pending order</p>
	{/if}
{/if}

<style>
	.book {
		display: grid;
		grid-template-columns: 1fr 1fr;
		gap: 1px;
		font-size: 0.85rem;
		font-variant-numeric: tabular-nums;
	}
	.head {
		display: flex;
		justify-content: space-between;
		font-size: 0.7rem;
		text-transform: uppercase;
		letter-spacing: 0.05em;
		color: var(--muted);
		padding: 0 0.5rem 0.25rem;
	}
	.row {
		position: relative;
		display: flex;
		justify-content: space-between;
		align-items: center;
		padding: 0.25rem 0.5rem;
		overflow: hidden;
	}
	/* Depth bar sits behind the numbers, anchored toward the spread (center). */
	.bar {
		position: absolute;
		top: 0;
		bottom: 0;
		z-index: 0;
	}
	.row > span {
		position: relative;
		z-index: 1;
	}
	.price-cell {
		display: inline-flex;
		align-items: center;
		gap: 0.3rem;
	}
	.bids .row {
		text-align: right;
	}
	.bids .bar {
		right: 0;
		background: #e7f6ee;
	}
	.bids .price {
		color: var(--up);
		font-weight: 600;
	}
	.asks .bar {
		left: 0;
		background: #fdeceb;
	}
	.asks .price {
		color: var(--down);
		font-weight: 600;
	}
	/* A level where the account has a resting order. */
	.mine {
		color: #e0a419;
		font-size: 0.7rem;
		line-height: 1;
	}
	.row.pending {
		background: rgba(224, 164, 25, 0.08);
	}
	.legend {
		display: flex;
		align-items: center;
		gap: 0.35rem;
		margin: 0.6rem 0 0;
		font-size: 0.72rem;
		color: var(--muted);
	}
	.none {
		font-size: 0.8rem;
		color: var(--muted);
	}
</style>
