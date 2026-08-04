using GrainTrade.Abstractions;

namespace GrainTrade.ApiHost.Contracts;

public record LimitOrderRequest(string Symbol, OrderSide Side, int Quantity, decimal LimitPrice);
