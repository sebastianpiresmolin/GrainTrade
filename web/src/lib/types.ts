// DTOs shared with the backend — mirror the C# records in GrainTrade.Abstractions.

export interface AccountSummary {
	accountId: string;
	cashBalance: number;
}
