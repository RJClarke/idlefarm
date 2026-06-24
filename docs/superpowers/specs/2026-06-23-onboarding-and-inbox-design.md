# Early-Game Onboarding + Inbox — Design

**Date:** 2026-06-23
**Branch context:** authored on `feat/run-ender-economy`
**Status:** Design — awaiting plan

## Goal

Introduce a system for **once-off narrative moments** in the game, and deliver the
first two consumers of it:

1. **Early-game onboarding** — on the very first launch, a popup asks the player to
   name their farm. The name becomes their account name, shown and editable in Settings.
2. **A robust Inbox / mailbox** — townspeople send the player "letters" triggered by
   game events (e.g. unlocking a new building type). Built to last: rewards, navigation
   call-to-actions, sender identities, and a clean delivery API so future systems can
   drop letters in.

Coach-mark / tutorial overlays are an explicit **non-goal for this spec** — deferred to
a future spec (Spec B). The foundation reserves a payload kind for them so they slot in
later without rework.

## The unifying idea: one-shot narrative beats

First-run naming, inbox letters, and (later) tutorials are the same underlying thing:
something happens, we check "has this already fired?", and if not we fire a payload and
record a flag so it never fires again. We build that shared mechanism once (the *ledger*),
then layer consumers on top.

## Architecture — 3 layers

### Layer 0 — One-shot ledger (`NarrativeManager`)

- New persisted field `GameData.firedNarrativeFlags` (`string[]`), kept **separate** from
  the existing `binaryFeatureFlagsSet` so feature-flag semantics stay clean.
- `NarrativeManager` singleton (persisted via `SaveManager`, like other managers):
  - `bool HasFired(string id)`
  - `void MarkFired(string id)` — adds to the set and triggers a save.
  - `LoadState(string[] saved)` / `string[] GetForSave()` — mirrors the
    `NewContentTracker` persistence pattern.
- That is the entire foundation. Everything one-shot checks this.

### Layer 1 — Beats + dispatch (hybrid: data content, code wiring)

**Content as data — `NarrativeEventSO` (ScriptableObject):**
- `string eventID` — stable id.
- `string oneShotFlag` — the flag checked/recorded in `NarrativeManager`.
- `PayloadKind payloadKind` — enum: `Letter`, `Popup`, `Coachmark`. Only `Letter` is
  implemented this spec; the others are valid values dispatched later.
- For `Letter` payloads, a reference to / inline `LetterContent` (see Inbox below).

**Wiring as code — `NarrativeDirector` (singleton):**
- Subscribes to existing engine events:
  - first-launch (boot check against `NarrativeManager`),
  - `ResearchManager.OnFeatureFlagUnlocked` (string featureID),
  - `AnimalManager.OnAnimalUnlocked` (string animalID),
  - `RunManager.OnRunStarted` (available for future beats).
- On a trigger, finds the matching `NarrativeEventSO`, checks `HasFired(oneShotFlag)`,
  and if not fired: dispatches by `payloadKind`, then `MarkFired`.
- Hybrid split: the **condition logic** (which event maps to which beat) lives in code
  where it can be as complex as needed; the **content** (letter text, sender, reward)
  lives in the SO so text tweaks need no recompile.
- Dispatch routing: `Letter` → `InboxManager.Deliver(...)`. `Popup`/`Coachmark` →
  no-op stubs this spec (logged), filled in later.

### Layer 2 — Consumers

#### A. First-run onboarding — `FarmNamePopup`

- **Trigger:** on boot, if `!NarrativeManager.HasFired("onboarding_named")`, show the popup
  (modal, blocking — game waits behind it).
- **UITK modal** following the established popup lifecycle/pattern:
  - Title: "Your new farm's name:"
  - Tiny subtext: "(this can be changed later)"
  - Text field **prefilled with a random playful name** from a curated list
    (`FarmNameSuggestions`, a static string array or small SO).
  - **Dice / randomize button** — re-rolls another suggestion from the list.
  - **Save button** — disabled unless the name is valid.
- **Validation:** required, **3–30 characters** (trimmed). Save disabled otherwise.
- **On save:**
  1. Store `GameData.farmName`.
  2. `NarrativeManager.MarkFired("onboarding_named")`.
  3. Deliver a **welcome letter** to the Inbox (the first narrative beat — ties the two
     features together and gives the new mailbox immediate content).
- **Settings panel:** shows the farm name as the account name with an **edit** affordance
  that re-opens the same field + validation (rename allowed anytime).

#### B. Inbox / mailbox — built robust

- **`InboxManager` singleton** owns the list of **received letters**, persisted in GameData.
  - Each stored entry = `{ letterId, receivedUtcTicks, read, claimed }`. Content is looked
    up from the authored `LetterSO`/`NarrativeEventSO` by id (lightweight; no need to
    serialize full text). Runtime-generated letters are out of scope but the API leaves room.
  - **Public API `Deliver(string letterId)`** — so *any* system (the Director, future story
    arcs, events, daily systems) can drop a letter in. Fires an `OnInboxChanged` event.
  - Queries: `int UnreadCount()`, `IReadOnlyList<...> Letters()`, `MarkRead(id)`,
    `Claim(id)`.
- **Letter content fields** (all optional except subject/body):
  - `string senderName`, `Sprite senderPortrait` (builds toward the shop-owner characters),
  - `string subject`, `string body` (supports `{farmName}` token),
  - **reward**: currency enum (Coins / Gems / Compost) + amount, granted on Claim,
  - **CTA**: target enum + arg (e.g. `OpenShop:scarecrow`) → a button that navigates.
- **`InboxPopupUITK`** — two views:
  - **List view:** rows of (sender · subject · unread dot), newest first.
  - **Detail view:** portrait, sender name, body (token-resolved), optional **Claim**
    button (grants reward, then marks claimed), optional **CTA** button (navigates).
  - Reuses the established UITK popup lifecycle (see `project_uitoolkit_popups`).
- **Envelope top-bar button** beside the daily-rewards basket, with an **unread-count dot**
  (mirrors the basket/chest notification-dot pattern). Updates on `OnInboxChanged`.

#### Token resolution

- A small static helper `NarrativeText.Resolve(string body)` replaces `{farmName}` with
  `GameData.farmName`, structured so more tokens are trivial to add. Used by both the
  onboarding welcome letter and any letter body.

## Data flow (example: unlocking a building)

1. Player completes research → `ResearchManager.OnFeatureFlagUnlocked("scarecrow")`.
2. `NarrativeDirector` matches the `NarrativeEventSO` whose trigger = that feature id.
3. `HasFired` is false → dispatch: `InboxManager.Deliver("letter_scarecrow_unlock")`,
   then `MarkFired`.
4. `InboxManager` appends the entry, raises `OnInboxChanged`.
5. Envelope button raises its unread dot. Player opens Inbox → reads the letter from the
   blacksmith: "You can now build a Scarecrow — stop by the shop!" with a **Go to Shop** CTA.

## Persistence

All in the existing GameData JSON save (via `SaveManager`):
- `firedNarrativeFlags : string[]`
- `farmName : string`
- `inboxLetters : InboxEntry[]` (`letterId`, `receivedUtcTicks`, `read`, `claimed`)

Wired through `SaveManager` save/load alongside the existing manager state, following the
`NewContentTracker` `LoadState` / `GetForSave` precedent.

## Components & boundaries

| Unit | Responsibility | Depends on |
|------|----------------|------------|
| `NarrativeManager` | One-shot flag ledger + persistence | GameData |
| `NarrativeEventSO` | Authored beat content + trigger key + payload kind | — |
| `NarrativeDirector` | Map engine events → fire beats → dispatch | NarrativeManager, InboxManager, Research/Animal/Run events |
| `FarmNamePopup` | First-run naming modal + Settings rename | NarrativeManager, GameData, InboxManager |
| `InboxManager` | Received-letter store, `Deliver`, read/claim, persistence | GameData |
| `InboxPopupUITK` | List + detail letter UI | InboxManager, NarrativeText |
| Envelope top-bar button | Entry point + unread dot | InboxManager |
| `NarrativeText` | `{farmName}` token resolution | GameData |

Each unit has one purpose, a small interface, and can be tested independently
(`NarrativeManager`, `InboxManager`, validation, and token resolution are pure-logic and
unit-testable without the scene).

## Testing

- **EditMode unit tests:**
  - `NarrativeManager`: fire-once semantics, persistence round-trip.
  - `InboxManager`: deliver appends, unread count, read/claim transitions, save round-trip,
    deliver-same-letter behavior.
  - Farm-name validation: rejects <3 and >30, trims, accepts boundary 3 and 30.
  - `NarrativeText.Resolve`: replaces token, leaves unknown tokens, handles empty name.
- **Manual smoke (Unity):** fresh save → naming popup → save → welcome letter appears →
  unread dot → open/read/claim → Settings rename round-trips → unlock a feature fires its
  letter exactly once across relaunch.

## Deferred (future Spec B)

- Coach-mark / tutorial overlay system (truly anchored across UITK + uGUI + world),
  "replay tutorial" from Settings. Foundation reserves `PayloadKind.Coachmark` for it.
- Runtime-generated / fully dynamic letters (current model references authored content).

## Open defaults (chosen, flag if wrong)

- A **welcome letter** is delivered on naming as the first inbox content.
- Letter persistence **references the authored SO by id** rather than serializing full text.
