using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class FloatingTextManager : MonoBehaviour
{
    public static FloatingTextManager Instance { get; private set; }

    public enum CurrencyType { Money, Coins, Gems, Compost, Wood }

    public struct CurrencyReward
    {
        public CurrencyType type;
        public int amount;
        public CurrencyReward(CurrencyType t, int a) { type = t; amount = a; }
    }

    private Canvas canvas;
    [SerializeField] private TMP_FontAsset font;

    // ── Label pool ────────────────────────────────────────────────────────
    // Floating rewards are the most-fired visual in the game (every harvest),
    // so we recycle label GameObjects instead of new/Destroy per pop.
    private const int PoolCap = 16;
    private readonly Queue<GameObject> idleLabels = new Queue<GameObject>();
    // Active labels in spawn order; index 0 is the oldest still-animating one,
    // which is the one we reclaim first when the pool is exhausted.
    private readonly List<GameObject> activeLabels = new List<GameObject>();

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
        if (Instance == null || Camera.main == null || !SettingsManager.ShowFloatingNumbers) return;
        Vector2 screenPos = Camera.main.WorldToScreenPoint(worldPos);
        Instance.SpawnLabel(new List<CurrencyReward> { new CurrencyReward(CurrencyType.Money, amount) }, screenPos);
    }

    // Called when money LEAVES the player (e.g. buying a seed bag). Shows "-N$" in red.
    public static void ShowMoneySpent(int amount, Vector3 worldPos)
    {
        if (Instance == null || Camera.main == null || !SettingsManager.ShowFloatingNumbers) return;
        Vector2 screenPos = Camera.main.WorldToScreenPoint(worldPos);
        Instance.SpawnSpendLabel(amount, screenPos);
    }

    // Called by AnimalManager / Plant.Harvest — accepts world position, converts internally.
    // Optional delay staggers the coin pop after the cash pop so both are readable.
    public static void ShowCoins(int amount, Vector3 worldPos, float delay = 0f)
    {
        if (Instance == null || Camera.main == null || !SettingsManager.ShowFloatingNumbers) return;
        Vector2 screenPos = Camera.main.WorldToScreenPoint(worldPos);
        var rewards = new List<CurrencyReward> { new CurrencyReward(CurrencyType.Coins, amount) };
        if (delay <= 0f)
        {
            Instance.SpawnLabel(rewards, screenPos);
        }
        else
        {
            // Scheduled on the persistent manager so it survives the source object's destruction.
            LeanTween.delayedCall(Instance.gameObject, delay,
                () => { if (Instance != null) Instance.SpawnLabel(rewards, screenPos); })
                .setIgnoreTimeScale(true);
        }
    }

    // Spend popup anchored to an explicit screen position (used by seed-bag widgets so the
    // -$ animates from the bag that was just bought).
    public static void ShowMoneySpentAtScreen(int amount, Vector2 screenPos)
    {
        if (Instance == null || !SettingsManager.ShowFloatingNumbers) return;
        Instance.SpawnSpendLabel(amount, screenPos);
    }

    // Called by AnimalManager — gem reward at world position
    public static void ShowGems(int amount, Vector3 worldPos)
    {
        if (Instance == null || Camera.main == null || !SettingsManager.ShowFloatingNumbers) return;
        Vector2 screenPos = Camera.main.WorldToScreenPoint(worldPos);
        Instance.SpawnLabel(new List<CurrencyReward> { new CurrencyReward(CurrencyType.Gems, amount) }, screenPos);
    }

    public static void ShowCompost(int amount, Vector3 worldPos)
    {
        if (Instance == null || Camera.main == null || !SettingsManager.ShowFloatingNumbers) return;
        Vector2 screenPos = Camera.main.WorldToScreenPoint(worldPos);
        Instance.SpawnLabel(new List<CurrencyReward> { new CurrencyReward(CurrencyType.Compost, amount) }, screenPos);
    }

    // Called by TreeNode while chopping — a small +N per swing so gathered Wood ticks up as you chop.
    public static void ShowWood(int amount, Vector3 worldPos)
    {
        if (Instance == null || Camera.main == null || !SettingsManager.ShowFloatingNumbers) return;
        Vector2 screenPos = Camera.main.WorldToScreenPoint(worldPos);
        Instance.SpawnLabel(new List<CurrencyReward> { new CurrencyReward(CurrencyType.Wood, amount) }, screenPos);
    }

    public static void Show(List<CurrencyReward> rewards, Vector2 screenPos)
    {
        if (Instance == null || !SettingsManager.ShowFloatingNumbers) return;
        Instance.SpawnLabel(rewards, screenPos);
    }

    // Generic colored text pop at a world position (e.g. the fishing "+1 🐟" over the pole).
    public static void ShowText(string text, Color color, Vector3 worldPos)
    {
        if (Instance == null || Camera.main == null || !SettingsManager.ShowFloatingNumbers) return;
        Vector2 screenPos = Camera.main.WorldToScreenPoint(worldPos);
        Instance.SpawnTextLabel(text, color, screenPos);
    }

    // Called by Plant.Harvest when a harvest is diverted into the Cannery instead of paying out.
    public static void ShowCanneryIntake(Vector3 worldPos)
    {
        if (Instance == null || Camera.main == null || !SettingsManager.ShowFloatingNumbers) return;
        Vector2 screenPos = Camera.main.WorldToScreenPoint(worldPos);
        Instance.SpawnTextLabel("→ Cannery", new Color(0.95f, 0.62f, 0.25f), screenPos);
    }

    // Generic single-string label using the same pool + drift animation as reward labels.
    private void SpawnTextLabel(string text, Color color, Vector2 screenPos)
    {
        GameObject go = GetLabel();
        TextMeshProUGUI tmp = go.GetComponent<TextMeshProUGUI>();
        RectTransform rt = go.GetComponent<RectTransform>();

        tmp.fontSize = 30;
        tmp.fontStyle = FontStyles.Bold;
        tmp.text = text;
        tmp.color = color;

        Vector2 localPt = ToLocalPoint(screenPos);
        rt.anchoredPosition = localPt;
        Vector2 endPos = localPt + new Vector2(0, 120f);

        LeanTween.value(go, localPt, endPos, 1.2f)
            .setEaseOutQuad().setIgnoreTimeScale(true)
            .setOnUpdate((Vector2 p) => { if (rt != null) rt.anchoredPosition = p; });
        LeanTween.value(go, 1f, 0f, 0.4f)
            .setDelay(0.8f).setIgnoreTimeScale(true)
            .setOnUpdate((float a) => { if (tmp != null) tmp.alpha = a; })
            .setOnComplete(() => ReturnLabel(go));
    }

    private void SpawnLabel(List<CurrencyReward> rewards, Vector2 screenPos)
    {
        if (rewards.Count == 0) return;
        GameObject go = GetLabel();
        TextMeshProUGUI tmp = go.GetComponent<TextMeshProUGUI>();
        RectTransform rt = go.GetComponent<RectTransform>();

        tmp.fontSize = 36;
        tmp.fontStyle = FontStyles.Bold;

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
        // null camera is correct for ScreenSpaceOverlay canvas
        Vector2 localPt = ToLocalPoint(screenPos);
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
            .setOnComplete(() => ReturnLabel(go));
    }

    private void SpawnSpendLabel(int amount, Vector2 screenPos)
    {
        GameObject go = GetLabel();
        TextMeshProUGUI tmp = go.GetComponent<TextMeshProUGUI>();
        RectTransform rt = go.GetComponent<RectTransform>();

        tmp.fontSize = 32;
        tmp.fontStyle = FontStyles.Bold;
        tmp.text = $"-{amount}$";
        tmp.color = new Color(0.85f, 0.15f, 0.15f); // red

        Vector2 localPt = ToLocalPoint(screenPos);
        rt.anchoredPosition = localPt;

        // Drift DOWN (opposite of rewards) so spend reads differently from income.
        Vector2 endPos = localPt + new Vector2(0, -70f);
        LeanTween.value(go, localPt, endPos, 1.0f)
            .setEaseOutQuad().setIgnoreTimeScale(true)
            .setOnUpdate((Vector2 p) => { if (rt != null) rt.anchoredPosition = p; });
        LeanTween.value(go, 1f, 0f, 0.4f)
            .setDelay(0.6f).setIgnoreTimeScale(true)
            .setOnUpdate((float a) => { if (tmp != null) tmp.alpha = a; })
            .setOnComplete(() => ReturnLabel(go));
    }

    // ── Pool plumbing ─────────────────────────────────────────────────────

    // null camera is correct for a ScreenSpaceOverlay canvas.
    private Vector2 ToLocalPoint(Vector2 screenPos)
    {
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            canvas.GetComponent<RectTransform>(), screenPos, null, out Vector2 localPt);
        return localPt;
    }

    // Returns an active, fully-reset label ready to be configured & animated.
    private GameObject GetLabel()
    {
        GameObject go;
        if (idleLabels.Count > 0)
        {
            go = idleLabels.Dequeue();
        }
        else if (idleLabels.Count + activeLabels.Count < PoolCap)
        {
            go = CreateLabel();
        }
        else
        {
            // Pool exhausted: reclaim the oldest still-animating label. Its drift/
            // fade tweens are cancelled below (cancel does NOT fire setOnComplete,
            // so the stale ReturnLabel never runs for this reused object).
            go = activeLabels[0];
            activeLabels.RemoveAt(0);
        }

        // Kill any tween still keyed to this GameObject before reuse, then reset
        // every property the animations mutate so a recycled label starts clean.
        LeanTween.cancel(go);
        go.transform.localScale = Vector3.one;
        var tmp = go.GetComponent<TextMeshProUGUI>();
        if (tmp != null) tmp.alpha = 1f;
        go.SetActive(true);

        activeLabels.Add(go);
        return go;
    }

    private GameObject CreateLabel()
    {
        var go = new GameObject("FloatingLabel", typeof(RectTransform));
        go.transform.SetParent(canvas.transform, false);

        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.raycastTarget = false;
        if (font != null) tmp.font = font;

        var rt = go.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(200, 80);
        rt.pivot = new Vector2(0.5f, 0f);
        return go;
    }

    // Called from a fade tween's setOnComplete: park the label back in the pool.
    private void ReturnLabel(GameObject go)
    {
        if (go == null) return; // destroyed (e.g. manager torn down) — nothing to park
        activeLabels.Remove(go);
        go.SetActive(false);
        idleLabels.Enqueue(go);
    }

    private static string FormatReward(CurrencyReward r)
    {
        return r.type switch
        {
            CurrencyType.Money => $"+{r.amount}$",
            CurrencyType.Coins => $"+{r.amount}G",
            CurrencyType.Gems  => $"+{r.amount}\u2736",
            CurrencyType.Compost => $"+{r.amount}\U0001F331",
            CurrencyType.Wood => $"+{r.amount}\U0001FAB5",
            _ => $"+{r.amount}"
        };
    }

    private static Color GetColor(CurrencyType t)
    {
        return t switch
        {
            CurrencyType.Money => new Color(0.039f, 0.220f, 0.051f), // #0A380D very dark green
            CurrencyType.Coins => new Color(1f, 0.843f, 0f),         // #FFD700
            CurrencyType.Gems  => new Color(0.659f, 0.333f, 0.969f), // #A855F7
            CurrencyType.Compost => new Color(0.439f, 0.788f, 0.392f), // #70C964 (compost green)
            CurrencyType.Wood => new Color(0.647f, 0.435f, 0.239f),    // #A56F3D warm log brown
            _ => Color.white
        };
    }
}
