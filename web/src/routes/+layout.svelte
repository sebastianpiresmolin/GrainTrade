<script lang="ts">
  import { market } from "$lib/market.svelte";
  import type { LayoutProps } from "./$types";

  let { data, children }: LayoutProps = $props();
</script>

<header class="topbar">
  <a class="brand" href="/">
    <img src="/logo.png" alt="" width="100" height="100" />
    <span>GrainTrade</span>
  </a>
  {#if data.username}
    <div class="right">
      <span class="status" class:live={market.connected}>
        <span class="dot"></span><span class="txt"
          >{market.connected ? "Live" : "Offline"}</span
        >
      </span>
      <span class="user">{data.username}</span>
      <form method="POST" action="/logout">
        <button type="submit">Log out</button>
      </form>
    </div>
  {/if}
</header>

<main class="content">
  {@render children()}
</main>

<style>
  :global(:root) {
    --bg: #f4f6f8;
    --surface: #ffffff;
    --surface-2: #f7f9fb;
    --border: #e7eaee;
    --text: #16202b;
    --muted: #737e8b;
    --brand: #0a9d57;
    --brand-dark: #087d45;
    --up: #0a9d57;
    --down: #e03e52;
    --radius: 14px;
    --shadow: 0 1px 2px rgba(16, 32, 43, 0.05), 0 2px 8px rgba(16, 32, 43, 0.04);
  }

  :global(body) {
    margin: 0;
    background: var(--bg);
    color: var(--text);
    font-family:
      "Inter",
      system-ui,
      -apple-system,
      "Segoe UI",
      Roboto,
      sans-serif;
    -webkit-font-smoothing: antialiased;
  }

  :global(*) {
    box-sizing: border-box;
  }

  .topbar {
    position: sticky;
    top: 0;
    z-index: 10;
    display: flex;
    align-items: center;
    justify-content: space-between;
    gap: 0.75rem;
    height: 56px;
    padding: 0 1.25rem;
    background: rgba(255, 255, 255, 0.85);
    backdrop-filter: saturate(1.4) blur(8px);
    border-bottom: 1px solid var(--border);
  }
  .brand {
    display: flex;
    align-items: center;
    gap: 0.55rem;
    min-width: 0;
    text-decoration: none;
    color: var(--text);
    font-weight: 700;
    font-size: 1.05rem;
    letter-spacing: -0.01em;
    white-space: nowrap;
  }
  /* Logo is a large square asset; the bar shows it small. */
  .brand img {
    width: 30px;
    height: 30px;
    object-fit: contain;
  }
  .status {
    display: inline-flex;
    align-items: center;
    gap: 0.4rem;
    font-size: 0.75rem;
    font-weight: 600;
    text-transform: uppercase;
    letter-spacing: 0.04em;
    color: var(--muted);
  }
  .status .dot {
    width: 7px;
    height: 7px;
    border-radius: 50%;
    background: #c2c8d0;
  }
  .status.live {
    color: var(--brand);
  }
  .status.live .dot {
    background: var(--brand);
    box-shadow: 0 0 0 3px rgba(10, 157, 87, 0.15);
  }

  .right {
    display: flex;
    align-items: center;
    gap: 0.9rem;
    min-width: 0;
  }
  .user {
    font-size: 0.85rem;
    font-weight: 600;
    max-width: 9rem;
    overflow: hidden;
    text-overflow: ellipsis;
    white-space: nowrap;
  }
  .right form {
    margin: 0;
  }
  .right button {
    border: 1px solid var(--border);
    background: var(--surface);
    color: var(--muted);
    border-radius: 0.5rem;
    padding: 0.3rem 0.7rem;
    font-size: 0.8rem;
    font-weight: 600;
    white-space: nowrap;
    cursor: pointer;
  }
  .right button:hover {
    color: var(--down);
    border-color: var(--down);
  }

  .content {
    max-width: 40rem;
    margin: 0 auto;
    padding: 1.5rem 1.25rem 4rem;
  }

  @media (max-width: 560px) {
    .topbar {
      padding: 0 0.85rem;
      gap: 0.5rem;
    }
    .right {
      gap: 0.6rem;
    }
    /* Keep the live dot, drop its label to save room. */
    .status .txt {
      display: none;
    }
    .user {
      max-width: 6rem;
    }
    .right button {
      padding: 0.3rem 0.55rem;
    }
    .content {
      padding: 1rem 0.85rem 3rem;
    }
  }
</style>
