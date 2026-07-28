<script lang="ts">
	import { enhance } from '$app/forms';
	import type { PageProps } from './$types';

	let { data, form }: PageProps = $props();

	const money = (n: number) =>
		new Intl.NumberFormat('en-US', { style: 'currency', currency: 'USD' }).format(n);
</script>

<section class="account">
	<h1>Account</h1>
	<p class="id">#{data.account.accountId}</p>

	<div class="balance">
		<span class="label">Cash balance</span>
		<span class="value">{money(data.account.cashBalance)}</span>
	</div>

	{#if form?.error}
		<p class="error" role="alert">{form.error}</p>
	{/if}

	<div class="forms">
		<form method="POST" action="?/deposit" use:enhance>
			<label>
				Deposit
				<input name="amount" type="number" min="0" step="0.01" placeholder="0.00" />
			</label>
			<button type="submit">Deposit</button>
		</form>

		<form method="POST" action="?/withdraw" use:enhance>
			<label>
				Withdraw
				<input name="amount" type="number" min="0" step="0.01" placeholder="0.00" />
			</label>
			<button type="submit">Withdraw</button>
		</form>
	</div>
</section>

<style>
	.account {
		max-width: 28rem;
		margin: 3rem auto;
		font-family: system-ui, sans-serif;
	}
	h1 {
		margin: 0 0 0.25rem;
	}
	.id {
		margin: 0 0 1.5rem;
		font-size: 0.8rem;
		color: #888;
		font-family: ui-monospace, monospace;
	}
	.balance {
		display: flex;
		flex-direction: column;
		gap: 0.25rem;
		padding: 1.25rem;
		border: 1px solid #e2e2e2;
		border-radius: 0.75rem;
		margin-bottom: 1.5rem;
	}
	.balance .label {
		font-size: 0.8rem;
		text-transform: uppercase;
		letter-spacing: 0.05em;
		color: #888;
	}
	.balance .value {
		font-size: 2rem;
		font-weight: 700;
		color: #1a7f37;
	}
	.forms {
		display: flex;
		gap: 1rem;
	}
	form {
		display: flex;
		flex-direction: column;
		gap: 0.5rem;
		flex: 1;
	}
	label {
		display: flex;
		flex-direction: column;
		gap: 0.25rem;
		font-size: 0.85rem;
	}
	input {
		padding: 0.5rem;
		border: 1px solid #ccc;
		border-radius: 0.4rem;
		font-size: 1rem;
	}
	button {
		padding: 0.5rem;
		border: 0;
		border-radius: 0.4rem;
		background: #1a7f37;
		color: white;
		font-weight: 600;
		cursor: pointer;
	}
	button:hover {
		background: #156f2f;
	}
	.error {
		padding: 0.6rem 0.75rem;
		border-radius: 0.4rem;
		background: #ffeaea;
		color: #b42318;
		font-size: 0.9rem;
		margin-bottom: 1rem;
	}
</style>
