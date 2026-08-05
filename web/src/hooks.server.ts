import { redirect, type Handle } from '@sveltejs/kit';
import { SESSION_COOKIE, accountIdFor } from '$lib/server/auth';

// Dumb gate: no username cookie ⇒ you can't see anything but /login.
export const handle: Handle = async ({ event, resolve }) => {
	const username = event.cookies.get(SESSION_COOKIE);

	if (username) {
		event.locals.username = username;
		event.locals.accountId = accountIdFor(username);
	} else if (event.url.pathname !== '/login') {
		redirect(303, '/login');
	}

	return resolve(event);
};
