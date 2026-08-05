<script lang="ts">
	import type { BookDepth } from '$lib/types';

	let { depth }: { depth: BookDepth } = $props();

	const money = (n: number) =>
		new Intl.NumberFormat('en-US', { style: 'currency', currency: 'USD' }).format(n);

	// Bar width is relative to the deepest single level on either side.
	const max = $derived(
		Math.max(1, ...depth.bids.map((l) => l.quantity), ...depth.asks.map((l) => l.quantity))
	);
	const pct = (q: number) => `${(q / max) * 100}%`;

	const empty = $derived(depth.bids.length === 0 && depth.asks.length === 0);
</script>

{#if empty}
	<p class="none">No resting orders on the book.</p>
{:else}
	<div class="book">
		<div class="col bids">
			<div class="head"><span>Qty</span><span>Bid</span></div>
			{#each depth.bids as level (level.price)}
				<div class="row">
					<div class="bar" style:width={pct(level.quantity)}></div>
					<span class="qty">{level.quantity}</span>
					<span class="price">{money(level.price)}</span>
				</div>
			{/each}
		</div>

		<div class="col asks">
			<div class="head"><span>Ask</span><span>Qty</span></div>
			{#each depth.asks as level (level.price)}
				<div class="row">
					<div class="bar" style:width={pct(level.quantity)}></div>
					<span class="price">{money(level.price)}</span>
					<span class="qty">{level.quantity}</span>
				</div>
			{/each}
		</div>
	</div>
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
	.none {
		font-size: 0.8rem;
		color: var(--muted);
	}
</style>
