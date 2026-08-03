// DTOs shared with the backend — mirror the C# records in GrainTrade.Abstractions.

export interface AccountSummary {
	accountId: string;
	cashBalance: number;
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
