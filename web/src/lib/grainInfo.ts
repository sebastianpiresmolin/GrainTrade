// Teaching content for the "how does the grain behind this work" modals.
// Each module (account value, ticker, order book...) has one entry, passed to
// <GrainInfo>. The code is a curated snippet of the real grain, not the whole
// file — if the grain is refactored, update the snippet here too.

export interface GrainInfo {
	// Modal heading, e.g. "AccountGrain".
	grain: string;
	// One-line subtitle under the heading.
	tagline: string;
	// Prose blocks: what the grain is, why it's a grain, what the guarantee buys.
	sections: { heading: string; body: string }[];
	// The IDE-window code sample.
	code: { file: string; source: string };
}

export const accountInfo: GrainInfo = {
	grain: 'AccountGrain',
	tagline: 'One grain per user. It owns that account\'s cash and holdings.',
	sections: [
		{
			heading: 'One grain, one account',
			body: 'The account value above is the state of a single AccountGrain, keyed by your user id. The cluster keeps one activation of your account alive at a time, so every deposit, withdrawal, and fill runs against the same in-memory state. Two requests can never race to read and rewrite the balance.'
		},
		{
			heading: 'Why this is a grain',
			body: 'A balance has everything the actor model is for. It has identity (whose account), state that lives with that identity (cash and positions), and a rule that concurrency must not break: you cannot spend the same dollar twice. Orleans runs one call at a time per grain, so the balance check and the debit that follows it run with nothing slipping in between. No lock, no transaction, no ConcurrentDictionary.'
		},
		{
			heading: 'Durable, written in batches',
			body: 'State lives behind IPersistentState<AccountState> and is saved to Postgres. A whole operation writes once with WriteStateAsync(), so cash and holdings move together. The grain does not hit the database on every field it touches.'
		},
		{
			heading: 'Calls fan out, never back',
			body: 'To place an order the grain asks the TickerGrain for a price, then tells the OrderBookGrain to record the fill. Those calls go outward and never loop back into the same account in the same chain. That keeps the call graph acyclic, which is how single-threaded grains stay deadlock-free.'
		}
	],
	code: {
		file: 'AccountGrain.cs',
		source: `public sealed class AccountGrain : Grain, IAccountGrain
{
    private readonly IPersistentState<AccountState> _state;

    // "accounts" is the storage provider the silo host registers.
    public AccountGrain(
        [PersistentState("account", "accounts")] IPersistentState<AccountState> state,
        TimeProvider time)
    {
        _state = state;
        _time = time;
    }

    public async Task<AccountSummary> Withdraw(decimal amount)
    {
        RequirePositive(amount);

        // No lock needed: Orleans runs one call at a time per grain, so this
        // check and the debit below can't interleave with another request.
        if (amount > _state.State.CashBalance)
        {
            throw new InvalidOperationException(
                $"Insufficient funds: cannot withdraw {Money(amount)}...");
        }

        _state.State.CashBalance -= amount;
        await _state.WriteStateAsync(); // one write per operation
        return ToSummary();
    }
}`
	}
};

export const holdingsInfo: GrainInfo = {
	grain: 'AccountGrain',
	tagline: 'Holdings live in the same grain as your cash. Price and P&L are figured in the browser.',
	sections: [
		{
			heading: 'What the grain actually stores',
			body: 'Each position keeps two numbers: how many shares you hold and the total you paid for them (the cost basis). It does not store an average cost. That figure is derived on read, cost basis divided by quantity, so it can never drift out of sync with the two values it comes from.'
		},
		{
			heading: 'Why selling does not move your average',
			body: 'When you sell, the grain reduces the cost basis by the same fraction as the shares sold. Sell a third of a position and a third of the basis goes with it. Because both sides of the division shrink together, the average cost you see stays the same. Only buying at a new price moves it. This lives in one place, the Apply method, so cash and the position always change in step.'
		},
		{
			heading: 'The grain does not know the price',
			body: 'A holding is worth its live market price, and that changes every second. Baking a price into the grain would mean writing to the account on every tick, for every account, forever. Instead the grain returns quantity and average cost, and the browser multiplies by the price coming off the ticker stream. Value and unrealised P&L are computed on the page and update as quotes arrive, with nothing persisted.'
		},
		{
			heading: 'One writer, plain collections',
			body: 'Positions are a plain Dictionary keyed by symbol, not a ConcurrentDictionary. The grain is the only thing that ever writes them and Orleans runs its calls one at a time, so there is no other writer to guard against. The concurrent collection would be dead weight.'
		}
	],
	code: {
		file: 'AccountGrain.cs',
		source: `private void Apply(Trade trade, PositionState? position)
{
    if (trade.Side == OrderSide.Buy)
    {
        _state.State.CashBalance -= trade.Notional;

        position ??= _state.State.Positions[trade.Symbol] = new PositionState();
        position.Quantity += trade.Quantity;
        position.CostBasis += trade.Notional; // a buy moves the average
        return;
    }

    _state.State.CashBalance += trade.Notional;

    // Selling takes the same slice out of the basis as it does out of the
    // shares, so cost / quantity (the average) is left unchanged.
    position!.CostBasis -= position.CostBasis * trade.Quantity / position.Quantity;
    position.Quantity -= trade.Quantity;

    if (position.Quantity == 0)
        _state.State.Positions.Remove(trade.Symbol);
}

// Average cost is derived on read, never stored: it is CostBasis / Quantity
// straight out of the position when the summary is built.
Holdings = _state.State.Positions
    .OrderBy(p => p.Key)
    .Select(p => new Holding
    {
        Symbol = p.Key,
        Quantity = p.Value.Quantity,
        AverageCost = p.Value.CostBasis / p.Value.Quantity,
    })
    .ToArray();`
	}
};

export const ordersInfo: GrainInfo = {
	grain: 'OrderBookGrain',
	tagline: 'One book per symbol. It matches orders and holds the ones that rest.',
	sections: [
		{
			heading: 'Matching without a lock',
			body: 'Each symbol has its own OrderBookGrain, and it owns every resting bid and ask for that symbol. A new order walks the book best price first, and among equal prices, oldest first. That is price-time priority, the same rule real exchanges use. It holds with no locking because Orleans runs the book one call at a time. A second order cannot start matching while the first is still deciding what it fills against.'
		},
		{
			heading: 'What a pending order is',
			body: 'When your limit order arrives, the book fills whatever it can against the other side. Anything left over is added to the book as a resting order. That leftover is what shows up here as pending. It sits and waits until price comes to it, someone trades through it, you cancel it, or it expires.'
		},
		{
			heading: 'The book never calls you back',
			body: 'A match does not reach into the accounts involved. It writes fills that each account claims for itself later. So the only direction calls ever flow is account to book, never book to account. That one rule keeps the call graph a tree instead of a cycle, which is what stops two grains from waiting on each other forever.'
		},
		{
			heading: 'Expiry uses a reminder, not a timer',
			body: 'Orders expire after ten minutes even if nothing else touches the book. A timer would not do here: it dies when the grain deactivates, and a quiet book deactivates fast, leaving orders resting forever. A reminder is persisted and survives a silo restart, so the sweep always runs. When the book empties, the grain unregisters the reminder so an idle symbol costs nothing.'
		}
	],
	code: {
		file: 'OrderBookGrain.cs',
		source: `public async Task<IReadOnlyList<Fill>> PlaceLimit(RestingOrder order)
{
    var fills = new List<Fill>();
    var remaining = order.Quantity;

    // Best price first, oldest first among equals: price-time priority.
    // No lock — Orleans runs this book one call at a time.
    foreach (var resting in Matching.Candidates(
                 _state.State.Orders, order.Side, order.LimitPrice))
    {
        if (remaining == 0) break;

        var quantity = Math.Min(remaining, resting.Remaining);
        var price = Matching.ExecutionPrice(resting);

        // A fill per side. Accounts claim these later; the book never
        // calls them, so calls only ever go account -> book.
        fills.Add(NewFill(order.OrderId, order.AccountId, order.Side, quantity, price));
        fills.Add(NewFill(resting.OrderId, resting.AccountId, resting.Side, quantity, price));

        Replace(resting with { Remaining = resting.Remaining - quantity });
        remaining -= quantity;
    }

    // Whatever didn't match rests on the book — this is a "pending" order.
    if (remaining > 0)
        _state.State.Orders.Add(order with { Remaining = remaining });

    _state.State.UnclaimedFills.AddRange(fills);
    await _state.WriteStateAsync();

    await EnsureExpirySweep(); // a reminder, so it fires even on a quiet book
    return fills;
}`
	}
};

export const marketInfo: GrainInfo = {
	grain: 'TickerGrain',
	tagline: 'One grain per symbol, walking its own price and pushing every tick to you.',
	sections: [
		{
			heading: 'A price that moves on its own',
			body: 'Each symbol is a TickerGrain that owns its current price. Every two seconds it takes a small random step from the last price, a random walk, and that is the number you watch move in the market list. The randomness is seeded from the symbol, so a given symbol walks the same path each run rather than jumping around unpredictably between restarts.'
		},
		{
			heading: 'A timer, not a reminder',
			body: 'The tick runs on a grain timer. This is the opposite choice from the order book expiry, and on purpose. If a ticker deactivates, dropping a tick or two costs nothing: it starts ticking again the moment someone asks for a quote. There is no event that has to fire, so the cheaper in-memory timer is right. A reminder would add persistence overhead for a guarantee this does not need.'
		},
		{
			heading: 'You are pushed to, not polling',
			body: 'Each new price is published to an Orleans stream. The web host subscribes once and forwards prices to your browser over a live connection, so the numbers change without the page ever asking again. Publishing is treated as a side effect of the price change. The stored price is already correct whether or not the push goes out, so a dropped message never corrupts state.'
		},
		{
			heading: 'It does not save every tick',
			body: 'Writing the price to Postgres every two seconds, for every symbol, would be a lot of writes for a simulated number. Instead the grain persists once every fifteen ticks and flushes whatever is unwritten when it deactivates. A restart resumes near where it left off, and the database is spared the churn.'
		}
	],
	code: {
		file: 'TickerGrain.cs',
		source: `private async Task Tick(CancellationToken cancellationToken)
{
    var price = PriceWalk.Next(_state.State.Price, Volatility, _random);
    var now = _time.GetUtcNow();

    _state.State.PreviousPrice = _state.State.Price;
    _state.State.Price = price;
    _state.State.AsOf = now;
    _state.State.History.Add(new PricePoint { Price = price, AsOf = now });

    // Persist every 15th tick, not every tick: it's a simulated price and
    // losing a few seconds on deactivation is fine. OnDeactivate flushes.
    if (++_ticksSinceWrite >= TicksPerWrite)
    {
        _ticksSinceWrite = 0;
        await _state.WriteStateAsync();
    }

    // A side effect of the change, not the change itself. State above is
    // already correct whether or not this push reaches anyone.
    await _quotes.OnNextAsync(ToQuote());
}`
	}
};

export const tradeFormInfo: GrainInfo = {
	grain: 'AccountGrain',
	tagline: 'Placing a market order: price first, settle the account, then record the trade.',
	sections: [
		{
			heading: 'One method owns the whole order',
			body: 'Buy and Sell run PlaceOrder on your AccountGrain. The whole thing happens inside a single grain turn, so the funds check, the debit, and the new position all commit together. Nothing can see a half-applied order, and no lock is involved, because Orleans is already running this one call to completion before it starts another.'
		},
		{
			heading: 'Price is fetched before anything changes',
			body: 'The first thing the method does is ask the TickerGrain for the current price. That is the only call that leaves the grain, and it is done before a single field is touched. If the ticker is slow or throws, the order fails cleanly with nothing half-written. Fetch first, mutate second, so a failure partway through cannot leave the account inconsistent.'
		},
		{
			heading: 'The account settles itself, the book just records',
			body: 'A market order fills immediately at the quoted price, so the grain updates its own cash and position right away. Only then does it call the OrderBookGrain to record the trade on the public tape. The account is already settled by that point. The book call is a record of what happened, not the thing that makes it happen, which keeps the flow account to book and never back.'
		},
		{
			heading: 'Reserved funds cannot be double spent',
			body: 'The funds check does not compare against your whole balance. It compares against the balance minus cash already reserved for resting limit orders. Money committed to a pending bid is off the table, so a market buy cannot spend the same dollar a resting order is holding.'
		}
	],
	code: {
		file: 'AccountGrain.cs',
		source: `public async Task<OrderResult> PlaceOrder(string symbol, OrderSide side, int quantity)
{
    // Price first: the only call that leaves the grain, done before any
    // mutation, so a slow or failing ticker can't leave half-applied state.
    var quote = await GrainFactory.GetGrain<ITickerGrain>(symbol).GetQuote();
    var price = quote.Price;
    var notional = quantity * price;

    if (side == OrderSide.Buy)
    {
        // Reserved cash backs resting limit orders — spend against what's
        // actually free, not the raw balance.
        var available = _state.State.CashBalance - _state.State.ReservedCash;
        if (notional > available)
            throw new InvalidOperationException("Insufficient funds...");
    }

    var trade = new Trade { /* ... */ Price = price, Quantity = quantity };
    Apply(trade, position);          // cash + position move together
    await _state.WriteStateAsync();  // settled in one write

    // Already settled; the book is just the public record of it.
    await GrainFactory.GetGrain<IOrderBookGrain>(symbol).Record(trade);
    return new OrderResult { Trade = trade, Account = ToSummary() };
}`
	}
};

export const tapeInfo: GrainInfo = {
	grain: 'OrderBookGrain',
	tagline: 'The trade tape is an Orleans stream. You subscribe once instead of polling.',
	sections: [
		{
			heading: 'Every execution is published',
			body: 'When a trade prints, whether from a market order or a limit order matching, the OrderBookGrain pushes it onto an Orleans stream keyed by the symbol. The recent-trades list you see is fed by that stream, so a new print shows up on its own rather than because the page asked again.'
		},
		{
			heading: 'Why a stream and not polling',
			body: 'Polling means every open ticker page hammering the grain on a loop, most calls returning nothing new. A stream flips it around: the grain publishes once when something actually happens, and the web host, subscribed a single time, forwards it to every browser watching that symbol. Work happens when there is news, not on a timer.'
		},
		{
			heading: 'Publishing is a side effect',
			body: 'The trade is written to the book state first, then pushed to the stream. The stored tape is already correct whether or not the push reaches a subscriber, so a dropped message is a missed notification, never lost data. The stream carries updates. It is not the source of truth.'
		},
		{
			heading: 'The grain still keeps a tape',
			body: 'A stream only reaches whoever is subscribed right now. Someone opening the page a second later missed the live push, so the grain also stores the recent trades and hands them over on load. Live updates come off the stream, the starting list comes from state, and together the page is correct whether you were watching or just arrived.'
		}
	],
	code: {
		file: 'OrderBookGrain.cs',
		source: `public override Task OnActivateAsync(CancellationToken cancellationToken)
{
    var symbol = this.GetPrimaryKeyString();
    var provider = this.GetStreamProvider(StreamConstants.Provider);
    // One stream per symbol for depth, one for the trade tape.
    _tradeStream = provider.GetStream<Trade>(StreamConstants.TradeNamespace, symbol);
    return Task.CompletedTask;
}

public async Task Record(Trade trade)
{
    RecordTrade(trade);              // keep it in state for late subscribers
    await _state.WriteStateAsync();

    // Push is a side effect: state above is already correct. A dropped
    // message is a missed notification, not lost data.
    await _tradeStream.OnNextAsync(trade);
}

// Loaded once when a page opens, so it isn't blank until the next trade.
public Task<IReadOnlyList<Trade>> GetRecentTrades() =>
    Task.FromResult<IReadOnlyList<Trade>>(_state.State.Trades.ToArray());`
	}
};
