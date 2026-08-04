import type { TickerQuote } from '$lib/types';

// The single SSE connection for the app. Components read from this module —
// none of them open their own EventSource.
class MarketStore {
	quotes = $state<Record<string, TickerQuote>>({});
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

			source.onmessage = (event) => {
				const quote: TickerQuote = JSON.parse(event.data);
				this.quotes[quote.symbol] = quote;
				this.connected = true;
			};

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

	// Seed from SSR data so the first paint isn't blank.
	seed(quotes: TickerQuote[]) {
		for (const quote of quotes) {
			this.quotes[quote.symbol] ??= quote;
		}
	}
}

export const market = new MarketStore();
