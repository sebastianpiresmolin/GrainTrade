import { error } from '@sveltejs/kit';
import type { PageServerLoad } from './$types';
import type { PricePoint, TickerQuote } from '$lib/types';
import { API_BASE } from '$lib/server/api';

export const load: PageServerLoad = async ({ fetch, params }) => {
	const [quoteRes, historyRes] = await Promise.all([
		fetch(`${API_BASE}/market/${params.symbol}`),
		fetch(`${API_BASE}/market/${params.symbol}/history`)
	]);

	if (quoteRes.status === 404) {
		error(404, `Unknown symbol "${params.symbol}".`);
	}
	if (!quoteRes.ok || !historyRes.ok) {
		error(502, 'Failed to load ticker.');
	}

	const quote: TickerQuote = await quoteRes.json();
	const history: PricePoint[] = await historyRes.json();
	return { quote, history };
};
