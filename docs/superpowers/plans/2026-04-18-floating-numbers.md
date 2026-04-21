# Floating Numbers Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Show animated floating reward text (+500G, +45$, etc.) whenever the player collects eggs or harvests crops, with a Settings toggle to disable it.

**Architecture:** `FloatingTextManager` singleton spawns TMP GameObjects on a high-order overlay canvas. `SettingsManager` wraps PlayerPrefs with a typed property. `SettingsMenuPanel` extends the existing `MenuPanel` base class and builds its UI programmatically in `Start()`. `EggClaimButton` and `Plant` call `FloatingTextManager` after currency is added.

**Tech Stack:** Unity TMP (TextMeshPro), LeanTween, PlayerPrefs, existing MenuPanel/DrawerUI system

---

## File Map

| File | Action | Responsibility |
|------|--------|---------------|
| `Assets/Scripts/FloatingTextManager.cs` | Create | Spawns and animates floating reward text |
| `Assets/Scripts/SettingsManager.cs` | Create | PlayerPrefs-backed static settings class |
| `Assets/Scripts/SettingsMenuPanel.cs` | Create | Settings UI panel — Visualize numbers toggle |
| `Assets/Scripts/EggClaimButton.cs` | Modify | Call `FloatingTextManager.ShowCoins` on egg claim |
| `Assets/Scripts/Plant.cs` | Modify | Call `FloatingTextManager.ShowMoney` on harvest |

---

### Task 1: SettingsManager

**Files:**
- Create: `Assets/Scripts/SettingsManager.cs`

- [ ] **Step 1: Create `SettingsManager.cs`**

```csharp
using UnityEngine;

public static class SettingsManager
{
    private const string KEY_FLOATING_NUMBERS = "setting_floating_numbers";

    private static int? _showFloatingNumbers;

    public static bool ShowFloatingNumbers
    {
        get
        {
            if (_showFloatingNumbers == null)
                _showFloatingNumbers = PlayerPrefs.GetInt(KEY_FLOATING_NUMBERS, 1);
            return _showFloatingNumbers.Value == 1;
        }
        set
        {
            _showFloatingNumbers = value ? 1 : 0;
            PlayerPrefs.SetInt(KEY_FLOATING_NUMBERS, _showFloatingNumbers.Value);
        }
    }
}
```

- [ ] **Step 2: Refresh Unity and verify no compile errors**

In Unity MCP: `refresh_unity(mode="force", scope="scripts", compile="request", wait_for_ready=True)` then `read_console(types=["error"], count=10)`. Expected: zero errors.

- [ ] **Step 3: Commit**

```bash
git add "Assets/Scripts/SettingsManager.cs"
git commit -m "feat: add SettingsManager with ShowFloatingNumbers PlayerPrefs setting"
```

---

### Task 2: FloatingTextManager

**Files:**
- Create: `Assets/Scripts/FloatingTextManager.cs`

This is the core singleton. It spawns a TMP label child of `FloatingTextCanvas` (sort order 500), animates it upward 120px over 1.2s with easeOutQuad, fades out over the last 0.4s, then destroys it. It uses `LeanTween.value` for the alpha fade (TMP alpha isn't a direct LeanTween target). All animations run on unscaled time.

- [ ] **Step 1: Create `FloatingTextManager.cs`**

```csharp
using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class FloatingTextManager : MonoBehaviour
{
    public static FloatingTextManager Instance { get; private set; }

    public enum CurrencyType { Money, Coins, Gems }

    public struct CurrencyReward
    {
        public CurrencyType type;
        public int amount;
        public CurrencyReward(CurrencyType t, int a) { type = t; amount = a; }
    }

    private Canvas canvas;
    private TMP_FontAsset font;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        canvas = GetComponent<Canvas>();
        if (canvas == null)
        {
            canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 500;
            gameObject.AddComponent<UnityEngine.UI.CanvasScaler>();
            gameObject.AddComponent<UnityEngine.UI.GraphicRaycaster>();
        }
    }

    // Called by Plant.Harvest() — accepts world position, converts internally
    public static void ShowMoney(int amount, Vector3 worldPos)
    {
        if (Instance == null || !SettingsManager.ShowFloatingNumbers) return;
        Vector2 screenPos = Camera.main.WorldToScreenPoint(worldPos);
        Instance.SpawnLabel(new List<CurrencyReward> { new CurrencyReward(CurrencyType.Money, amount) }, screenPos);
    }

    // Called by EggClaimButton — position is already screen-space
    public static void ShowCoins(int amount, Vector2 screenPos)
    {
        if (Instance == null || !SettingsManager.ShowFloatingNumbers) return;
        Instance.SpawnLabel(new List<CurrencyReward> { new CurrencyReward(CurrencyType.Coins, amount) }, screenPos);
    }

    public static void Show(List<CurrencyReward> rewards, Vector2 screenPos)
    {
        if (Instance == null || !SettingsManager.ShowFloatingNumbers) return;
        Instance.SpawnLabel(rewards, screenPos);
    }

    private void SpawnLabel(List<CurrencyReward> rewards, Vector2 screenPos)
    {
        GameObject go = new GameObject("FloatingReward", typeof(RectTransform));
        go.transform.SetParent(canvas.transform, false);

        TextMeshProUGUI tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.fontSize = 36;
        tmp.fontStyle = FontStyles.Bold;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.raycastTarget = false;

        if (rewards.Count == 1)
        {
            tmp.text = FormatReward(rewards[0]);
            tmp.color = GetColor(rewards[0].type);
        }
        else
        {
            // Multi-reward: use rich text, each on its own line
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < rewards.Count; i++)
            {
                string hex = ColorUtility.ToHtmlStringRGB(GetColor(rewards[i].type));
                sb.Append($"<color=#{hex}>{FormatReward(rewards[i])}</color>");
                if (i < rewards.Count - 1) sb.Append("\n");
            }
            tmp.text = sb.ToString();
            tmp.color = Color.white;
        }

        // Position: convert screen pos to canvas local pos
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(200, 80);
        rt.pivot = new Vector2(0.5f, 0f);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            canvas.GetComponent<RectTransform>(), screenPos, null, out Vector2 localPt);
        rt.anchoredPosition = localPt;

        // Animate: drift up 120px over 1.2s (easeOutQuad), fade out over last 0.4s
        Vector2 endPos = localPt + new Vector2(0, 120f);

        LeanTween.value(go, localPt, endPos, 1.2f)
            .setEaseOutQuad()
            .setIgnoreTimeScale(true)
            .setOnUpdate((Vector2 p) => { if (rt != null) rt.anchoredPosition = p; });

        // Fade: starts at t=0.8s, duration 0.4s
        LeanTween.value(go, 1f, 0f, 0.4f)
            .setDelay(0.8f)
            .setIgnoreTimeScale(true)
            .setOnUpdate((float a) => { if (tmp != null) tmp.alpha = a; })
            .setOnComplete(() => { if (go != null) Destroy(go); });
    }

    private static string FormatReward(CurrencyReward r)
    {
        return r.type switch
        {
            CurrencyType.Money => $"+{r.amount}$",
            CurrencyType.Coins => $"+{r.amount}G",
            CurrencyType.Gems  => $"+{r.amount}✦",
            _ => $"+{r.amount}"
        };
    }

    private static Color GetColor(CurrencyType t)
    {
        return t switch
        {
            CurrencyType.Money => new Color(0.298f, 0.686f, 0.314f), // #4CAF50
            CurrencyType.Coins => new Color(1f, 0.843f, 0f),         // #FFD700
            CurrencyType.Gems  => new Color(0.659f, 0.333f, 0.969f), // #A855F7
            _ => Color.white
        };
    }
}
```

- [ ] **Step 2: Refresh Unity and check console**

`refresh_unity(mode="force", scope="scripts", compile="request", wait_for_ready=True)` then `read_console(types=["error"], count=10)`. Expected: zero errors.

- [ ] **Step 3: Create `FloatingTextCanvas` GameObject in scene**

Using Unity MCP:
```python
manage_gameobject(action="create", name="FloatingTextCanvas", primitive_type=None)
manage_components(action="add", target="FloatingTextCanvas", component_type="Canvas")
manage_components(action="set_property", target="FloatingTextCanvas", component="Canvas",
    property="renderMode", value=0)  # ScreenSpaceOverlay
manage_components(action="set_property", target="FloatingTextCanvas", component="Canvas",
    property="sortingOrder", value=500)
manage_components(action="add", target="FloatingTextCanvas", component_type="CanvasScaler")
manage_components(action="add", target="FloatingTextCanvas", component_type="GraphicRaycaster")
manage_components(action="add", target="FloatingTextCanvas", component_type="FloatingTextManager")
```

Then save the scene.

- [ ] **Step 4: Commit**

```bash
git add "Assets/Scripts/FloatingTextManager.cs"
git commit -m "feat: add FloatingTextManager singleton with animated floating reward labels"
```

---

### Task 3: Wire egg claim → FloatingTextManager

**Files:**
- Modify: `Assets/Scripts/EggClaimButton.cs`

The `EggClaimButton` already subscribes to `OnEggClaimed`. We add a handler that reads the button's own `RectTransform.position` (which is screen-space on a Screen Space Overlay canvas) and calls `FloatingTextManager.ShowCoins`.

- [ ] **Step 1: Add `OnEggClaimedShowCoins` method to `EggClaimButton.cs`**

In `EggClaimButton.cs`, after the existing `onEggClaimed = UpdateState;` line in `Start()`, add a second subscriber. Then add the handler method.

Locate the block in `Start()`:
```csharp
onEggClaimed = UpdateState;
AnimalManager.Instance.OnEggClaimed += onEggClaimed;
```

Replace with:
```csharp
onEggClaimed = () =>
{
    UpdateState();
    AnimalData equipped = AnimalManager.Instance?.GetEquippedAnimal();
    if (equipped != null)
    {
        Vector2 screenPos = transform.position;
        FloatingTextManager.ShowCoins(equipped.rewardCoins, screenPos);
    }
};
AnimalManager.Instance.OnEggClaimed += onEggClaimed;
```

- [ ] **Step 2: Refresh Unity and check console**

`refresh_unity(mode="force", scope="scripts", compile="request", wait_for_ready=True)` then `read_console(types=["error"], count=10)`. Expected: zero errors.

- [ ] **Step 3: In-editor test**

Enter play mode, use "Egg Ready" dev tool button to force the egg ready, click the egg claim button. Expect: a gold "+{N}G" label rises from the button position and fades out over ~1.2s.

- [ ] **Step 4: Commit**

```bash
git add "Assets/Scripts/EggClaimButton.cs"
git commit -m "feat: show floating coins label when egg is claimed"
```

---

### Task 4: Wire crop harvest → FloatingTextManager

**Files:**
- Modify: `Assets/Scripts/Plant.cs`

`Plant.Harvest()` calls `CurrencyManager.Instance.AddMoney(harvestValue)` at line 245. We add a `FloatingTextManager.ShowMoney` call immediately after.

- [ ] **Step 1: Edit `Plant.cs` — add ShowMoney after AddMoney**

Locate in `Harvest()`:
```csharp
if (CurrencyManager.Instance != null)
    CurrencyManager.Instance.AddMoney(harvestValue);
```

Replace with:
```csharp
if (CurrencyManager.Instance != null)
{
    CurrencyManager.Instance.AddMoney(harvestValue);
    FloatingTextManager.ShowMoney(harvestValue, transform.position);
}
```

- [ ] **Step 2: Refresh Unity and check console**

`refresh_unity(mode="force", scope="scripts", compile="request", wait_for_ready=True)` then `read_console(types=["error"], count=10)`. Expected: zero errors.

- [ ] **Step 3: In-editor test**

Enter play mode, start a run, harvest a ready crop. Expect: a green "+{N}$" label rises from the crop's world position and fades out.

- [ ] **Step 4: Commit**

```bash
git add "Assets/Scripts/Plant.cs"
git commit -m "feat: show floating money label on crop harvest"
```

---

### Task 5: SettingsMenuPanel

**Files:**
- Create: `Assets/Scripts/SettingsMenuPanel.cs`

The existing `settingsPanel` GameObject in the DrawerUI scene already has a `VerticalLayoutGroup` (per the spec). `SettingsMenuPanel` extends `MenuPanel`, builds its toggle row programmatically in `Start()`, and reads/writes `SettingsManager.ShowFloatingNumbers`.

- [ ] **Step 1: Create `SettingsMenuPanel.cs`**

```csharp
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class SettingsMenuPanel : MenuPanel
{
    [SerializeField] private TMP_FontAsset font;

    private void Start()
    {
        BuildUI();
    }

    private void BuildUI()
    {
        // Row: [Toggle]  Visualize numbers
        GameObject rowGO = new GameObject("FloatingNumbersRow", typeof(RectTransform));
        rowGO.transform.SetParent(transform, false);
        rowGO.layer = 5;

        HorizontalLayoutGroup hlg = rowGO.AddComponent<HorizontalLayoutGroup>();
        hlg.spacing = 12;
        hlg.childAlignment = TextAnchor.MiddleLeft;
        hlg.childControlWidth = false;
        hlg.childControlHeight = true;
        hlg.childForceExpandWidth = false;
        hlg.childForceExpandHeight = false;

        LayoutElement rowLE = rowGO.AddComponent<LayoutElement>();
        rowLE.preferredHeight = 44;

        // Toggle
        GameObject toggleGO = new GameObject("Toggle", typeof(RectTransform), typeof(Toggle));
        toggleGO.transform.SetParent(rowGO.transform, false);
        toggleGO.layer = 5;
        RectTransform toggleRT = toggleGO.GetComponent<RectTransform>();
        toggleRT.sizeDelta = new Vector2(36, 36);

        // Toggle background
        GameObject bgGO = new GameObject("Background", typeof(RectTransform), typeof(Image));
        bgGO.transform.SetParent(toggleGO.transform, false);
        bgGO.layer = 5;
        RectTransform bgRT = bgGO.GetComponent<RectTransform>();
        bgRT.anchorMin = Vector2.zero;
        bgRT.anchorMax = Vector2.one;
        bgRT.sizeDelta = Vector2.zero;
        bgGO.GetComponent<Image>().color = new Color(0.3f, 0.3f, 0.3f, 1f);

        // Checkmark
        GameObject checkGO = new GameObject("Checkmark", typeof(RectTransform), typeof(Image));
        checkGO.transform.SetParent(bgGO.transform, false);
        checkGO.layer = 5;
        RectTransform checkRT = checkGO.GetComponent<RectTransform>();
        checkRT.anchorMin = new Vector2(0.1f, 0.1f);
        checkRT.anchorMax = new Vector2(0.9f, 0.9f);
        checkRT.sizeDelta = Vector2.zero;
        checkGO.GetComponent<Image>().color = new Color(0.298f, 0.686f, 0.314f); // green

        Toggle toggle = toggleGO.GetComponent<Toggle>();
        toggle.targetGraphic = bgGO.GetComponent<Image>();
        toggle.graphic = checkGO.GetComponent<Image>();
        toggle.isOn = SettingsManager.ShowFloatingNumbers;
        toggle.onValueChanged.AddListener(val => SettingsManager.ShowFloatingNumbers = val);

        // Label
        GameObject labelGO = new GameObject("Label", typeof(RectTransform));
        labelGO.transform.SetParent(rowGO.transform, false);
        labelGO.layer = 5;

        TextMeshProUGUI tmp = labelGO.AddComponent<TextMeshProUGUI>();
        tmp.text = "Visualize numbers";
        tmp.fontSize = 28;
        tmp.color = new Color(0.9f, 0.9f, 0.9f);
        tmp.alignment = TextAlignmentOptions.MidlineLeft;
        if (font != null) tmp.font = font;

        LayoutElement labelLE = labelGO.AddComponent<LayoutElement>();
        labelLE.flexibleWidth = 1;
    }
}
```

- [ ] **Step 2: Refresh Unity and check console**

`refresh_unity(mode="force", scope="scripts", compile="request", wait_for_ready=True)` then `read_console(types=["error"], count=10)`. Expected: zero errors.

- [ ] **Step 3: Attach SettingsMenuPanel to SettingsPanel GameObject in scene**

Using Unity MCP:
```python
find_gameobjects(search_term="SettingsPanel", search_method="by_name")
# Get the instance ID, then:
manage_components(action="add", target=<instance_id>, component_type="SettingsMenuPanel")
# Assign font if NotoSans SDF is available:
# manage_components(action="set_property", target=<id>, component="SettingsMenuPanel",
#     property="font", value=<font_guid>)
```

Save the scene.

- [ ] **Step 4: In-editor test**

Enter play mode, open the Settings drawer tab. Expect: a "Visualize numbers" row with a toggle checkbox appears. Toggle it off — floating numbers should stop showing on egg claim and harvest. Toggle it back on — they reappear.

- [ ] **Step 5: Commit**

```bash
git add "Assets/Scripts/SettingsMenuPanel.cs"
git commit -m "feat: add SettingsMenuPanel with Visualize numbers toggle"
```

---

## Self-Review

**Spec coverage:**
- [x] FloatingTextManager singleton on FloatingTextCanvas (sort 500) — Task 2
- [x] CurrencyType enum (Money, Coins, Gems) with symbols and colors — Task 2
- [x] `Show(List<CurrencyReward>, Vector2)`, `ShowMoney`, `ShowCoins` — Task 2
- [x] Returns early if `SettingsManager.ShowFloatingNumbers == false` — Task 2
- [x] Multi-reward stacking (separate lines) — Task 2
- [x] Animation: drift 120px up, 1.2s easeOutQuad, fade last 0.4s, unscaled — Task 2
- [x] SettingsManager with PlayerPrefs key `setting_floating_numbers`, default true — Task 1
- [x] SettingsMenuPanel on SettingsPanel, NotoSans font, size 28 — Task 5
- [x] EggClaimButton calls ShowCoins on egg claim, uses RectTransform.position — Task 3
- [x] Plant.Harvest calls ShowMoney(harvestValue, transform.position) — Task 4

**Type consistency check:**
- `FloatingTextManager.ShowCoins(int, Vector2)` — defined Task 2, called Task 3 ✓
- `FloatingTextManager.ShowMoney(int, Vector3)` — defined Task 2, called Task 4 ✓
- `SettingsManager.ShowFloatingNumbers` — defined Task 1, read Task 2, written Task 5 ✓
- `CurrencyReward(CurrencyType, int)` struct — defined and used within Task 2 ✓

**Placeholder scan:** No TBD/TODO/fill-in items present. All steps contain actual code.

**Note:** The spec mentions `Font: NotoSans-Regular SDF, size 36, bold` for the floating labels. The `FloatingTextManager` sets `fontSize = 36` and `FontStyles.Bold` but doesn't assign the font asset — Unity will use the default TMP font. To assign `NotoSans-Regular SDF` you'd need the GUID at component-add time; this is a wire-up step the implementer should do in the Inspector after attaching the script, or via MCP `manage_components` with the correct font GUID from `Assets/Fonts/`.
