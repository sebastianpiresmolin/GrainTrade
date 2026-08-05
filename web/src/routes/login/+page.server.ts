import { fail, redirect } from '@sveltejs/kit';
import type { Actions } from './$types';
import { SESSION_COOKIE, STARTING_BALANCE, accountIdFor } from '$lib/server/auth';
import { API_BASE } from '$lib/server/api';

export const actions: Actions = {
	default: async ({ request, cookies, fetch }) => {
		const form = await request.formData();
		const username = String(form.get('username') ?? '').trim();
		if (username.length < 2 || username.length > 24) {
			return fail(400, { error: 'Pick a username between 2 and 24 characters.', username });
		}

		// Fund a brand-new account so there's something to trade with. A returning
		// user (already has cash or holdings) is left alone.
		const accountId = accountIdFor(username);
		const res = await fetch(`${API_BASE}/accounts/${accountId}`);
		if (res.ok) {
			const account = await res.json();
			if (account.cashBalance === 0 && (account.holdings?.length ?? 0) === 0) {
				await fetch(`${API_BASE}/accounts/${accountId}/deposit`, {
					method: 'POST',
					headers: { 'content-type': 'application/json' },
					body: JSON.stringify({ amount: STARTING_BALANCE })
				});
			}
		}

		cookies.set(SESSION_COOKIE, username, {
			path: '/',
			httpOnly: true,
			sameSite: 'lax',
			maxAge: 60 * 60 * 24 * 30
		});
		redirect(303, '/');
	}
};
