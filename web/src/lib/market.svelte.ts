import type {
	AccountSummary,
	AccountUpdate,
	BookDepth,
	DepthUpdate,
	RestingOrder,
	TickerQuote,
	Trade
} from '$lib/types';

const MAX_TAPE = 20;

// The single SSE connection for the app. Components read from this module —
// none of them open their own EventSource. The stream carries named events:
// quote, depth, trade, account.
class MarketStore {
	quotes = $state<Record<string, TickerQuote>>({});
	depth = $state<Record<string, BookDepth>>({});
	trades = $state<Record<string, Trade[]>>({});
	// The account's live state. Null until seeded from SSR or the first push.
	account = $state<AccountSummary | null>(null);
	orders = $state<RestingOrder[] | null>(null);
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

			source.addEventListener('account', (e: MessageEvent) => {
				const { summary, orders }: AccountUpdate = JSON.parse(e.data);
				this.account = summary;
				this.orders = orders;
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

	// Initial account from SSR; ignored once the live push has taken over.
	seedAccount(summary: AccountSummary, orders: RestingOrder[]) {
		if (this.account === null) {
			this.account = summary;
			this.orders = orders;
		}
	}

	// Apply a foreground action's result immediately so the user's own trades
	// don't wait for the next background push.
	applyAccount(summary: AccountSummary) {
		this.account = summary;
	}
}

export const market = new MarketStore();
