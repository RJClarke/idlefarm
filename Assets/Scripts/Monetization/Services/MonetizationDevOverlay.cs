#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>The fake ad + fake purchase screens (dev only). Self-creating; sort 1900 sits above popups
/// and below toasts (2000).</summary>
public sealed class MonetizationDevOverlay : MonoBehaviour
{
    private const int SortOrder = 1900;
    private const float AdSeconds = 3f;

    private static MonetizationDevOverlay instance;
    private PanelSettings runtimeSettings;
    private VisualElement root, layer;

    private static MonetizationDevOverlay Ensure()
    {
        if (instance != null) return instance;
        instance = new GameObject("MonetizationDevOverlay").AddComponent<MonetizationDevOverlay>();
        return instance;
    }

    private void OnDestroy()
    {
        if (instance == this) instance = null;
        if (runtimeSettings != null) Destroy(runtimeSettings);
    }

    private bool EnsureRoot()
    {
        if (root != null) return true;
        root = MonetizationUI.CreateRoot(this, SortOrder, "MonetizationDevOverlay", out runtimeSettings);
        return root != null;
    }

    /// <summary>3-second fake ad. done(true) when it runs out, done(false) on Close early.</summary>
    public static void ShowAd(Action<bool> done)
    {
        MonetizationDevOverlay o = Ensure();
        if (!o.EnsureRoot()) { done?.Invoke(true); return; } // no UI available: behave like a finished ad
        o.Open();

        var panel = o.Panel();
        panel.Add(MonetizationUI.Text("Test Ad", 44, MonetizationUI.Cream, bold: true));
        Label count = MonetizationUI.Text(AdSeconds.ToString("0"), 120, MonetizationUI.Gold, bold: true);
        panel.Add(count);

        bool finished = false;
        IVisualElementScheduledItem tick = null;
        float start = Time.unscaledTime;
        void Finish(bool completed)
        {
            if (finished) return;
            finished = true;
            tick?.Pause();
            o.CloseLayer();
            done?.Invoke(completed);
        }
        Button close = MonetizationUI.BrownButton("Close early", () => Finish(false), 28);
        close.name = "dev-ad-close";
        close.style.marginTop = 30;
        panel.Add(close);

        tick = count.schedule.Execute(() =>
        {
            float left = AdSeconds - (Time.unscaledTime - start);
            count.text = Mathf.CeilToInt(Mathf.Max(0f, left)).ToString();
            if (left <= 0f) Finish(true);
        }).Every(100);
    }

    /// <summary>"[Test Store] Buy X for $Y?" with Confirm / Fail / Cancel.</summary>
    public static void ShowPurchase(string title, string price, Action<PurchaseOutcome> done)
    {
        MonetizationDevOverlay o = Ensure();
        if (!o.EnsureRoot()) { done?.Invoke(PurchaseOutcome.Unavailable); return; }
        o.Open();

        var panel = o.Panel();
        panel.Add(MonetizationUI.Text("[Test Store]", 30, MonetizationUI.Gold, bold: true));
        Label question = MonetizationUI.Text($"Buy {title} for {price}?", 36, MonetizationUI.Cream);
        question.style.unityTextAlign = TextAnchor.MiddleCenter;
        question.style.marginTop = 12; question.style.marginBottom = 26;
        panel.Add(question);

        bool answered = false;
        void Answer(PurchaseOutcome outcome)
        {
            if (answered) return;
            answered = true;
            o.CloseLayer();
            done?.Invoke(outcome);
        }
        var row = new VisualElement();
        row.style.flexDirection = FlexDirection.Row;
        row.style.justifyContent = Justify.Center;
        Button confirm = MonetizationUI.BrownButton("Confirm", () => Answer(PurchaseOutcome.Success), 28);
        Button fail = MonetizationUI.BrownButton("Fail", () => Answer(PurchaseOutcome.Failed), 28);
        Button cancel = MonetizationUI.BrownButton("Cancel", () => Answer(PurchaseOutcome.Cancelled), 28);
        confirm.name = "dev-buy-confirm"; fail.name = "dev-buy-fail"; cancel.name = "dev-buy-cancel";
        fail.style.marginLeft = 14; cancel.style.marginLeft = 14;
        row.Add(confirm); row.Add(fail); row.Add(cancel);
        panel.Add(row);
    }

    private void Open()
    {
        CloseLayer();
        layer = new VisualElement();
        MonetizationUI.Fill(layer);
        layer.style.backgroundColor = MonetizationUI.Dim(0.85f);
        layer.style.alignItems = Align.Center;
        layer.style.justifyContent = Justify.Center;
        root.Add(layer);
        root.pickingMode = PickingMode.Position;
    }

    private VisualElement Panel()
    {
        var panel = new VisualElement();
        panel.style.alignItems = Align.Center;
        panel.style.paddingLeft = 40; panel.style.paddingRight = 40;
        panel.style.paddingTop = 36; panel.style.paddingBottom = 36;
        panel.style.backgroundColor = MonetizationUI.WalnutDark;
        MonetizationUI.Radius(panel, 18);
        panel.style.maxWidth = 900;
        layer.Add(panel);
        return panel;
    }

    private void CloseLayer()
    {
        if (layer != null) { layer.RemoveFromHierarchy(); layer = null; }
        if (root != null) root.pickingMode = PickingMode.Ignore;
    }
}
#endif
