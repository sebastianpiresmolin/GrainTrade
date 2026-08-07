<script lang="ts">
  import type { GrainInfo } from "$lib/grainInfo";

  let { info }: { info: GrainInfo } = $props();

  let dialog = $state<HTMLDialogElement>();

  function open() {
    dialog?.showModal();
  }
  function close() {
    dialog?.close();
  }

  // Split source into highlighted spans. A tiny hand-rolled C# lexer — enough
  // for the "cool IDE window" look without pulling in a highlighter dependency.
  const KEYWORDS = new Set([
    "public", "private", "sealed", "class", "async", "await", "return", "new",
    "throw", "if", "var", "readonly", "this", "static", "void", "using",
    "namespace", "true", "false", "null",
  ]);

  type Tok = { text: string; kind: string };

  function tokenize(line: string): Tok[] {
    const comment = line.indexOf("//");
    let code = line;
    let trailing: Tok | null = null;
    if (comment >= 0) {
      trailing = { text: line.slice(comment), kind: "comment" };
      code = line.slice(0, comment);
    }

    const toks: Tok[] = [];
    // string literals | identifiers | numbers | everything else (one char).
    const re = /(\$?"(?:[^"\\]|\\.)*")|([A-Za-z_]\w*)|(\d+\.?\d*m?)|(\s+)|([^\s])/g;
    let m: RegExpExecArray | null;
    while ((m = re.exec(code))) {
      if (m[1]) toks.push({ text: m[1], kind: "string" });
      else if (m[2]) {
        const kind = KEYWORDS.has(m[2])
          ? "keyword"
          : /^[A-Z]/.test(m[2])
            ? "type"
            : "ident";
        toks.push({ text: m[2], kind });
      } else if (m[3]) toks.push({ text: m[3], kind: "number" });
      else toks.push({ text: m[0], kind: "plain" });
    }
    if (trailing) toks.push(trailing);
    return toks;
  }

  const lines = $derived(info.code.source.split("\n").map(tokenize));
</script>

<button class="icon" onclick={open} aria-label="How the {info.grain} works" title="How the {info.grain} works">
  <img src="/orleans_icon.png" alt="" width="26" height="26" />
</button>

<dialog bind:this={dialog} onclick={(e) => e.target === dialog && close()}>
  <div class="sheet">
    <header>
      <div>
        <h2>{info.grain}</h2>
        <p class="tagline">{info.tagline}</p>
      </div>
      <button class="x" onclick={close} aria-label="Close">✕</button>
    </header>

    <div class="body">
      {#each info.sections as s (s.heading)}
        <section>
          <h3>{s.heading}</h3>
          <p>{s.body}</p>
        </section>
      {/each}

      <!-- IDE-style code window -->
      <div class="ide">
        <div class="titlebar">
          <span class="dots">
            <i class="r"></i><i class="y"></i><i class="g"></i>
          </span>
          <span class="tab">{info.code.file}</span>
        </div>
        <pre><code>{#each lines as toks, i (i)}<span class="ln">{i + 1}</span>{#each toks as t}<span class={t.kind}>{t.text}</span>{/each}
{/each}</code></pre>
      </div>
    </div>
  </div>
</dialog>

<style>
  .icon {
    border: 0;
    background: transparent;
    padding: 0;
    cursor: pointer;
    line-height: 0;
    border-radius: 6px;
    opacity: 0.85;
    transition:
      opacity 0.12s ease,
      transform 0.12s ease;
  }
  .icon:hover {
    opacity: 1;
    transform: scale(1.08);
  }
  .icon img {
    display: block;
  }

  dialog {
    border: 0;
    background: transparent;
    padding: 0;
    max-width: min(44rem, 94vw);
    width: 100%;
  }
  dialog::backdrop {
    background: rgba(16, 32, 43, 0.55);
    backdrop-filter: blur(2px);
  }

  .sheet {
    background: var(--surface);
    border-radius: var(--radius);
    box-shadow: 0 12px 40px rgba(16, 32, 43, 0.28);
    overflow: hidden;
    max-height: 88vh;
    display: flex;
    flex-direction: column;
  }

  header {
    display: flex;
    align-items: flex-start;
    justify-content: space-between;
    gap: 1rem;
    padding: 1.25rem 1.4rem 0.9rem;
    border-bottom: 1px solid var(--border);
  }
  h2 {
    margin: 0;
    font-size: 1.25rem;
    font-family: "SFMono-Regular", "Consolas", ui-monospace, monospace;
    letter-spacing: -0.01em;
  }
  .tagline {
    margin: 0.25rem 0 0;
    color: var(--muted);
    font-size: 0.88rem;
  }
  .x {
    border: 0;
    background: transparent;
    color: var(--muted);
    font-size: 1rem;
    cursor: pointer;
    padding: 0.2rem 0.4rem;
    border-radius: 6px;
    flex-shrink: 0;
  }
  .x:hover {
    color: var(--text);
    background: var(--surface-2);
  }

  .body {
    padding: 1.1rem 1.4rem 1.4rem;
    overflow-y: auto;
  }
  section {
    margin-bottom: 1.1rem;
  }
  h3 {
    margin: 0 0 0.3rem;
    font-size: 0.72rem;
    text-transform: uppercase;
    letter-spacing: 0.06em;
    color: var(--brand);
  }
  section p {
    margin: 0;
    font-size: 0.92rem;
    line-height: 1.55;
    color: var(--text);
  }

  /* IDE window */
  .ide {
    margin-top: 1.3rem;
    border-radius: 10px;
    overflow: hidden;
    background: #1e2530;
    box-shadow: 0 6px 20px rgba(16, 32, 43, 0.25);
  }
  .titlebar {
    display: flex;
    align-items: center;
    gap: 0.8rem;
    padding: 0.5rem 0.8rem;
    background: #171c25;
    border-bottom: 1px solid #0d1117;
  }
  .dots {
    display: inline-flex;
    gap: 0.4rem;
  }
  .dots i {
    width: 11px;
    height: 11px;
    border-radius: 50%;
    display: block;
  }
  .dots .r {
    background: #ff5f56;
  }
  .dots .y {
    background: #ffbd2e;
  }
  .dots .g {
    background: #27c93f;
  }
  .tab {
    font-family: ui-monospace, "SFMono-Regular", "Consolas", monospace;
    font-size: 0.75rem;
    color: #9aa4b2;
  }
  pre {
    margin: 0;
    padding: 0.9rem 0;
    overflow-x: auto;
    font-family: ui-monospace, "SFMono-Regular", "Consolas", monospace;
    font-size: 0.8rem;
    line-height: 1.6;
  }
  code {
    display: block;
    min-width: max-content;
  }
  .ln {
    display: inline-block;
    width: 2.4rem;
    padding-right: 1rem;
    text-align: right;
    color: #4b5563;
    user-select: none;
  }

  /* token colours — a calm dark palette */
  .keyword {
    color: #c586c0;
  }
  .type {
    color: #4ec9b0;
  }
  .string {
    color: #ce9178;
  }
  .number {
    color: #b5cea8;
  }
  .comment {
    color: #6a9955;
    font-style: italic;
  }
  .ident,
  .plain {
    color: #d4d4d4;
  }

  @media (max-width: 560px) {
    header {
      padding: 1rem 1.1rem 0.8rem;
    }
    .body {
      padding: 1rem 1.1rem 1.2rem;
    }
    pre {
      font-size: 0.72rem;
    }
    .ln {
      width: 1.9rem;
      padding-right: 0.6rem;
    }
  }
</style>
