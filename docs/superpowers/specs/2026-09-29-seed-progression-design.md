# Seed Progression — Design

**Date:** 2026-09-29
**Status:** Approved in chat; awaiting spec review
**Goal:** New farms start with one simple crop and grow into the rest. Each new crop feels different, teaches something new, and the seed stall hints that there's more still to discover.

---

## 1. Summary

Today every crop is plantable from the first run, the Mayor hands out Carrot + Tomato (a regrowing crop on day one), and every crop banks exactly 1 Coin per harvest. The Market already has a **Plants stall** selling four crop unlocks, but the seed menu ignores ownership — the gating was lost in an earlier menu rewrite.

This design:

1. Enforces crop ownership in the seed menu (only owned crops are plantable).
2. Puts all 9 crops in the Plants stall — run by a new character, **Hazel** — as either a **priced packet** (visible, locked, shows its price) or a **masked packet** (greyed-out mystery; unlocked by progression, with a vague hint).
3. Gives the Mayor's welcome letter a **Radish** starter and 50 Coins (enough for Carrot, not Beans).
4. Scales **Coins per harvest** up the ladder so pricier crops earn more Coins, not fewer.
5. Adds a **compost trait** (Corn makes double compost when lost).
6. Adds teaching beats: a regrowing-crop tip, Hazel's letters, and a **town-board chain** (letter → first request → Collect/Sell → first Barn point → spend it).
7. Resets existing saves to the starter crop (user decision).

---

## 2. The crop ladder

"Priced" = visible in the stall from the start, locked, shows its price. "Masked" = greyed-out mystery packet until its condition is met, then becomes a normal priced packet.

**Revised 2026-09-29/30 (user):**
- The fixed order is **cheapest to most expensive**.
- Prices have more variety, and the two peppers cost the same so neither looks best-in-slot.
- Coins per harvest are **flat (1)**. Crops differ by timing, HP, thirst, pests and extras; per-minute balance comes from a later balance pass.

| Crop | Stall state at start | Condition to reveal | 1st packet (Coins) | Niche |
|---|---|---|---|---|
| Radish | Owned (starter) | — | free (welcome letter) | Volume: most harvests per minute |
| Carrot | Priced | — | 10 | Underground, so crows can't reach it; deer love it |
| Green Beans | Priced | — | 250 | **Regrowing**; easy to keep watered |
| Tomato | Priced | — | 500 | Sturdy regrower, cannable |
| Corn | **Masked** | research `composting_basics` | 1,000 | **Double compost** when lost |
| Green Pepper | Priced | — | 1,500 | Deer won't touch it |
| Red Pepper | Priced | — | 1,500 | Deer won't touch it; highest value |
| Strawberry | **Masked** | research `cannery_unlocked` | 2,500 | Quickest jam |
| Blueberry | Priced | — | 5,000 | Premium jam; deer mostly ignore it |

**Seed packets.** Each packet lets a crop grow in one field at once, up to one per field the farm owns (maximum 4).
- **Pricing:** the 2nd packet costs 10× the 1st (a free starter's 2nd costs 10), and the 3rd and 4th double. `SeedShopRules.SecondPacketMultiplier` is the one number to tune.
- **Storage:** ownership level = packet count.
- **In the stall:** owned rows show "+1 PACKET" and the price while the farm has more fields than packets. Otherwise they show "Owned" or "Owned xN".
- **Teaching:** buying Field 2 sends Hazel's letter `extra_packets` ("One crop, two fields?"). The stall then shows `tip_extra_packet` the first time an extra packet is buyable.
- **Crop upgrades** (improving the crop itself) are a separate, later system.

**Starter check (must happen before tuning is final):** the offline simulator (one fresh field, no upgrades) suggested Radish-as-starter / Carrot-as-first-buy: Radish run ~30 min / ~284 Coins, Carrot run ~45 min / ~181 Coins at 1 Coin each. One real play-mode run with each on a fresh farm confirms the call and the Coins-per-harvest numbers above. If Carrot plays better as the starter, the two swap rows (Radish becomes the 10-Coin packet); nothing else in this design changes.

**Masked packet hint text:** "Discovered through research" for both research-gated crops. The hint is generic on purpose — it says *how* to find more, not *what* it is.

**Welcome letter:** gives the Radish seeds (unlocks Radish) and **50 Coins** (down from 100). Copy points at Hazel's stall for the next seeds.

---

## 3. Teaching beats

### 3.1 Seeds & crops

| When | Mail | Tip |
|---|---|---|
| Farm named | Mayor's welcome letter now gives Radish seeds + 50 Coins | (existing first-session chain, unchanged) |
| Run 1 ends | New letter from **Hazel**: "Fresh seeds at my stall — carrots for next to nothing." Button: **Visit Hazel's Stall** | — |
| Plants stall first opened | — | `tip_seed_stall`: buying a packet unlocks that crop for every run after; masked packets are still to be discovered |
| First regrowing crop bought | Hazel: "These keep producing" letter | — |
| First regrowing harvest in a run | — | `tip_regrow`: "As long as the plant survives after it's picked, it grows more. No new seed needed!" |
| Compost Bay research | Pippa's existing letter gains a line about Corn | — |
| Cannery research | Marta's existing letter gains a line about Strawberries | Cannery tip (existing) |

### 3.2 Town board chain

1. **Run 4 ends** → Mayor letter (`town_board_intro`, new players only), button **See Requests**. Draft: *"Word's getting around about your farm! A few of us in town could use a hand now and then, so we post what we need on the community board by the Market. Help out, and folks won't forget it."*
2. **First board visit** → the first request is a fixed **"Welcome basket"** request: **8 Radish**, from the Mayor, worth exactly `ReputationMath.PointCost(1)` = **100 Reputation** (one full Barn point). `tip_town_requests` (existing) gains a line: deliveries earn Reputation; a full bar earns a Barn point.
3. **Next run start** → the **Collect / Sell** spotlight (moved from "start of run 2") with new copy: *"Sell turns each harvest into Money right away, to keep your run going. Collect keeps harvests as items, for town requests and your jars."*
4. **First delivery** → "The town is grateful" (existing letter, button **Open the Barn**).
5. **Barn opened with an unspent point, first time** → spotlight on the first track's "+" button: *"Tap + to spend your point. Each level makes your farm a little better at that job."*

Rough timing with ~30–45-minute runs: runs 1–4 on day 1–2, the Barn point by day 2–3.

---

## 4. Systems

### 4.1 Crop ownership (single source of truth)

- `CropData` gains `unlockCost` (int), `unlockFeatureFlag` (string, empty = none), `isStarter` (bool, Radish only), `compostMultiplier` (float, default 1; Corn = 2).
- Ownership is stored like other permanent unlocks: `UpgradeManager` permanent level for id `crop_<cropName>` (0 = not owned). The starter is owned by default.
- New pure helper in EconomyCore, `SeedShopRules`:
  - `SeedState State(bool isStarter, bool owned, bool hasCondition, bool conditionMet)` → `Owned | Priced | Masked`.
  - Unit-tested.
- `CropDatabase.IsOwned(CropData)` and `CropDatabase.OwnedCrops()` wrap it for gameplay code.

The four existing crop `UnlockData` assets (Corn, Strawberry, Blueberry, Bean) and the Plants stall's `unlocks` list are replaced by rows built from `CropDatabase.allCrops` + the new `CropData` fields, so price and condition live in one place.

### 4.1a One fixed crop order, everywhere

The ladder order in §2 is **the** crop order for every player, always:

Radish, Carrot, Green Beans, Corn, Strawberry, Tomato, Blueberry, Green Pepper, Red Pepper.

- `CropDatabase.allCrops` is reordered to match, and it is the only source of order. Every list of crops iterates `allCrops` and **filters** it, never sorts or re-collects it. That covers the stall, the seed rail, the Almanac, the inventory, the Cannery and town-request pools.
- Nothing is ordered by purchase time, save order, dictionary order or state. For example, owned crops do *not* float to the top of the stall, and a newly bought crop takes its fixed slot in the seed rail, not the end.
- `CropDatabase.OwnedCrops()` returns `allCrops` filtered, in `allCrops` order.
- **Guard test:** an EditMode test loads `CropDatabase.asset` and asserts `allCrops` names equal the ladder list exactly, so a stray drag in the Inspector fails the suite instead of shuffling the game.

### 4.1b Crop info pages (Almanac)

Crop "i" badges open Almanac pages, and the Almanac follows the same three states, in the same fixed order:

- **Owned:** full page, as today.
- **Priced (not yet bought):** full page, so players can read up before spending, plus a line at the top: "Sold at Hazel's stall - 100 Coins".
- **Masked:** a locked "???" tile using the Almanac's existing locked style (as used for locked equipment). It can't be opened, so nothing about the crop is revealed.

The seed-rail badge (owned crops) and the stall badge (owned and priced rows) both go to these pages. Masked stall rows have no badge.

### 4.2 Plants stall (`ShopPopupUITK`, Plants section)

- Rows for all 9 crops, in the fixed order (§4.1a):
  - **Owned:** packet art, name, "Owned".
  - **Priced:** packet art, name, price button.
  - **Masked:** packet art tinted to a dark silhouette, name "???", hint "Discovered through research", no button.
- Buying spends Coins, sets `crop_<name>` to 1, fires `NarrativeDirector.Raise("seed_bought")`, and fires `seed_bought:regrow` when the crop regrows.
- Title shows "Hazel's Seeds". First open fires `OnMenuOpened("tip_seed_stall")`.
- The row "i" badge opens the crop's Almanac page for Owned and Priced rows; Masked rows have no badge.

### 4.3 Seed menu (`SeedSelectionPopup`)

- The seed rail shows **owned crops only**, in the fixed order (§4.1a), plus one trailing tile: "More seeds at Hazel's stall" (key icon). Tapping it closes the picker, pans to the Market and opens the Plants stall.
- Saved field assignments that reference a crop the player doesn't own are cleared when the seed menu opens and before a run starts, so no field starts with an unowned crop.
- **One crop per field (unchanged), and a run can start with some fields empty.** Today a run can't start until every unlocked field is filled. With progression, a player could own fewer crops than fields, so the rule becomes "at least one field has a crop". An unlocked field without a crop shows "Buy more seeds at Hazel's" in the seed menu and stays empty that run. (Later idea, not in this spec: buy extra packets of a crop to plant it in more fields. Ownership levels above 1 can represent packet count.)

### 4.4 Coins per harvest & compost trait

- `coinValue` is set per the ladder table. `CropStats.Coins` already reads it, so the Almanac shows the new values.
- `CompostBay` multiplies its yield by `cropData.compostMultiplier`.
- The Almanac gets a "Great compost" tag for crops with a multiplier above 1 (`CropTraits.Tags`).

### 4.5 Save reset (no migration step)

- Ownership uses **new** ids: `seed_<crop slug>` (for example `seed_carrot`, `seed_green_beans`).
- The old crop unlock ids (`corn_unlock`, `strawberry_unlock`, `blueberry_unlock`, `bean_unlock`) are simply never read again. Every existing save therefore starts from the starter crop automatically, while Coins, upgrades and everything else stay as they are.
- A one-time migration step would have been riskier: it could wipe crops a new player bought during their first session.
- Saved field assignments for crops the player doesn't own are cleared when the seed menu opens and before a run starts (§4.3).

### 4.6 Town board first request

- `ReputationManager`: while flag `town_board_intro` has fired and flag `first_request_done` has not, slot 0 always holds the fixed "Welcome basket" request. Rep reward = `ReputationMath.PointCost(1)`.
- The fixed request can't be skipped.
- On fulfilment, set `first_request_done`; normal generation resumes.

### 4.7 Narrative / tutorial wiring

- New cast member **Hazel** (seed stall): warm, practical, loves a bargain.
- New letters:
  - `seed_stall_intro`: `run_ended:1`, new players only, CTA `OpenPlantsShop`.
  - `regrow_bought`: `seed_bought:regrow`, new players only.
  - `town_board_intro`: `run_ended:4`, new players only, CTA `OpenTownRequests`.
- New `CtaKind.OpenPlantsShop` (appended to the enum): pans to the Market and opens the Plants stall.
- New tips: `tip_seed_stall`, `tip_regrow`, `tip_barn_spend`. Updated copy: `tip_collect_sell`, `tip_town_requests`, the welcome, compost and cannery letters.
- `OnboardingTutorials`:
  - `OnFirstRegrow()` is called from `Plant.StartRegrowth` during a run.
  - `TryCollectSellTip` now triggers on the first run start after `town_board_intro`, no longer on run 2.
  - `OnBarnOpened` shows `tip_barn_spend` when there are unspent points, spotlighting a "+" button rect exposed by `BarnPopupUITK`.
- All new copy goes through `LetterCatalog.asset` + `NarrativeDefaults`, and `docs/narrative/cast-and-copy.md` is regenerated.

---

## 5. Out of scope

- The offline (away-from-game) simulator still ignores crop traits, regrowing and the compost multiplier (known gap).
- Pricing beyond this ladder, new crops, and seasonal seeds.
- Renown (Overall Farm Level) milestone rewards.

---

## 6. Testing

- **EditMode, pure:**
  - `SeedShopRules.State` for every combination.
  - **Crop order guard:** `CropDatabase.allCrops` equals the ladder order; `OwnedCrops()` preserves it for any ownership subset, including a purchase order such as Tomato before Carrot.
  - `SeedShopRules.OwnershipKey` slugs, including a two-word crop name.
  - The first request's reward equals `PointCost(1)`.
  - `CropTraits` "Great compost" tag.
  - `NarrativeDefaults`: every new letter has a sender in the cast, and every new tip id is present.
- **Play-verified at 1080x2400 on a fresh farm:**
  - Radish-only seed menu with the Hazel tile.
  - Hazel letter after run 1, stall with priced + masked rows, Carrot purchase → Carrot in the seed menu.
  - Beans purchase → regrow letter → regrow tip on first regrowth.
  - Board letter after run 4 → Welcome basket request → Collect tip → delivery → Barn point → spend tip.
  - Starter check: one real run with Radish vs Carrot to confirm the starter and the Coins-per-harvest numbers.
- **Save reset:** load the backed-up dev save, confirm only Radish is owned, and confirm Coins and upgrades are unchanged.
- **Two fields, one crop:** with only Radish owned and Field 2 bought, Radish fills Field 1, Field 2 shows the Hazel hint, and the run starts with Field 2 empty.
