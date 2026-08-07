import type { RequestHandler } from './$types';
import { API_BASE } from '$lib/server/api';

// Proxies the SSE feed so the browser never needs the API host's address.
// The upstream body is piped through untouched — buffering it here would
// defeat the point of a stream.
export const GET: RequestHandler = async ({ fetch, request, locals }) => {
	// Tag the stream with the logged-in account so the API host settles and pushes
	// this user's holdings (market data is shared; the account is per-connection).
	const url = `${API_BASE}/market/stream?account=${locals.accountId ?? ''}`;
	const upstream = await fetch(url, {
		signal: request.signal
	});

	if (!upstream.ok || !upstream.body) {
		return new Response('upstream unavailable', { status: 502 });
	}

	return new Response(upstream.body, {
		headers: {
			'content-type': 'text/event-stream',
			'cache-control': 'no-cache',
			connection: 'keep-alive'
		}
	});
};
