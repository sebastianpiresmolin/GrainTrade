<script lang="ts">
  import { enhance } from "$app/forms";
  import type { PageProps } from "./$types";

  let { form }: PageProps = $props();
</script>

<section class="login">
  <img src="/logo.png" alt="" width="180" height="180" />
  <h1>GrainTrade</h1>
  <p class="lead">
    Pick a username to start trading. Re-enter it later to return to your
    portfolio.
  </p>

  <form method="POST" use:enhance>
    <input
      name="username"
      placeholder="Username"
      autocomplete="username"
      autocapitalize="off"
      spellcheck="false"
      value={form?.username ?? ""}
    />
    <button type="submit">Enter</button>
  </form>

  {#if form?.error}
    <p class="error" role="alert">{form.error}</p>
  {/if}

  <p class="note">
    No password, it's a demo. Anyone using the same username shares that
    portfolio... bank level security!
  </p>
</section>

<section class="login">
  <img class="orleans_full" src="/orleans_full.png" alt="" />
  <h3 class="note">
    Built for the educational purpose of implementing .NET Orleans and
    demonstrate how grains behave.<br /><br />Click the Orleans logo like the
    one below to read more about how grains work in the specific module.
  </h3>
  <img class="orleans_icon" src="/orleans_icon.png" alt="" />
</section>

<style>
  .login {
    max-width: 22rem;
    margin: 4rem auto;
    text-align: center;
    display: flex;
    flex-direction: column;
    align-items: center;
    gap: 0.4rem;
  }
  h1 {
    margin: 0.25rem 0 0;
    letter-spacing: -0.02em;
  }
  .lead {
    color: var(--muted);
    margin: 0 0 0.75rem;
    font-size: 0.95rem;
  }
  form {
    display: flex;
    gap: 0.5rem;
    width: 100%;
  }
  input {
    flex: 1;
    padding: 0.6rem 0.8rem;
    border: 1px solid var(--border);
    border-radius: 0.6rem;
    font-size: 1rem;
    background: var(--surface);
  }
  button {
    padding: 0.6rem 1.25rem;
    border: 0;
    border-radius: 0.6rem;
    background: var(--brand);
    color: #fff;
    font-weight: 700;
    cursor: pointer;
  }
  button:hover {
    background: var(--brand-dark);
  }
  .error {
    color: var(--down);
    font-size: 0.9rem;
    margin: 0.25rem 0 0;
  }
  .note {
    color: var(--muted);
    font-size: 0.78rem;
    margin-top: 1rem;
  }
  .orleans_full {
    width: 50%;
  }
  .orleans_icon {
    width: 10%;
  }
</style>
