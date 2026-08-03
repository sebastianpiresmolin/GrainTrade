import { json } from '@sveltejs/kit';
import type { RequestHandler } from './$types';
import { API_BASE } from '$lib/server/api';

// Proxies the poll so the browser never needs to know the API host's address.
// Slice 3 replaces this polling with a push stream.
export const GET: RequestHandler = async ({ fetch }) => {
	const res = await fetch(`${API_BASE}/market`);
	if (!res.ok) {
		return json({ error: 'upstream failed' }, { status: 502 });
	}
	return json(await res.json());
};
