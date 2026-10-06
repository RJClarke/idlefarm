# Store 2.0: Tabs, Skins, Premium Sets, Harvest Blessing & Research Finish-Now — Design Spec

**Date:** 2026-10-05 · **Builds on:** `2026-10-04-free-gift-and-store-design.md` (Free Gift, Farmer's Pass, Store v1, stand-in services)

## 1. Goal

Turn the single-scroll Store into a tabbed shop that sells **skins** made from art already in the project (animal colour variants, alternate farmhouses), **real-money premium sets**, and a permanent **Harvest Blessing** boost. Gems need long-term sinks now that ad chests pay ~40-100 gems a day, so skins are priced as multi-week goals, and Research gets a repeatable gem **Finish now**.

Success:
- Buying and equipping a skin visibly changes the farm (the animal or the farmhouse) and survives restarts.
- Gem prices make the full catalogue a ~2-3 year goal for a typical player (§4).
- Everything is data-driven (catalog assets), so new skins are content, not code.

Non-goals: Cannery/Smokehouse speed-ups, timed gem boosts, seasonal skins (backlog), skins for Horse/Goose (no variant art), real SDK wiring (still phase 2).

## 2. Decisions (from brainstorming)

| Topic | Decision |
|---|---|
| Layout | **Top tabs:** Featured · Gems · Skins · Boosts. Skins = **2-column grid** of art cards with **Animals / Buildings** filter chips. Other tabs use list rows. |
| Skin currency | **Gems**, plus **real-money premium sets** whose items are set-exclusive. |
| In-world | All skins apply this round (animals + farmhouse). |
| Gem prices | 1,000 / 1,500 / 2,000-3,500 tiers (§4). User: "fairly long-term things… too expensive to be prohibitive". |
| Never-ending sink | **Research Finish-now for gems** only (not Cannery/Smokehouse, not timed boosts). |
| Harvest Blessing | $9.99 NonConsumable, permanent **+25% Coins from harvests** (tunable). |

## 3. Store layout

- **Header:** "Store" + close. Below it, a tab row of 4 brown pill tabs. The active tab is filled walnut with cream text; inactive tabs are light parchment with ink text. The last tab is remembered while the app runs.
- **Featured:** Free Gift card (as v1) · Farmer's Pass hero (as v1) · **Golden Farm Set** spotlight card · **Starter Bundle** card (hidden once bought).
- **Gems:** the 5 bundles (as v1 rows).
- **Skins:** chips `Animals | Buildings`. Under **Animals**, groups in order Chicken, Rooster, Cow, Pig, Dog. Under **Buildings**, the Farmhouse group. Each group has a small pixel-font header, then a 2-column grid. The first card is "Classic" (the base look: always owned, equippable). Set-exclusive skins show their set's price and a "Set" ribbon, and tapping them opens the set purchase.
- **Skin card:** preview art (an idle frame for animals, the whole house for buildings) on a parchment tile, name, then one state:
  - **Price** (gem icon + amount; walnut button).
  - **Set** (the set's real-money price).
  - **Equip** (owned, not equipped).
  - **Equipped** (gold outline + "Equipped" label, no button).
- **Boosts:** Harvest Blessing row ($9.99 · "+25% Coins from every harvest, forever"; "Owned" once bought) · Farm Themes (coming soon).
- **Footer** (every tab): Restore Purchases + fine print (as v1).
- Cards update their labels/buttons in place on purchase/equip events. There is no rebuild on a tick; a tab switch rebuilds only that tab's content.

## 4. Catalogue & prices

Gem tiers: **Common 1,000 · Uncommon 1,500 · Farmhouse 2,000-3,500.** Set items are real-money only.

| Target | Classic (base art) | Common 1,000 | Uncommon 1,500 | Set-exclusive |
|---|---|---|---|---|
| Chicken | White | Brown, Brown 2, Gray | Black & Brown, Yellow | Golden |
| Rooster | Gray | White, Brown, Brown 2 | Black & Brown, Yellow | Golden |
| Cow | Classic | Caramel | Big White, Big Black, Big Caramel | — |
| Pig | Pink | Light Pink, Pink & Brown | Spotted Pink, Spotted Gray | — |
| Dog | Brown Shepherd | Dark Shepherd, Gray Shepherd | — | Labrador Brown, Dark, White |

| Farmhouse skin | Art | Gems |
|---|---|---|
| Classic | Farmer_House_2 | owned |
| Cottage | Farmer_House_1 | 2,000 (also in the Starter Bundle) |
| Steel Barn | Barn_Small | 2,500 |
| Red / Green / Grey Barn | Front_Hayloft_* | 2,500 each |
| Ranch House | One_Story_House | 3,000 |
| Country Manor | Country_House | 3,500 |
| Mountain Lodge | Japanese_House | 3,500 |
| Yellow Barn | Front_Hayloft_Yellow | Set-exclusive |

**Real-money products (new):**

| ID | Name | Price | Grants |
|---|---|---|---|
| `set_golden_farm` | Golden Farm Set | $4.99 | Golden Chicken, Golden Rooster, Yellow Barn |
| `set_puppy_pack` | Puppy Pack | $2.99 | 3 Labradors |
| `bundle_starter` | Starter Bundle | $2.99, one-time | 800 gems + Cottage |
| `boost_harvest_blessing` | Harvest Blessing | $9.99 | +25% harvest Coins, permanent |

**Economy check** (all gem sinks: animals 28,770 + 28 gem skins 46,500 = ~75,500):

| Player | Gems/day | Gems after 2 yrs | Buys everything |
|---|---|---|---|
| Casual (3 chests/day) | ~34 | ~24.8k | ~6 yrs |
| Typical (6/day) | ~64 | ~46.7k | ~3.2 yrs |
| Dedicated (10/day + Rooster) | ~116 | ~84.7k | ~1.8 yrs |

Research Finish-now (§7) is the open-ended sink past that. Balance-pass note: the Rooster (2 gems / 20 min) is a large gem source for active players.

## 5. Skin ownership

- `SkinOwnershipCore` (EconomyCore, pure):
  - owned skin ids;
  - the equipped skin per target (`chicken`, `rooster`, `cow`, `pig`, `farm_dog`, `farmhouse`);
  - "Classic" is implicit (always owned; equipped when no entry).
- Operations:
  - `CanBuy` / `TryBuy(skin, gemsAvailable)` returns the gem cost to spend, or a refusal (already owned, set-only).
  - `Grant(ids)` (sets and bundles).
  - `Equip(skinId)` (must be owned; Classic always).
  - `EquippedFor(target)`.
  - Export/import.
- `SkinManager` (singleton, self-bootstrapping like StoreManager): owns the core, spends gems, raises `OnSkinsChanged`, and saves via `GameData.ownedSkinIds` + `equippedSkins` (`target=skinId` strings).
- Buying a skin with gems equips it immediately, followed by a short toast "Equipped!" (chest-icon toast).
- Skins are not per-animal-unlock gated: you can buy a Cow skin before owning a Cow, and it shows once the Cow is out.

## 6. Applying skins in the world

- **Animals:** a `SkinSwapper` is added to the animal visual when `AnimalManager` spawns it (this includes the Dog).
  - In `LateUpdate`, after the Animator writes its frame, any sprite drawn from the **base texture** is replaced by a cached `Sprite.Create(skinTexture, baseSprite.rect, normalized pivot, baseSprite.pixelsPerUnit)`.
  - This works because every variant sheet has the identical pixel layout as its base (verified: same dimensions per family; slicing tools only copied metadata).
  - Eggs and other renderers are untouched. Equip changes apply live via `OnSkinsChanged`.
- **Farmhouse:** a `BuildingSkinApplier` on `BarnBuilding` swaps the SpriteRenderer sprite for `Sprite.Create(skinSprite.texture, skinSprite.rect, basePivotNormalized, ppu)`. `ppu` is chosen so the skin draws at the **base sprite's world width**. The transform, collider and press animation are untouched, the pivot keeps the base aligned, and taller houses extend upward. Classic restores the original sprite.

## 7. Research Finish-now (gems)

- In the Compost Boost modal for an active slot, a new row reads "Finish now" with a price (gem icon). It's disabled when gems are short.
- Cost: `ceil(hoursLeft × gemsPerHour)`, minimum 1. **`gemsPerHour = 20`** on `ResearchTuning`, so 1 day costs 480 gems. This is a pure function in EconomyCore, tested.
- `ResearchManager.TryFinishWithGems(slot)` recomputes the remaining seconds at tap time, spends the gems, then moves the slot's `startUtcTicks` back by the remaining time. The next tick completes the level through the normal path, so auto-repeat, partial progress, binary unlocks and events all keep working.

## 8. Harvest Blessing

`StoreManager.HarvestCoinMultiplier` is 1.25 when owned (tunable) and 1 otherwise. `Plant` multiplies harvest Coins by it after Bountiful/Golden. The offline run simulator and the Almanac don't show it this round (noted).

## 9. Data & assets

- `SkinCatalogSO` (Resources/SkinCatalog). Entries: `id`, `displayName`, `target`, `kind` (AnimalSheet / BuildingSprite), `texture` (animal sheet) or `sprite` (building), `gemPrice`, `setId` (set-exclusive), `sortOrder`.
- Seeded by `Farm Game/Monetization/Build Skin Catalog` from the paths in §4, appending missing ids only.
- `StoreProductDef` gains `grantsSkinIds` (string[]) and `StoreSection.Sets` / `StoreSection.Boosts`. `StoreDefaults` adds the 4 new products. Harvest Blessing replaces the "soon_harvest_blessing" teaser.
- Card previews: animals use the base idle-left frame's rect cut from the variant texture; buildings use the sprite. Both are built at runtime and cached.

## 10. Testing

- **EditMode:**
  - `SkinOwnershipCore`: buy (enough/insufficient gems, owned, set-only refused); grant; equip owned/unowned/classic; export/import round-trip with bad data.
  - `ResearchGemPrice`: rounding, minimum, zero/negative time.
  - `StoreDefaults`: new products and grants exist in the skin defaults.
  - `SkinDefaults`: unique ids, valid targets, prices match the §4 tiers.
- **Play-test:**
  - Each tab renders.
  - Buy a gem skin: gems spent, auto-equipped, the animal in the world changes colour while walking/idling/eating.
  - Equip Classic restores the original.
  - The farmhouse skin swaps with its base aligned and the tap still opens the Barn.
  - The Golden Farm Set grants 3 skins via the chest reveal.
  - Harvest Blessing increases harvest Coins.
  - Research Finish-now completes a level and charges the right gems.
  - Skins persist across restart.
