using GrainTrade.Abstractions;

namespace GrainTrade.ApiHost.Contracts;

public record OrderRequest(string Symbol, OrderSide Side, int Quantity);
