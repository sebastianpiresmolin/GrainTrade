// Dumb demo auth: the "session" is just a username in a cookie, and each username
// deterministically maps to an Orleans account key. No password, no user store —
// re-entering the same username lands you back in the same account. Not secure;
// it exists so people can try the demo with separate portfolios.

export const SESSION_COOKIE = 'gt_user';

// Give a brand-new account something to trade with.
export const STARTING_BALANCE = 100_000;

// username → a stable GUID. A plain (non-cryptographic) hash is fine here — it
// only needs to be deterministic per username, so login and register are the
// same operation.
export function accountIdFor(username: string): string {
	const name = username.trim().toLowerCase();

	let hex = '';
	let h = 2166136261 >>> 0; // FNV-1a offset basis
	for (let round = 0; hex.length < 32; round++) {
		for (let i = 0; i < name.length; i++) {
			h ^= name.charCodeAt(i) + round * 131;
			h = Math.imul(h, 16777619) >>> 0;
		}
		hex += (h >>> 0).toString(16).padStart(8, '0');
	}
	hex = hex.slice(0, 32);

	return `${hex.slice(0, 8)}-${hex.slice(8, 12)}-${hex.slice(12, 16)}-${hex.slice(16, 20)}-${hex.slice(20, 32)}`;
}
