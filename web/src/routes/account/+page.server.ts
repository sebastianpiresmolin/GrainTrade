import { fail } from '@sveltejs/kit';
import type { Actions, PageServerLoad } from './$types';
import type { AccountSummary } from '$lib/types';
import { API_BASE, ACCOUNT_ID } from '$lib/server/api';

export const load: PageServerLoad = async ({ fetch }) => {
	const res = await fetch(`${API_BASE}/accounts/${ACCOUNT_ID}`);
	if (!res.ok) {
		throw new Error(`Failed to load account (${res.status}).`);
	}
	const account: AccountSummary = await res.json();
	return { account };
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
