# Requirements — Email Questions Agent

> **Status: ready for implementation (revision 6).** Every decision below was confirmed by the product owner.
> The smaller defaults (retention periods, a system-wide threshold, Members seeing their own outcomes) were accepted
> along with the recommendations.
>
> This file is the "what" and "why" only. Everything about "how" belongs in the technical design (to be written:
> `.claude/technical-designs/support-email-agent.md`). That covers the mailbox setup, the agent loop, prompts,
> the confidence calculation, schema, endpoints and configuration keys. (The receiving model, IMAP with IDLE plus
> a polling fallback, and the PostgreSQL processing table were decided here: decisions 10 and 11.)
>
> Builds on the current application:
> - organization and individual workspaces, with Owner/Admin/Member roles and one workspace per user
>   ([authentication-and-workspaces.md](./authentication-and-workspaces.md));
> - search and Ask (RAG), which already answers questions only from the workspace's documents, with citations and a
>   "not found" outcome ([rag.md](./rag.md)).
>
> **Revision 2 changes:**
> - one shared CitadelIQ support address instead of one mailbox per organization;
> - only registered portal users are answered, and only from their own organization's documents;
> - the support-email field at onboarding is removed.
>
> **Revision 3:** individual workspaces are included, holding replies are sent, and replies name their sources with
> portal links (all decided). The mail provider must work with a Gmail test mailbox and, in production, a mailbox on
> CitadelIQ's own domain.
>
> **Revision 4:** drafts live only in the portal review queue, reviewers are Owners/Admins, and admins get batched
> draft notifications (decisions 12–14).
>
> **Revision 5:** how a sender's email is matched, sender authentication by aligned SPF/DKIM, an operator-only log
> for untrusted mail, and processing keyed on mailbox ids (decisions 15–18).
>
> **Revision 6:** behaviour above a cap, `DraftsOnly` and failures for individual workspaces, and one system table for
> processing state and the operator log (decisions 17 amended, 19–21). Technical design:
> [support-email-agent.md](../technical-designs/support-email-agent.md).

## Objective

Let signed-up CitadelIQ users ask questions about their organization's documents **by email**. A user sends a
question to CitadelIQ's support address (e.g. `ask@citadeliq.com`). An agent identifies the sender as a registered
user, looks for the answer in **that user's own workspace**, and then:

- **Answer found, and the agent is confident:** it **replies to the user's email** with the answer.
- **No answer, or low confidence:** in an organization, it creates a **draft reply** for the Owners/Admins to
  review, edit and send in a review queue in the portal (decision 4). In an individual workspace, which has no
  reviewer, it replies that no confident answer was found (see "Who is answered").

## Problem statement

Members often need an answer from their organization's documents (a policy, a procedure, a figure) when they aren't
in the portal: on their phone, or in the middle of an email conversation. Ask already answers these questions with
citations, but only inside the portal. Email removes that friction. And when the documents don't answer the
question, the organization's admins get a ready-to-edit draft instead of the question being lost.

## Concepts

- **Support address:** a single email address operated by CitadelIQ, shared by all workspaces. CitadelIQ sets
  it up and authorizes it once; organizations configure nothing.
- **Registered sender:** an authenticated incoming email (decision 16) whose sender address matches the verified
  email of exactly one CitadelIQ user with an **active** membership (decision 15). The sender's workspace is then
  known, since each user has exactly one.
- **Email question:** one incoming message from a registered sender.
- **Auto-reply:** an answer CitadelIQ emails back to the sender on its own.
- **Draft:** a proposed reply that isn't sent. It waits for an Owner/Admin of the sender's organization to review it.
- **Confidence:** the agent's assessment that its answer fully and correctly answers the question from the
  documents. It's compared with a threshold to choose between auto-reply and draft.
- **Email activity:** the record of each processed email and what the agent did with it.

## Users and key scenarios

1. **Find the address.** A signed-in user sees the support address in the portal, with one line explaining it:
   "Email questions about your documents to ask@citadeliq.com from <your email>."
2. **Confident answer.** A Member of Acme emails "What is our travel reimbursement limit?" from their registered
   address. The agent finds the answer in Acme's documents, is confident, and replies to that email.
3. **No answer, or low confidence.** The documents don't cover the question, or only partly. The agent creates a
   draft, and Acme's Owners/Admins see it in their review queue. One of them edits it if needed and sends it; the
   reply goes to the member from the support address, in the same thread.
4. **Unknown sender.** Someone who isn't a registered user, or whose account is closed or still awaiting
   approval, or whose mail fails sender authentication, emails the address. CitadelIQ doesn't reply, creates no
   draft, and records it as ignored in the operator-only log (decision 17), not in any organization's activity.
5. **Not a question.** Out-of-office replies, bounces, newsletters, spam and "thanks!" messages aren't answered and
   produce no draft.
6. **Review activity.** An Owner or Admin opens the email activity list for their organization and sees, for each
   email from their members: when it arrived, the sender, the subject, the outcome (auto-replied / drafted /
   ignored, and why), the confidence and the documents used.

## Functional requirements

### Who is answered
- Only **registered senders** are answered. A registered sender has:
  - a sender address matching, case-insensitively, the verified email of one user (decision 15);
  - an active membership: not closed, not pending approval, not still in onboarding.
- If **no user** or **more than one active user** matches the sender address, the email is ignored and recorded
  as ignored, with no reply and no draft.
- Answers come **only from the sender's own workspace**: the same documents the sender can search in the portal,
  with the same isolation (application filter plus database row-level security).
- Both organization and individual workspaces are covered (decided). Individual workspaces have no reviewer, so
  instead of a draft the user gets a reply saying no confident answer was found in their documents. If there was a
  low-confidence suggestion, the reply includes it, clearly labelled as unverified. This labelled suggestion is the
  only case where a low-confidence answer is sent.
- Mail from unknown senders never gets a reply, not even "you're not registered". This avoids confirming the
  address to spammers and replying to forged senders.

### Processing incoming email
- Every new email that arrives at the support address is processed once, including if it arrives while CitadelIQ
  is restarting.
- The agent first decides whether the email is a question. Anything else (automated messages, auto-replies,
  bounces, mailing lists, empty messages) is ignored and recorded.
- For a question, the agent searches the sender's workspace, possibly several times with reworded queries. It
  writes an answer **only** from what it finds, following the same grounding rule as Ask.
- It then decides:
  - **Auto-reply** only if an answer was found **and** confidence is at or above the threshold.
  - **Draft** in every other case (in an individual workspace: the "no confident answer" reply described under
    "Who is answered").
- The confidence threshold is configurable as a system setting. It isn't set per
  organization in this phase.
- The subject and body are both used as the question. Quoted earlier messages and signatures are ignored as far as
  practical.

### Auto-replies
- Sent **only to the registered sender's address**, as stored in CitadelIQ. Never to a Reply-To header, CC'd
  addresses, or addresses mentioned in the body. Even a forged email can therefore only ever cause an answer to
  go to the real, registered user.
- Threaded to the original email: subject prefixed "Re:", sent from the support address.
- Content: a short answer in plain text based only on the documents.
  - It lists the source document names, each with a link to that document in the portal (decided). The sender can
    already see these documents, and opening a link requires signing in. The portal has no per-document page
    today, so the design decides what the link opens (for example, the document's folder).
  - Attachments and file downloads are never sent.
- A closing line says the reply was generated automatically from the organization's documents, and how to reach a
  person.
- **Follow-ups:** a reply from the user in an already-answered thread is processed as a new
  question, with the thread as conversation history (like Ask follow-ups). It's subject to the same confidence rule
  and limits.

### Drafts
- Drafts live only in a **review queue in the portal**, never in a mailbox. Each organization has its own,
  visible only to its Owners and Admins. Each draft shows:
  - the question;
  - the suggested answer (if any);
  - the reason (no answer found / low confidence);
  - the documents used.
- An Owner/Admin can edit and **send** a draft or **dismiss** it. A sent draft goes to the original sender from the
  support address, in the same thread.
- When a draft is created, the sender gets a short holding reply (decided): "We couldn't answer this from your
  documents automatically; your organization's admins have been notified." It counts toward the per-thread limits.
- Drafts are kept until sent or dismissed, or for a configurable period, after which they expire (default 30 days).

### Draft notifications
- When a draft is created in an organization, its Owners and Admins are told a draft is awaiting review:
  - **In the portal:** a count of pending drafts on the account menu / admin page.
  - **By email:** sent to each Owner/Admin's registered address, from the support address.
- The notification email contains:
  - the asker's display name;
  - the email subject;
  - the reason (no answer found / low confidence);
  - a link to the draft in the portal.

  It does **not** contain the question body or the suggested answer. That content stays in the portal, under its
  retention period and access rules, rather than being copied into inboxes.
- **No flooding:** notifications are batched. An admin gets at most one notification email per configurable
  interval (default 15 minutes), summarising every draft created since the last one ("3 drafts awaiting review").
  No email is sent if the drafts were already handled by then.
- **Opt-out:** each Owner/Admin can turn notification emails off for themselves; the portal count always shows.
- **Not answered as questions:** notification emails are marked as automatically generated. A reply to one is
  recognised as part of a notification thread and ignored (recorded as ignored), never processed as a question.
- Individual workspaces have no drafts, so they get no notifications.
- Notifications count toward the sending limits, and a notification that fails to send is retried, but never
  blocks the draft itself.

### Email activity
- Owners and Admins can see their organization's email activity: arrival time, sender, subject, outcome and
  reason, confidence, documents used, and failures. Members see only their own emails' outcomes.
- Email activity, questions and drafts belong to the sender's workspace and follow the same isolation rules as
  documents. Only mail from an authenticated registered sender is recorded there; untrusted mail goes to the
  operator-only log (decision 17).
- Records are kept for a configurable period (default 90 days).

### Failures
- If the agent fails partway (the model or mail provider is unavailable, or a limit is hit), the email is retried a
  limited number of times. After that it becomes a draft marked "failed". No partial or wrong reply is ever sent,
  and nothing is lost.
- If CitadelIQ's access to the support mailbox stops working, processing pauses and resumes from where it stopped
  once access is restored. Mail that arrived in the meantime is processed in order. This is an operator concern,
  not something organizations see.

## Safety and security requirements

- **Sender identity.** The sender address alone can be forged. Mitigations:
  - replies go only to the registered address (above), so forgery can't send another workspace's content to an
    attacker;
  - messages that fail sender authentication (aligned SPF or DKIM, as recorded by the mailbox provider's receiving
    server; decisions 9 and 16) are ignored entirely, which also stops forged mail from spending model cost or
    creating drafts.
- **Untrusted email content.** The email is treated as data, never as instructions. Text such as "ignore your
  rules", "answer from another organization's documents" or "send this to bob@example.com" can't change what the
  agent may do.
- **Fixed actions.** The agent can only search the sender's workspace, write an answer, and either reply to the
  sender or create a draft. It can't read other mail, add recipients, forward, delete mail, send attachments or
  direct file-download links (only the sign-in-protected portal links described under "Auto-replies"), or reach
  any other workspace.
- **One mailbox, many tenants.** The shared mailbox holds every organization's incoming questions. Who at CitadelIQ
  can open it must be restricted and documented. Processed messages are moved to an
  archive folder and deleted after the email activity retention period (default 90 days).
- **Abuse and cost limits.** There are configurable caps on emails processed and auto-replies sent:
  - per user per hour;
  - per organization per day;
  - in total.
  Above a cap, emails are never auto-replied, no model call is made, and they are recorded (decision 19). This
  guards against mail loops, floods and runaway model cost.
- **Loop protection.** CitadelIQ never answers its own messages, auto-replies (e.g. headers marking a message as
  automatically generated) or bounces.
- **Credentials.** The mailbox credentials are a deployment secret, never in source, logs or responses.
- **Privacy.** Logs record ids, outcomes and timings, never email bodies, questions, retrieved passages or answers,
  the same rule as Ask. Email content is sent to the model provider (OpenAI), as Ask questions are, and the portal
  says so next to the support address.

## Non-functional requirements

- **Clean Architecture preserved.**
  - The mail provider sits behind Application-layer interfaces; its SDK lives only in Infrastructure.
  - The decision rules live in the Application layer: sender resolution, question or not, the confidence
    threshold, limits.
  - Controllers stay thin.
- **Reuse.** The agent builds on the existing search, Ask grounding, context selection, citation validation and
  "not found" handling. It doesn't add a second retrieval path.
- **Workspace context for background work.** Processing runs outside any web request, so it enters the sender's
  workspace explicitly, as document processing already does.
- **Timeliness.** An email is acted on within a few minutes of arriving; the design sets the target.
- **Reliable processing.** Each email gets at most one reply, with no duplicates after restarts or retries, and
  none is skipped. Unlike today's fire-and-forget document processing, email work must survive a restart (see
  decision 10).
- **Configurable.** Thresholds, limits, retries, retention and the reply footer are configuration, not hard-coded.
- **Testability.** The mail provider and the model can be replaced with fakes in tests. Automated tests cover:
  - sender resolution (unknown, closed, pending, duplicate addresses);
  - isolation between workspaces;
  - the auto-reply vs draft decision at the threshold;
  - ignoring automated and non-question mail;
  - recipients never taken from Reply-To, CC or the body;
  - limits;
  - prompt-injection attempts.
- **Kill switch.** A system-wide setting turns off all auto-replies (drafts only) or all processing.

## Out of scope for this phase

- A support mailbox per organization.
- Answering people who aren't registered users.
- Processing attachments or inline images.
- Emails in languages other than the documents' (best effort only).
- Additional sender addresses per user (only the registered email counts).
- Ticketing-system integrations.
- Learning from how admins edit drafts.
- Per-organization prompt customization.
- Analytics dashboards.
- More than one mail provider.

## Decisions

1. **One shared support address** operated by CitadelIQ (decided by the product owner). It's authorized once by
   CitadelIQ's operators, scoped to that single mailbox only.
2. **Only registered senders, only their own workspace** (decided by the product owner). The sender is identified
   by their registered, verified email address, and an ambiguous match is ignored.
3. **Mail provider:** provider-neutral, behind one interface. The mailbox is a Gmail account for testing and,
   in production, a mailbox on CitadelIQ's own domain (decided). The first implementation
   uses standard IMAP (receive) and SMTP (send), which works with Gmail, Google Workspace, Microsoft 365 and most
   hosts. Provider-specific APIs or an inbound-email webhook service can be added later behind the same interface.
4. **Drafts live in a per-organization review queue in the portal,** not in the shared mailbox. A shared mailbox
   would mix every organization's content in one place visible to CitadelIQ staff, and its drafts couldn't be
   restricted to the right organization's admins.
5. **Confidence:** an answer is auto-sent only if:
   - it has valid citations;
   - the retrieved passages clear a similarity floor;
   - the model's own judgment of "fully answered" clears the threshold.
   Any doubt means a draft.
6. **Recipients come from CitadelIQ's user record,** never from the message headers or body.
7. **Questions, answers and drafts are stored in the sender's workspace** (needed for the review queue), under the
   same isolation and with a retention limit.

## Decided after revision 3

8. **Follow-ups** are answered automatically as conversation history. The history comes from CitadelIQ's own
   records of the thread, never from the quoted text in the email. A thread gets at most a configured number of
   automatic replies (default 5). Once an admin has replied in a thread, later messages become drafts. "Thanks"
   messages are ignored.
9. **Sender authentication:** an email is accepted only if SPF or DKIM passes for the same domain as the From
   address (the alignment DMARC checks; decision 16 refines this). The check uses the `Authentication-Results`
   header added by the support mailbox provider's own receiving server (identified by its authserv-id), never a
   header the sender supplied or one added by an earlier hop. Failures are ignored and recorded only in the
   operator-only log (decision 17).
10. **Reliability:** each email's processing state lives in a PostgreSQL table, keyed by its mailbox ids (decision
    18) and worked by a background service in the API. The state is recorded before sending, so a restart never
    causes a duplicate reply or a lost email. This is an exception to design.md §9's "no job queue" for this feature
    only; document processing is unchanged.
11. **Receiving and sending:** IMAP to receive (IDLE, plus a periodic poll as a fallback) and SMTP to send, behind
    one interface. This works with the Gmail test mailbox and the production domain mailbox, and needs no public
    endpoint. An inbound-email webhook service can be added later. The production domain publishes SPF, DKIM and
    DMARC records so CitadelIQ's replies are delivered.

## Decided in revision 4

12. **Drafts never go into a mailbox.** They exist only in the portal's review queue. The support mailbox is read
    and sent from by CitadelIQ alone. Nobody signs in to it day to day; operators only set it up and troubleshoot.
    This replaces the original idea of saving drafts in the support mailbox. A draft that an admin sends goes out
    from the support address in the original thread, like an auto-reply.
13. **Reviewers are the organization's Owners and Admins.** No new role is added.
14. **Admins are notified of new drafts** in the portal and by a batched email (at most one per 15 minutes per
    admin, configurable). The email holds no question or answer content, only a link to the draft. Each admin can
    opt out of the emails; replies to a notification are ignored.

## Decided in revision 5

15. **Email identity.** Users stay identified by (issuer, subject); email is only used to match an incoming sender.
    Email addresses are deliberately not unique in the database: a closed account keeps its row, and re-joining with
    the same email creates a new one (authentication decisions 13/20).
    - Only the `email` claim from Entra's verified sign-up flow is stored as a matchable address. The
      `preferred_username` fallback is dropped, so a user without an `email` claim has no matchable address.
    - Matching is case-insensitive.
    - Only active accounts are candidates (closed, pending and onboarding accounts are excluded), so a re-joined
      user's closed account never causes ambiguity.
    - More than one active match: the email is ignored (no unique index). With one Entra tenant this shouldn't
      happen; a unique constraint is reconsidered when a second identity provider (organization SSO) is added.
    - Dev-login addresses count as verified only in Development.
16. **Aligned SPF or DKIM, with or without a DMARC record.** Mail is accepted when SPF (`smtp.mailfrom`) or DKIM
    (`header.d`) passes for the From domain, computed from the provider's `Authentication-Results` header. The
    security property is DMARC's, but domains that publish no DMARC record (result `none`) aren't dropped. Mail that
    still fails is ignored. The portal line next to the support address says mail must come from a properly
    configured mail domain. A "did you send this? sign in to confirm" email to the registered address is a possible
    later addition, not in this phase.
17. **Where ignored mail is recorded.**
    - Unauthenticated mail and unknown senders go to an **operator-only, system-level log** with no workspace and
      no row-level-security policy: received time, mailbox id, reason, and the sender's **domain** only. No subject,
      body or full address. Nothing from it appears in the portal.
    - Mail from an authenticated registered sender that is ignored (not a question, a reply to a notification, over
      a cap) is recorded in that workspace's email activity as before.
    - The processing table (decision 10) is also system-level, because a row exists before the workspace is known.
      It holds **no email content**. The worker re-fetches the message from the mailbox by its id, and content is
      written only into workspace tables once the sender is resolved. Both are granted to `citadeliq_app`.
    - *Amended in revision 6:* the processing table and the operator log are **one table**. An ignored row carries
      only the reason and sender domain listed above, so the operator log is a filtered view of the processing table
      rather than a copy to keep in sync.
18. **Processing key and duplicate-safe sending.**
    - Processing is keyed on the mailbox's own ids (IMAP UIDVALIDITY + UID), never on Message-ID, which the sender
      controls. Message-ID is kept for threading (`In-Reply-To`/`References`) and as a secondary duplicate check only
      when it is DKIM-signed.
    - Processed mail moves to the archive folder, so the inbox only ever holds unprocessed mail and a UIDVALIDITY
      change means everything in it is new.
    - Before sending, the reply's own Message-ID is generated and stored with the state "sending". On restart,
      anything still "sending" is looked up in the mailbox's Sent folder by that Message-ID before any retry. The
      design must confirm the production provider saves sent mail, or append a copy itself.

## Decided in revision 6

19. **Above a cap** (per user per hour, per organization per day, or in total over a rolling 24 hours): in an
    organization the email becomes a draft with reason "over limit", with **no model call and no holding reply**,
    so a flood costs nothing and sends nothing while admins can still see it. In an individual workspace it is
    recorded as ignored ("over limit") with no reply. Mail is never delayed and answered later.
20. **`DraftsOnly` in individual workspaces:** nothing is sent; the question is recorded as "paused" in the user's
    own email list.
21. **Failures in individual workspaces:** after the retries, the question is shown as "failed" in the user's own
    email list; no email is sent.

## Definition of done

- [ ] Signed-in users see the support address in the portal and how to use it.
- [ ] Each email to the support address is processed once. Registered senders' questions are answered only from
      their own workspace: auto-replied when confident, otherwise drafted (organizations) or answered with a
      "no confident answer" reply (individual workspaces). Unknown senders, closed or pending
      accounts, and non-questions are ignored with no reply.
- [ ] Replies go only to the registered sender's address, never to Reply-To, CC or addresses in the body.
- [ ] Senders are matched case-insensitively against active accounts' `email` claim only, and accepted only with
      aligned SPF or DKIM; untrusted mail appears only in the operator log, never in the portal (tested).
- [ ] Owners/Admins can review, edit, send and dismiss their organization's drafts, and can view email activity.
- [ ] Owners/Admins are notified of new drafts in the portal and by batched email (opt-out per admin), without the
      question or answer in the email.
- [ ] No one can see another organization's drafts or activity.
- [ ] Limits, loop protection and the kill switch are in place, and forged or injection-laden emails can't
      redirect replies or reach other workspaces (tested).
- [ ] Failures never send a partial or wrong reply, and they show up in the activity list.
- [ ] No email bodies, answers or mailbox credentials appear in logs.
- [ ] README and design document describe the support-mailbox setup and the new configuration.
