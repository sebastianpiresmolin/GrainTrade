import type { BookDepth, DepthUpdate, TickerQuote, Trade } from '$lib/types';

const MAX_TAPE = 20;

// The single SSE connection for the app. Components read from this module —
// none of them open their own EventSource. The stream carries three named
// events: quote, depth, trade.
class MarketStore {
	quotes = $state<Record<string, TickerQuote>>({});
	depth = $state<Record<string, BookDepth>>({});
	trades = $state<Record<string, Trade[]>>({});
	connected = $state(false);

	#source: EventSource | null = null;
	#refs = 0;

	// Ref-counted so several components can use the feed over one connection,
	// and it closes when the last one goes away.
	connect() {
		this.#refs++;

		if (!this.#source) {
			const source = new EventSource('/api/market/stream');
			this.#source = source;

			source.addEventListener('quote', (e: MessageEvent) => {
				const quote: TickerQuote = JSON.parse(e.data);
				this.quotes[quote.symbol] = quote;
			});

			source.addEventListener('depth', (e: MessageEvent) => {
				const { symbol, bids, asks }: DepthUpdate = JSON.parse(e.data);
				this.depth[symbol] = { bids, asks };
			});

			source.addEventListener('trade', (e: MessageEvent) => {
				this.#recordTrade(JSON.parse(e.data));
			});

			source.onopen = () => (this.connected = true);
			// EventSource reconnects on its own; this just reflects the gap.
			source.onerror = () => (this.connected = false);
		}

		return () => {
			if (--this.#refs === 0) {
				this.#source?.close();
				this.#source = null;
				this.connected = false;
			}
		};
	}

	#recordTrade(trade: Trade) {
		const tape = this.trades[trade.symbol] ?? [];
		if (tape.some((t) => t.tradeId === trade.tradeId)) return;
		this.trades[trade.symbol] = [trade, ...tape].slice(0, MAX_TAPE);
	}

	// Seed from SSR data so the first paint isn't blank. `??=` leaves any newer
	// value the stream already delivered untouched.
	seed(quotes: TickerQuote[]) {
		for (const quote of quotes) this.quotes[quote.symbol] ??= quote;
	}

	seedDepth(symbol: string, depth: BookDepth) {
		this.depth[symbol] ??= depth;
	}

	seedTrades(symbol: string, trades: Trade[]) {
		this.trades[symbol] ??= trades.slice(0, MAX_TAPE);
	}
}

export const market = new MarketStore();
