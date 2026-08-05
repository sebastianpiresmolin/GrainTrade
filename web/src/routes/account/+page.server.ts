import { fail } from '@sveltejs/kit';
import type { Actions, PageServerLoad } from './$types';
import type { AccountSummary, TickerQuote } from '$lib/types';
import { API_BASE, ACCOUNT_ID } from '$lib/server/api';

export const load: PageServerLoad = async ({ fetch }) => {
	// Quotes come along so unrealized P&L has prices on first paint; the live
	// stream takes over from there.
	const [accountRes, marketRes] = await Promise.all([
		fetch(`${API_BASE}/accounts/${ACCOUNT_ID}`),
		fetch(`${API_BASE}/market`)
	]);
	if (!accountRes.ok) {
		throw new Error(`Failed to load account (${accountRes.status}).`);
	}
	if (!marketRes.ok) {
		throw new Error(`Failed to load market (${marketRes.status}).`);
	}
	const account: AccountSummary = await accountRes.json();
	const quotes: TickerQuote[] = await marketRes.json();
	return { account, quotes };
};

// Client-side validation is a courtesy; the grain owns the real invariant.
async function mutate(
	fetch: typeof globalThis.fetch,
	op: 'deposit' | 'withdraw',
	formData: FormData
) {
	const raw = formData.get('amount');
	const amount = Number(raw);
	if (raw === null || raw === '' || Number.isNaN(amount) || amount <= 0) {
		return fail(400, { error: 'Enter an amount greater than zero.' });
	}

	const res = await fetch(`${API_BASE}/accounts/${ACCOUNT_ID}/${op}`, {
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
	deposit: async ({ request, fetch }) => mutate(fetch, 'deposit', await request.formData()),
	withdraw: async ({ request, fetch }) => mutate(fetch, 'withdraw', await request.formData())
};
