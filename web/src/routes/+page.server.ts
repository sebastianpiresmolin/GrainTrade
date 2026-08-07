import { fail } from '@sveltejs/kit';
import type { Actions, PageServerLoad } from './$types';
import type { AccountSummary, RestingOrder, TickerQuote } from '$lib/types';
import { API_BASE } from '$lib/server/api';

// The overview: account + market + pending orders in one load so the dashboard
// renders whole on first paint. The live stream keeps prices and P&L moving after.
// locals.accountId is guaranteed by the auth hook (this route requires login).
export const load: PageServerLoad = async ({ fetch, locals }) => {
	const acct = locals.accountId!;
	const [accountRes, marketRes, ordersRes] = await Promise.all([
		fetch(`${API_BASE}/accounts/${acct}`),
		fetch(`${API_BASE}/market`),
		fetch(`${API_BASE}/accounts/${acct}/orders`)
	]);
	if (!accountRes.ok) {
		throw new Error(`Failed to load account (${accountRes.status}).`);
	}
	if (!marketRes.ok) {
		throw new Error(`Failed to load market (${marketRes.status}).`);
	}
	if (!ordersRes.ok) {
		throw new Error(`Failed to load orders (${ordersRes.status}).`);
	}
	const account: AccountSummary = await accountRes.json();
	const quotes: TickerQuote[] = await marketRes.json();
	const orders: RestingOrder[] = await ordersRes.json();
	return { account, quotes, orders };
};

// Client-side validation is a courtesy; the grain owns the real invariant.
async function mutate(
	fetch: typeof globalThis.fetch,
	acct: string,
	op: 'deposit' | 'withdraw',
	formData: FormData
) {
	const raw = formData.get('amount');
	const amount = Number(raw);
	if (raw === null || raw === '' || Number.isNaN(amount) || amount <= 0) {
		return fail(400, { error: 'Enter an amount greater than zero.' });
	}

	const res = await fetch(`${API_BASE}/accounts/${acct}/${op}`, {
		method: 'POST',
		headers: { 'content-type': 'application/json' },
		body: JSON.stringify({ amount })
	});

	if (!res.ok) {
		const problem = await res.json().catch(() => null);
		return fail(res.status, { error: problem?.detail ?? `Request failed (${res.status}).` });
	}

	const account: AccountSummary = await res.json();
	return { account };
}

export const actions: Actions = {
	deposit: async ({ request, fetch, locals }) =>
		mutate(fetch, locals.accountId!, 'deposit', await request.formData()),
	withdraw: async ({ request, fetch, locals }) =>
		mutate(fetch, locals.accountId!, 'withdraw', await request.formData()),

	cancel: async ({ request, fetch, locals }) => {
		const form = await request.formData();
		const symbol = form.get('symbol');
		const orderId = form.get('orderId');
		const res = await fetch(
			`${API_BASE}/accounts/${locals.accountId!}/orders/${symbol}/${orderId}`,
			{ method: 'DELETE' }
		);
		// 404 means it already filled or expired — nothing to do, still refresh.
		if (!res.ok && res.status !== 404) {
			return fail(res.status, { error: 'Could not cancel the order.' });
		}
		return { cancelled: true };
	}
};
