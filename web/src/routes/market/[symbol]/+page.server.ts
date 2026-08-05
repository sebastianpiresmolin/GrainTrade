import { error, fail } from '@sveltejs/kit';
import type { Actions, PageServerLoad } from './$types';
import type {
	AccountSummary,
	BookDepth,
	OrderSide,
	PricePoint,
	RestingOrder,
	TickerQuote,
	Trade
} from '$lib/types';
import { ACCOUNT_ID, API_BASE } from '$lib/server/api';

export const load: PageServerLoad = async ({ fetch, params }) => {
	const [quoteRes, historyRes, tradesRes, depthRes, accountRes, ordersRes] = await Promise.all([
		fetch(`${API_BASE}/market/${params.symbol}`),
		fetch(`${API_BASE}/market/${params.symbol}/history`),
		fetch(`${API_BASE}/market/${params.symbol}/trades`),
		fetch(`${API_BASE}/market/${params.symbol}/depth`),
		fetch(`${API_BASE}/accounts/${ACCOUNT_ID}`),
		fetch(`${API_BASE}/accounts/${ACCOUNT_ID}/orders`)
	]);

	if (quoteRes.status === 404) {
		error(404, `Unknown symbol "${params.symbol}".`);
	}
	if (
		!quoteRes.ok ||
		!historyRes.ok ||
		!tradesRes.ok ||
		!depthRes.ok ||
		!accountRes.ok ||
		!ordersRes.ok
	) {
		error(502, 'Failed to load ticker.');
	}

	const quote: TickerQuote = await quoteRes.json();
	const history: PricePoint[] = await historyRes.json();
	const trades: Trade[] = await tradesRes.json();
	const depth: BookDepth = await depthRes.json();
	const account: AccountSummary = await accountRes.json();

	// The account's own resting orders for this symbol, so the book can flag them.
	const allOrders: RestingOrder[] = await ordersRes.json();
	const orders = allOrders.filter((o) => o.symbol === params.symbol);

	return { quote, history, trades, depth, account, orders };
};

// Client-side checks are a courtesy; the grain owns funds and share counts.
async function order(
	fetch: typeof globalThis.fetch,
	symbol: string,
	side: OrderSide,
	formData: FormData
) {
	const raw = formData.get('quantity');
	const quantity = Number(raw);
	if (raw === null || raw === '' || !Number.isInteger(quantity) || quantity <= 0) {
		return fail(400, { error: 'Enter a whole number of shares greater than zero.' });
	}

	const res = await fetch(`${API_BASE}/accounts/${ACCOUNT_ID}/orders`, {
		method: 'POST',
		headers: { 'content-type': 'application/json' },
		body: JSON.stringify({ symbol, side, quantity })
	});

	if (!res.ok) {
		const problem = await res.json().catch(() => null);
		return fail(res.status, { error: problem?.detail ?? `Order failed (${res.status}).` });
	}

	const result: { trade: Trade; account: AccountSummary } = await res.json();
	return { order: result };
}

// Limit orders rest on the book until they match, so placing one is what puts a
// level into the depth view. Re-running load() after the action refreshes it.
async function limit(
	fetch: typeof globalThis.fetch,
	symbol: string,
	side: OrderSide,
	formData: FormData
) {
	const quantity = Number(formData.get('quantity'));
	const limitPrice = Number(formData.get('limitPrice'));
	if (!Number.isInteger(quantity) || quantity <= 0) {
		return fail(400, { error: 'Enter a whole number of shares greater than zero.' });
	}
	if (!(limitPrice > 0)) {
		return fail(400, { error: 'Enter a limit price greater than zero.' });
	}

	const res = await fetch(`${API_BASE}/accounts/${ACCOUNT_ID}/limit-orders`, {
		method: 'POST',
		headers: { 'content-type': 'application/json' },
		body: JSON.stringify({ symbol, side, quantity, limitPrice })
	});

	if (!res.ok) {
		const problem = await res.json().catch(() => null);
		return fail(res.status, { error: problem?.detail ?? `Order failed (${res.status}).` });
	}

	const result: { trade: Trade; account: AccountSummary } = await res.json();
	return { order: result };
}

export const actions: Actions = {
	buy: async ({ request, fetch, params }) =>
		order(fetch, params.symbol, 'Buy', await request.formData()),
	sell: async ({ request, fetch, params }) =>
		order(fetch, params.symbol, 'Sell', await request.formData()),
	limitBuy: async ({ request, fetch, params }) =>
		limit(fetch, params.symbol, 'Buy', await request.formData()),
	limitSell: async ({ request, fetch, params }) =>
		limit(fetch, params.symbol, 'Sell', await request.formData())
};
