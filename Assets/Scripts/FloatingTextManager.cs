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
        // null camera is correct for ScreenSpaceOverlay canvas
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
            CurrencyType.Gems  => $"+{r.amount}\u2736",
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
