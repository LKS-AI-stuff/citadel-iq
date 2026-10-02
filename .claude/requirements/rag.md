# Requirements — RAG (Answers from Documents)

> This file is the "what" and "why" only. All "how" — configuration keys, endpoints, event formats,
> prompts, libraries, rate-limiter mechanics, deployment details — lives in the technical design:
> [`.claude/technical-designs/rag.md`](../technical-designs/rag.md).
> Builds on `postgresql-pgvector.md` (requirements and design): chunks, embeddings and similarity search
> are already persistent and working, so the *retrieval* half of RAG exists.

## Objective

Let a user ask a question — and follow-up questions within the same conversation — and get a **short,
professional written answer generated from their own documents**, with **citations** pointing to the
exact documents (and page or sheet, where known) the answer came from. Today CitadelIQ stops at ranked
relevant passages; this phase adds the answer on top, without turning the product into a general-purpose
chatbot and without storing conversations on the server.

## Problem statement

Search finds the right passages, but the user still has to read several of them to find the answer ("How
many vacation days do employees get?" returns five passages, not "15 days"). Users want the answer
itself, and they must be able to trust it — so every statement has to be traceable to a source document.

## Users and key scenarios

There is no authentication yet, so there is a single anonymous kind of user and no server-side notion of a
user session.

1. **Ask a factual question** — "What is the notice period for termination?" → a short answer with numbered
   citations, each linking to a source (document, folder, page or sheet).
2. **Ask something the documents don't cover** — "What's the weather in Paris?" → the product says it
   couldn't find the answer in the user's documents. It must never answer from general knowledge or invent.
3. **Scope the question** — the user limits the answer to the current folder, the folder and its
   subfolders, or the entire portal (the same three scopes as search).
4. **Verify an answer** — the user opens a citation to see the supporting passage and where it came from,
   and can download the original document.
5. **Ask a follow-up** — after "What is the notice period for employees?", the user asks "and for
   contractors?" and gets an answer about contractors, using the earlier turns to understand the question
   while still citing fresh sources.
6. **Start over** — the user can erase the conversation at any time and begin a new session.
7. **See just the passages** — the user can switch to the existing plain ranked search results.

## Functional requirements

### Answering
- Accept a natural-language question and the same scope choice that search uses.
- Find relevant passages using the **existing** search (only fully processed documents, the three scopes,
  and the relevance threshold) — no second search mechanism.
- Generate the answer with an LLM from those passages only, and **stream it** to the screen as it is
  produced so the user sees it start almost immediately.
- Retrieval and generation are separate steps, so either can change independently (model swap, retrieval
  tuning).

### Grounding
- The answer must be based only on the retrieved passages. When the passages don't contain the answer,
  the product declines rather than guesses.
- If nothing relevant is found at all, the product answers "couldn't find an answer in your documents"
  **without calling the LLM** (saves cost and removes any chance of an ungrounded answer).
- Answering may apply a stricter relevance bar than plain search, because a wrong passage leads to a wrong
  answer.

### Answer style
- Professional, neutral and concise **English**: one to three short paragraphs or a short list; no
  filler, no chatter, no speculation. Answers are always in English in this phase.
- Plain text only in this phase (no rich formatting).

### Citations
- Retrieved passages are numbered, and the answer places a numbered marker (e.g. `[1]`) after each
  statement it takes from a passage.
- Markers that don't correspond to a real source are never shown as citations.
- Every answer lists its sources: document name, folder path, page or sheet (when known), relevance score
  and the passage text. Sources the answer actually cited are shown prominently; the others are collapsed
  as "Other passages considered".
- Clicking a citation takes the user to that source; every source offers **download of the original
  document**.
- An answer that cites nothing is flagged to the user as **unverified** (see decisions below), not
  silently trusted.

### Conversation (stateless)
- The conversation lives **only in the user's browser** and is lost on refresh or when cleared; the server
  stores no conversations, questions or answers.
- Each question carries the recent conversation so follow-ups make sense. The history is the **whole
  conversation chain of the session**, including turns where nothing was found.
- A conversation is limited to **10 turns** and **12,000 characters**. The server enforces the limits and
  rejects over-limit requests with a clear message — it never silently shortens a conversation.
- When a conversation reaches the limit, the user sees an alert explaining that the session will be
  reloaded; clicking **OK** clears it and starts a new session. A **New session** button is always
  available to do the same voluntarily.
- A follow-up is first turned into a **standalone question** so the right passages are found; the user is
  shown what was actually searched ("Searched for: …"). With no history the question is used as typed. If
  turning it into a standalone question fails, the original question is used instead of failing.

### Interface
- **Ask is the primary experience** of the existing right-hand panel, which opens on Ask. A "Passages
  only" mode shows today's plain search results, kept as a fallback if the answer service is unavailable,
  as a free and instant option, and unchanged.
- The panel keeps the scope selector and shows loading, streaming, answer, sources, "not found", unverified
  warning, error and "too many requests" states.
- The conversation is a stack of question → answer → sources blocks with a clear **AI-generated, based on
  your documents** label. It must not look like a messaging app (no chat bubbles, avatars or typing
  indicators).
- The UI learns its limits and whether Ask is available from the backend when the portal first loads, so
  the two never drift; if that fails it falls back to the documented defaults.

### Limits and abuse protection
- **Hard input limits** (all adjustable by an administrator, with the defaults below): question length,
  conversation size, and how much passage text is sent to the model.
- **Rate limiting:** a per-client cap on how many questions can be asked per minute and how many answers
  can stream at once, with a friendly "please wait" message when exceeded.
- A switch lets an administrator turn the feature off without redeploying.
- A hard monthly spend limit is configured with the LLM provider.

| Limit | Default |
|---|---|
| Question length | 1,000 characters |
| Conversation | 10 turns / 12,000 characters |
| Passages sent to the model | 8 (maximum 12), up to 8,000 characters |
| Questions per minute | 20 per client |
| Concurrent streams | 2 per client |

### Configuration
- The LLM provider is **OpenAI**, using the existing API key. The default chat model is **`gpt-5.6-luna`**
  (chosen by the product owner), and it must be changeable without code changes. Its availability, price
  and supported settings are to be confirmed against the OpenAI account before building on it.
- Every limit, threshold and switch above is configuration, never hard-coded.

## Non-functional requirements

- **Clean Architecture preserved** — the LLM is reached through an application-level interface with the
  provider's library confined to infrastructure, so the provider can be swapped later. Business logic
  (retrieval orchestration, prompt assembly, citation validation) is not in the web layer. No new tables
  or migrations are introduced for conversations.
- **Safe failures** — provider outages, timeouts, rate limits or malformed responses produce a friendly
  error; no stack traces, provider messages, prompts or keys are exposed. "Passages only" keeps working.
- **Prompt-injection resistance** — document text is untrusted; instructions hidden inside a document must
  not change the system's behaviour.
- **Privacy** — passage text from the user's documents, the recent conversation and the question are sent
  to the LLM provider; nothing is stored on the server. This must be stated in the README and design, and
  only retrieved passages (never whole documents) are sent. Questions, answers, prompts and document
  content are never written to logs; only counts and outcomes are.
- **Latency and cost** — the first words of an answer appear within about a second of generation starting;
  one LLM call for a first question and two for a follow-up; nothing is called when nothing is found;
  generation stops when the user leaves; usage (counts only) is logged.
- **Consistency** — answers are made consistent through strict grounding instructions rather than model
  tuning knobs the target model may not support.
- **Existing behaviour unchanged** — search, uploads, folders and the current UI keep working exactly as today.
- **Testability** — the LLM can be replaced by a fake in tests; the answering rules (no passages → no LLM
  call, limits, citation checking, follow-up handling) are covered by automated tests. Real-model answer
  quality is checked manually.

## Product principles

- CitadelIQ is a professional document-management and search portal, **not a chatbot**. "Ask" is a
  question-and-answer panel with sources. Follow-ups are supported for convenience, but there is no
  persistent chat, no personality and no open-ended conversation: every answer is grounded in the
  documents or declines.
- Trust over fluency: a short grounded answer with sources beats a long confident one.
- No hidden magic: the user can always see exactly which passages an answer was built from.

## Out of scope for this phase

Server-side or persisted conversations (across refreshes or devices), summarising long conversations (at
the limit the session is reset instead), saved or shared answers, answer feedback buttons, authentication
and per-user document permissions, summarising whole documents, agent/tool behaviour, hybrid keyword
search or re-ranking, query rewriting other than the follow-up step above, fine-tuning, understanding
images or charts inside documents, evaluation dashboards, answer caching, persisted usage budgets,
rich-text rendering of answers, and multiple LLM providers at once.

## Decisions made

1. **Provider/model:** OpenAI with the existing key; default model `gpt-5.6-luna`, configurable.
2. **Streaming:** required in this phase.
3. **Context size and answering relevance bar:** configurable, with the defaults above.
4. **Answer format:** professional, concise English, plain text.
5. **Citations:** numbered markers, validated; sources downloadable.
6. **Unanswerable questions:** the model signals "not found" at the very start of its reply, so the user
   never sees an ungrounded answer being written out; if a finished answer still cites nothing, it stays
   visible but is clearly flagged as unverified, with the sources shown. Streamed text is never replaced
   after the fact.
7. **UI:** Ask is primary; plain search stays as "Passages only" and as the fallback.
8. **History:** the whole conversation chain, 10 turns / 12,000 characters, alert-then-reload at the limit,
   New session button, server rejects over-limit requests.
9. **Follow-ups:** rewritten into a standalone question with the same model before searching.
10. **Cost and abuse controls:** hard input limits and rate limiting are in scope; budgets and caching are
    deferred.
11. **Settings:** the UI reads its limits and the on/off state from the backend once at load.

## Definition of done

- [ ] A question returns a grounded answer with numbered citations and a source list showing document,
      folder path, page/sheet (when known), relevance and passage text; each source can be downloaded.
- [ ] A question the documents don't cover returns a clear "couldn't find an answer in your documents"
      message, and the LLM is not called when no passages are found.
- [ ] The answer streams into the UI, starts within about a second, and leaving the panel stops generation.
- [ ] The three search scopes work for questions as they do for search.
- [ ] A follow-up is answered correctly using the conversation so far; the searched standalone question is
      shown; the rewrite is skipped without history and falls back to the original question on failure.
- [ ] The conversation is held only in the browser, limited to 10 turns / 12,000 characters, with the
      alert-then-reload behaviour and a New session button; over-limit and over-length requests are rejected
      with a clear message.
- [ ] Invented citation numbers never appear as valid citations; an answer with no valid citation is
      flagged as unverified.
- [ ] Limits, thresholds, the model name and the on/off switch are configuration; no key, prompt or
      document content appears in source, logs or errors.
- [ ] Rate limiting and the on/off switch behave as configured; provider failures show a safe error and
      "Passages only" still works.
- [ ] The UI loads its limits from the backend once at start-up, shows all required states, labels answers
      as AI-generated, and does not resemble a messaging app.
- [ ] Existing search, upload and folder features and tests are unaffected; new automated tests cover the
      answering rules with a fake LLM; the solution and frontend build cleanly.
- [ ] README, `CLAUDE.md` and the technical design are updated, including the privacy note about sending
      passage text to the LLM provider.
