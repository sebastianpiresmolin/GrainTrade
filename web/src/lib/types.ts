// DTOs shared with the backend — mirror the C# records in GrainTrade.Abstractions.

export type OrderSide = 'Buy' | 'Sell';

export interface Holding {
	symbol: string;
	quantity: number;
	averageCost: number;
}

export interface AccountSummary {
	accountId: string;
	cashBalance: number;
	holdings: Holding[];
}

export interface Trade {
	tradeId: string;
	accountId: string;
	symbol: string;
	side: OrderSide;
	quantity: number;
	price: number;
	executedAt: string;
	notional: number;
}

export interface TickerQuote {
	symbol: string;
	price: number;
	change: number;
	asOf: string;
}

export interface PricePoint {
	price: number;
	asOf: string;
}

export interface DepthLevel {
	price: number;
	quantity: number;
}

export interface BookDepth {
	// Best first: bids descending, asks ascending.
	bids: DepthLevel[];
	asks: DepthLevel[];
}

// A depth event off the stream: BookDepth tagged with which book it's for.
export interface DepthUpdate extends BookDepth {
	symbol: string;
}
