import type { PageServerLoad } from './$types';
import type { TickerQuote } from '$lib/types';
import { API_BASE } from '$lib/server/api';

// Server-side load so the table renders with real prices on first paint.
export const load: PageServerLoad = async ({ fetch }) => {
	const res = await fetch(`${API_BASE}/market`);
	if (!res.ok) {
		throw new Error(`Failed to load market (${res.status}).`);
	}
	const quotes: TickerQuote[] = await res.json();
	return { quotes };
};
