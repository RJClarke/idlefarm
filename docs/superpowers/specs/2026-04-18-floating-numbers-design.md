# Floating Numbers Design
**Date:** 2026-04-18  
**Status:** Approved

## Overview

Show animated floating reward text whenever the player collects something — egg claims, crop harvests, etc. Each currency has a fixed color and symbol. A Settings toggle lets the player turn this off.

---

## Architecture

### New files
| File | Purpose |
|------|---------|
| `Assets/Scripts/FloatingTextManager.cs` | Singleton. Spawns and animates floating text. |
| `Assets/Scripts/SettingsManager.cs` | Static class. Reads/writes PlayerPrefs settings. |
| `Assets/Scripts/SettingsMenuPanel.cs` | `MenuPanel` subclass. Builds Settings UI in the existing drawer panel. |

### Modified files
| File | Change |
|------|--------|
| `Assets/Scripts/AnimalManager.cs` | Call `FloatingTextManager.Show` in `ClaimEgg()` |
| `Assets/Scripts/Plant.cs` | Call `FloatingTextManager.Show` in `Harvest()` |

---

## FloatingTextManager

**GameObject:** `FloatingTextCanvas` — new root object in the scene.  
**Canvas:** Screen Space — Overlay, sort order **500**.  
**Script:** `FloatingTextManager : MonoBehaviour`, singleton.

### CurrencyReward struct
```
struct CurrencyReward { CurrencyType type; int amount; }

enum CurrencyType { Money, Coins, Gems }
```

### Public API
```csharp
// Screen-space position (from UI RectTransform or Camera.main.WorldToScreenPoint)
FloatingTextManager.Show(List<CurrencyReward> rewards, Vector2 screenPos);

// Convenience overloads
FloatingTextManager.ShowMoney(int amount, Vector3 worldPos);
FloatingTextManager.ShowCoins(int amount, Vector2 screenPos);
```

### Behavior
- Returns immediately if `SettingsManager.ShowFloatingNumbers == false`.
- Spawns a single TMP `GameObject` as a child of `FloatingTextCanvas`.
- If multiple rewards: each is a separate line in the same TMP object, ordered top-to-bottom.
- **Animation** (all via LeanTween, unscaled time so game speed doesn't affect it):
  - Starts at `screenPos`, alpha = 1, scale = 1.
  - Drifts upward **120px** over **1.2s** with `easeOutQuad`.
  - Fades to alpha = 0 over the final **0.4s**.
  - Self-destructs on completion.
- Font: NotoSans-Regular SDF, size 36, bold.

### Currency identities
| CurrencyType | Symbol | Color |
|-------------|--------|-------|
| Money | `$` | `#4CAF50` (green) |
| Coins | `G` | `#FFD700` (gold) |
| Gems | `✦` | `#A855F7` (purple) |

Format: `+{amount}{symbol}` (e.g. `+45$`, `+500G`).

---

## SettingsManager

Plain `static` class (no MonoBehaviour). All settings backed by `PlayerPrefs`.

```csharp
public static class SettingsManager
{
    public static bool ShowFloatingNumbers { get; set; }  // PlayerPrefs key: "setting_floating_numbers", default true
}
```

Reads on first access, writes immediately on set.

---

## SettingsMenuPanel

Concrete `MenuPanel` subclass attached to the existing `SettingsPanel` GameObject (already has a `VerticalLayoutGroup`).

Builds its UI programmatically in `Start()`:

**Row layout:** `[Toggle checkbox]  Visualize numbers`

- Toggle reads initial state from `SettingsManager.ShowFloatingNumbers`.
- On value changed: writes back to `SettingsManager.ShowFloatingNumbers`.
- Style: matches existing drawer panel font (NotoSans-Regular SDF, size 28, color matching panel text).

---

## Integration Points

### Egg claim — `AnimalManager.ClaimEgg()`
After `CurrencyManager.Instance.AddCoins(equipped.rewardCoins)`:
```csharp
FloatingTextManager.ShowCoins(equipped.rewardCoins, eggClaimButtonScreenPos);
```
`eggClaimButtonScreenPos` is obtained from the `EggClaimButton`'s `RectTransform.position` (already screen-space on a Screen Space — Overlay canvas).

`AnimalManager` gets the screen position via an event or by holding a reference to `EggClaimButton`. Simplest: `EggClaimButton` subscribes to `OnEggClaimed` and calls `FloatingTextManager.ShowCoins` itself (keeps position logic in the UI layer).

### Crop harvest — `Plant.Harvest()`
After `CurrencyManager.Instance.AddMoney(harvestValue)`:
```csharp
Vector2 screenPos = Camera.main.WorldToScreenPoint(transform.position);
FloatingTextManager.ShowMoney(harvestValue, transform.position);
```
`ShowMoney` accepts a world position and does the conversion internally.

---

## Settings Persistence

| Key | Type | Default |
|-----|------|---------|
| `setting_floating_numbers` | int (0/1) | 1 (true) |

Uses `int` for PlayerPrefs compatibility (no native bool support).

---

## Out of Scope
- Floating numbers for purchases, upgrades, or daily rewards (can be added later using the same `Show` API).
- Animation customization per currency type.
- Queuing/pooling (low enough frequency that instantiate/destroy is fine).
