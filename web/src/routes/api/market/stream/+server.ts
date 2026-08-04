import type { RequestHandler } from './$types';
import { API_BASE } from '$lib/server/api';

// Proxies the SSE feed so the browser never needs the API host's address.
// The upstream body is piped through untouched — buffering it here would
// defeat the point of a stream.
export const GET: RequestHandler = async ({ fetch, request }) => {
	const upstream = await fetch(`${API_BASE}/market/stream`, {
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
