# Free Gift Chest, Farmer's Pass & Store — Design Spec

**Date:** 2026-10-04 · **Status:** approved in chat, awaiting written-spec review

## 1. Goal

Start monetizing with **opt-in rewarded ads only** and a **$9.99 Farmer's Pass** that delivers the same reward without the ad. Ship a scrollable **Store** page now, so gem bundles and future products (gains booster, skins, themes) are catalog entries, not new screens.

**Theme:** the chest is **the town's gift to the farm**. It starts the moment you first help the town (Welcome Basket), and it grows as you invest in the village (coins scale with Overall Farm Level). Copy, letters and art should read as "the town giving back", not "an ad reward".

Success:
- A capped, timed **Free Gift chest** is clearly the best *regular* gem source and is fun to open.
- After a few ad claims, players "get" the pass, so it becomes the most common purchase.
- Phase 1 is fully playable in the Editor with stand-in ad and store services. Real LevelPlay / Unity IAP adapters are stubbed and compile out until accounts exist.

Non-goals (this spec): real SDK wiring, store product registration, device testing, a server or cloud save, the gains booster, skins, themes, and interstitial or banner ads (never planned).

## 2. Decisions (from brainstorming)

| Topic | Decision |
|---|---|
| Ad types | Opt-in rewarded only. No interstitials, no banners. |
| Reward per claim | **10 gems + coins scaled by Overall Farm Level**. Same for ad and pass claims. |
| Cooldown | **30 min** from the last claim. 1 charge, no stacking while away. |
| Daily cap | **10 claims/day**, reset at local midnight. Applies to ad and pass claims alike. |
| Unlock | Delivering the **Welcome Basket** (first town request, Farm Level 1). Mayor letter + spotlight tooltip. **That first chest is free.** |
| Daily free chest | The **first chest of each local day needs no ad**. Same cooldown and cap, not an extra chest. |
| Economy changes | **Chicken 100 → 120 gems. Research slot 2: 100 → 200 gems.** |
| Pass | **$9.99 non-consumable**. Button becomes "Claim" (no ad), same cooldown and cap. **+500 gems** one-time on purchase. |
| Pass pitch | One-time card after the player's **3rd lifetime ad claim**. |
| Store entry | Tap the top-bar **gem counter**, plus a **Store** entry in Settings. |
| Ad SDK | **LevelPlay** (stubbed). Purchases: **Unity IAP** (stubbed). |
| Server | None. Store = truth for the pass. Gems stay local (see §9). |
| Copy | Short and plain. Store pass description uses: *"The Farmer's Pass: your Free Gift chest, ready to open the moment it's ready. No ads, ever. Plus 500 gems."* All copy lives in the catalog SO so it is editable without code. |

## 3. Free Gift rules

### 3.1 Coins by Farm Level
`coins = Interpolate(anchors, OverallFarmLevel)`. Linear between anchors, clamped at the ends, rounded to the nearest 10 above 100.

Coins **taper on purpose**: ~12% of an hour's Coins early (buys an occasional upgrade), falling to ~2% late, so gems become the real reward. Sources: `tools/progression_estimate.py` (2026-10-04 run: 243 Coins/h on day 1, 863 on day 7, 68.9k on day 30, 141k on day 90, 254k on day 365; Barn best case is 44 points by day 34 and 175 by day 455):

| Farm Level | Coins | ≈ share of an hour's Coins |
|---|---|---|
| 0 | 30 | 12% |
| 10 | 100 | 12% |
| 25 | 600 | ~10% |
| 50 | 4,000 | ~5% |
| 100 | 6,000 | ~3% |
| 175 | 7,500 | ~2% |

The anchors and the gem amount live on a `FreeGiftTuning` ScriptableObject. Re-tune them in the balance pass.

### 3.1a Chicken timing (why Chicken = 120)
All gem sources counted (chests, daily-reward track `0,1,0,2,0,1,0` + weekly 10, quest milestones at 5/10/15… quests). The daily free chest replaces one ad, so these numbers hold for ad watchers:

| Player | Chests/day | Chicken (120) |
|---|---|---|
| Minimum | 4 | day 3 |
| Typical | 6 | day 2 |
| Max (cap) | 10 | day 2, 2nd chest (not day 1) |
| Never watches ads | 1 (daily free) | ~day 9-10 |
| Pass owner | — | instantly (500-gem gift) |

Research slot 2 moves to 200 gems so the Chicken is the natural first gem purchase. **Balance-pass follow-up:** gem income rises from ~5/day to ~40-100/day, so re-check the later animal prices (Dog 500 → Goose 12,000) and the Rooster's 2 gems / 20 min. The Barn's Overall Farm Level milestone tooltips (10/25/50/100/175, currently "Reward not yet implemented") show *"Free Gift: X coins"* at that level.

### 3.2 Availability
- **Unlock:** when the **Welcome Basket** is delivered (narrative flag `first_request_done`, which awards Farm Level 1). Existing saves with that flag, or with ≥1 Barn point, start unlocked.
- **Introduction:** on unlock, a Mayor letter arrives (subject *"A little thank-you"*, two short lines, editable in `LetterCatalog`). When the inbox closes, a spotlight tooltip points at the HUD Gift button: *"A gift every 30 minutes. This one's on us!"* (`TutorialManager`, TargetPressed). That first chest is free. Its cooldown starts when it's claimed.
- **Daily free chest:** the first claim of each local day needs no ad (`claimsToday == 0`). It uses the same cooldown and cap, is not an extra chest, and doesn't count toward the pitch.
- **Ready** when `unlocked && claimsToday < dailyCap && now ≥ lastClaimUtc + cooldown`.
- **Daily cap** counts claims whose local date equals today. A new local date resets the count.
- **Clock guard:** elapsed time is measured with `OfflineClock.ForwardGapSeconds`, so setting the clock back never shortens the cooldown. If the stored claim time is in the future, re-anchor it to now.

### 3.3 Claim flow
1. Tap the HUD button or the Store card.
2. **No pass and not free** (the welcome chest and the day's first chest are free): `IAdService.ShowRewarded`.
   - Completed → step 3.
   - Closed early → nothing granted, no cooldown spent.
   - No ad available → toast *"No gift right now. Try again soon."*, charge kept.
3. **Chest reveal opens.** The reward is **granted and saved the instant the chest opens**, and the cooldown and count are recorded then too.
4. Ad claims increment `lifetimeAdClaims`. When it reaches 3 and the pitch hasn't been shown, the **pass pitch** opens after the chest closes.

## 4. HUD Gift button
- uGUI on the same Canvas as `EggClaimButton`, in a **fixed slot directly above it**. It does not move when the egg button hides.
- Same visibility rules as the egg button: farm + during runs, hidden at the Market (`LocationModeController` hide list).
- Icon: the closed-chest sprite (default `Chest_Red`).
- States:
  - **Ready:** gentle bob + notification dot. Corner tag: **"FREE"** when this chest needs no ad (welcome or the day's first), **"AD"** when it does, none for pass owners.
  - **Cooldown:** dimmed + countdown `mm:ss`.
  - **Capped:** dimmed + "Tomorrow".
  - **Locked:** hidden.

## 5. Chest reveal (`ChestRevealUITK`)
One shared modal for gift claims, gem bundles and the pass. Input: a list of `(currency, amount)` plus an optional banner line.

- Full-screen dim, high sort order (below the tutorial overlay's 2500). The closed chest drops in with a bounce, drawn at ~8× (pixel-crisp). Hint underneath: "Tap to open!"
- **Tap 1:** small shake + squash. **Tap 2:** bigger shake + glow behind. **Tap 3:** burst. The sprite swaps to the open chest (default `Chest_GoldOpen`), light rays rotate behind, coin and gem icons spray out, and **big numbers count up** with a scale-pop per line.
- **Bottom button:** reads "Claim" before opening (opens instantly in one press) and "Collect" after (closes). Mashing = 2 presses, about 1s.
- On Collect, the numbers fly to the top-bar counters, then the modal closes. Extra taps during transitions are ignored, never queued.
- The grant happens via a callback when the chest opens, not on close.
- SFX and haptic hook points (no-op today) on each tap and on the burst.
- Chest sprites are Inspector fields, so a sack, basket or real animation frames can drop in later.

## 6. Store (`StorePopupUITK`)
Scrollable UITK popup in the existing parchment/brown style (no blue, no accent stripes). Sections:

1. **Free Gift card:** chest icon, status ("Ready!" / countdown / "Tomorrow"), and a Watch or Claim button running the §3.3 flow.
2. **Farmer's Pass hero card:** description (§2 copy), "+500 gems" tag, price. Owned → "Owned" state with no button. Artwork slot for the user's future graphic.
3. **Gem bundles** (consumables; placeholder values, tuned later):

   | ID | Name | Gems | Fallback price | Ribbon |
   |---|---|---|---|---|
   | `gems_handful` | Handful | 100 | $0.99 | — |
   | `gems_pouch` | Pouch | 550 | $4.99 | +10% |
   | `gems_sack` | Sack | 1,200 | $9.99 | +20% |
   | `gems_chest` | Chest | 2,600 | $19.99 | +30% |
   | `gems_vault` | Vault | 7,000 | $49.99 | +40% |

4. **Coming soon** (disabled teaser cards): Harvest Blessing, Animal Skins, Building Skins, Farm Themes.
5. **Footer:** "Restore Purchases" link + one line of fine print.

Prices shown come from `IStoreService.GetPriceString(id)`. The stand-in returns the catalog fallback; Unity IAP will return localized prices.

**Delivery:** gem bundles play the chest reveal with gems only. The pass plays the chest with +500 gems and the banner "Farmer's Pass unlocked!". Purchase failure or cancel → short toast, nothing granted.

## 7. Pass pitch (`PassPitchUITK`)
Small modal: pass art slot, title "Farmer's Pass", the store description, a price button, and "Maybe later". Shown once (`passPitchShown` saved). Buying runs the §6 purchase flow.

## 8. Architecture

```
FreeGiftButton (uGUI) ─┐                     ┌─ IAdService ── FakeAdService (Editor/Dev)
StorePopupUITK ────────┼─ FreeGiftManager ───┤               └ LevelPlayAdService (#if LEVELPLAY, stub)
PassPitchUITK ─────────┘        │            └─ ChestRevealUITK
                                │  uses
                           FreeGiftCore (EconomyCore, pure)
StorePopupUITK ── StoreManager ── IStoreService ── FakeStoreService (Editor/Dev)
                       │                       └ UnityIapStoreService (#if UNITY_PURCHASING, stub)
                       └─ StoreCatalog (SO) / PurchaseLedgerCore (EconomyCore, pure)
MonetizationBootstrap: picks real vs fake services at startup.
```

| Unit | Responsibility |
|---|---|
| `FreeGiftCore` (EconomyCore) | Pure rules: IsReady, SecondsUntilReady, claims today, daily reset, clock guard, coin interpolation, pitch trigger. No Unity scene deps. |
| `FreeGiftTuning` (SO) | Cooldown, daily cap, gems per claim, coin anchors, unlock rule. |
| `FreeGiftManager` (singleton) | Owns `FreeGiftCore` state, runs ad → chest → grant, saves, events `OnStateChanged` / `OnClaimed`. |
| `IAdService` | `bool IsReady`, `void ShowRewarded(Action onRewarded, Action<AdFailReason> onFailed)`. |
| `FakeAdService` | 3s "Test Ad" overlay with **Finish** and **Close early**, plus a dev toggle for "no fill". |
| `LevelPlayAdService` | Stub inside `#if LEVELPLAY`. Empty methods with TODOs pointing at the levelplay skill. |
| `StoreCatalog` (SO) | Products: id, name, type (Consumable / NonConsumable), gems, fallback price, icon, section, description, ribbon, comingSoon. |
| `IStoreService` | `Initialize(Action<bool>)`, `string GetPriceString(id)`, `Purchase(id, Action<PurchaseResult>)`, `Restore(Action<bool>)`, `bool IsOwned(id)`, event `OnPurchasePending(id, transactionId)`. |
| `FakeStoreService` | Confirm dialog *"[Test Store] Buy {name} for {price}?"* with Confirm / Fail / Cancel. |
| `UnityIapStoreService` | Stub inside `#if UNITY_PURCHASING`. Pending → grant → confirm flow and local receipt validation noted as TODO. |
| `PurchaseLedgerCore` (EconomyCore) | Pure: records delivered transaction IDs and decides "deliver or already delivered". |
| `StoreManager` (singleton) | Routes purchases, delivers through the ledger + chest reveal, holds the pass entitlement, reconciles the pass with `IsOwned` on init. |
| `MonetizationBootstrap` | Picks services. Editor/Development → fakes. Release + defines → real. Release without defines → `UnavailableAdService` / `UnavailableStoreService`, which never grant. |

**Delivery order (every purchase):** pending → `ledger.TryDeliver(txId)` → grant + `SaveGame()` → confirm to the store. A re-delivered pending purchase whose txId is already in the ledger is confirmed without granting.

**Pass ownership:** saved locally for offline play. On store init, if the store says owned → set owned. If the store successfully reports *not* owned → clear the local flag (reverts save edits). If the store is unreachable → keep the local flag.

## 9. Purchase state & tampering (no server)
- **Pass (non-consumable):** the store account is the source of truth. It's re-checked each launch, survives reinstalls, and Restore covers iOS.
- **Fake receipts:** Unity IAP local receipt validation (Google Play key / Apple root cert) when wired in phase 2.
- **Gems after delivery** live only in the local save. Editing them needs a rooted or jailbroken device and only affects that player's single-player game. Accepted.
- **Revisit with a server** (Unity Gaming Services Cloud Save + Economy) when adding leaderboards, gifting/trading, or **cloud save** (a lost phone currently loses bought gems).

## 10. Save data (`GameData` additions)
| Field | Type |
|---|---|
| `farmersPassOwned` | bool |
| `giftLastClaimUtcTicks` | long |
| `giftClaimsTodayDate` | string (local `yyyy-MM-dd`) |
| `giftClaimsToday` | int |
| `giftLifetimeAdClaims` | int |
| `passPitchShown` | bool |
| `deliveredTransactionIds` | string[] |

Wired in `SaveManager.SaveGame` / load, following the existing `data.x = Manager.Instance.GetXForSave()` pattern. Defaults are safe for old saves (JsonUtility zero values).

## 11. Testing
- **EditMode (`IdleFarm.EditModeTests`)**
  - `FreeGiftCore`: ready / cooldown / cap; midnight reset; clock rollback can't shorten the cooldown; future timestamp re-anchors; coin interpolation at, between and beyond the anchors; pitch fires exactly once at the 3rd ad claim; pass claims and free chests don't count toward the pitch; the first claim of a day is free (and only the first); unlock via `first_request_done` / ≥1 Barn point.
  - `PurchaseLedgerCore`: first delivery grants; duplicate txId doesn't.
- **Play-mode click-through:** fake ad finish / close early / no fill; chest 3-tap and mash paths; reward lands and saves; cooldown and cap in the HUD and the Store; 3rd-claim pitch; buy the pass (500 gems, button flips to Claim, "AD" tag gone); buy each bundle; fake purchase fail/cancel; Restore; the gem counter opens the Store.

## 12. Phase 2 (later, needs accounts)
LevelPlay account + app key → install the package, define `LEVELPLAY`, fill in `LevelPlayAdService`. Google Play app + product IDs → install Unity IAP, define `UNITY_PURCHASING`, fill in `UnityIapStoreService` with receipt validation. Then device-test with Play test tracks.

## 13. Pre-release checklist (outside this build)
Reset the 99,999 test gems (scene + save). Make the Settings "+1,000 gems" button Editor/dev-only. Register products. Confirm the fake services are excluded from release builds.
