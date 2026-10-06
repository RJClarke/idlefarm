using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

public enum RewardCurrency { Coins, Gems }

public struct RewardLine
{
    public RewardCurrency currency;
    public int amount;
    public RewardLine(RewardCurrency currency, int amount) { this.currency = currency; this.amount = amount; }
}

/// <summary>One chest to open: the rewards to show, an optional banner, a grant hook that fires once
/// when the chest bursts open (or early if the app is backgrounded first), and a close hook.</summary>
public sealed class ChestRevealRequest
{
    public List<RewardLine> lines = new List<RewardLine>();
    public string banner;
    public Action onOpened;
    public Action onClosed;
}

/// <summary>
/// The Free Gift / purchase chest (spec §5). Closed chest drops in. Taps 1-2 shake it, tap 3 bursts it
/// open: sprite swaps, rays spin, icons spray, BIG numbers count up. The bottom button opens it in one
/// press ("Claim") and closes it in the next ("Collect"), so mashing is 2 presses (~1s).
/// Requests queue; each onOpened fires exactly once. Self-creating, sort 1400.
/// </summary>
public sealed class ChestRevealUITK : MonoBehaviour
{
    private const int SortOrder = 1400;
    private const float ChestSize = 256f;     // the 32px sprite at 8x
    private const float CollectGuard = 0.25f; // ignore the same mash that opened it
    private const string SprayClass = "chest-spray";

    /// <summary>SFX / haptics hook points: "tap", "burst", "collect". No-op until those systems exist.</summary>
    public static event Action<string> OnFeedback;

    private static ChestRevealUITK instance;
    public static bool IsShowing => instance != null && instance.current != null;

    private readonly Queue<ChestRevealRequest> queue = new Queue<ChestRevealRequest>();
    private ChestRevealRequest current;
    private bool opened, closing, grantFired;
    private int taps;
    private float openedAt;

    private PanelSettings runtimeSettings;
    private VisualElement root, layer, stage, chest, glow, linesColumn;
    private RaysElement rays;
    private Label banner, hint;
    private Button actionButton;
    private IVisualElementScheduledItem spin;

    public static void Show(ChestRevealRequest request)
    {
        if (request == null) return;
        if (instance == null) instance = new GameObject("ChestRevealUITK").AddComponent<ChestRevealUITK>();
        instance.queue.Enqueue(request);
        if (instance.current == null) instance.Next();
    }

    // No grant here: a scene unload (dev "Reset Save") would save over half-destroyed managers.
    // Pause/Quit below cover real backgrounding and exits.
    private void OnDestroy()
    {
        if (instance == this) instance = null;
        if (runtimeSettings != null) Destroy(runtimeSettings);
    }

    // Backgrounding with an unopened chest: grant now so a killed app can't eat the reward.
    private void OnApplicationPause(bool paused) { if (paused) FireGrant(); }
    private void OnApplicationQuit() => FireGrant();

    private void FireGrant()
    {
        if (current == null || grantFired) return;
        grantFired = true;
        try { current.onOpened?.Invoke(); }
        catch (Exception e) { Debug.LogException(e); }
    }

    private bool EnsureBuilt()
    {
        if (root != null) return true;
        root = MonetizationUI.CreateRoot(this, SortOrder, "ChestReveal", out runtimeSettings);
        if (root == null) return false;

        layer = new VisualElement { name = "chest-layer" };
        MonetizationUI.Fill(layer);
        layer.style.backgroundColor = MonetizationUI.Dim(0.75f);
        layer.style.alignItems = Align.Center;
        layer.style.justifyContent = Justify.Center;
        layer.style.display = DisplayStyle.None;
        root.Add(layer);

        banner = MonetizationUI.PixelText("", 1, MonetizationUI.Gold);
        banner.style.unityTextAlign = TextAnchor.MiddleCenter;
        banner.style.marginBottom = 20;
        layer.Add(banner);

        stage = new VisualElement();
        stage.style.width = 640; stage.style.height = 520;
        stage.style.alignItems = Align.Center;
        stage.style.justifyContent = Justify.Center;
        layer.Add(stage);

        rays = new RaysElement();
        rays.style.position = Position.Absolute;
        rays.style.width = 640; rays.style.height = 640;
        rays.style.left = 0; rays.style.top = -60;
        stage.Add(rays);

        glow = new VisualElement { pickingMode = PickingMode.Ignore };
        glow.style.position = Position.Absolute;
        glow.style.width = 360; glow.style.height = 360;
        glow.style.left = 140; glow.style.top = 80;
        glow.style.backgroundColor = new Color(1f, 0.85f, 0.4f, 0.35f);
        MonetizationUI.Radius(glow, 180);
        stage.Add(glow);

        chest = new VisualElement { name = "chest" };
        chest.style.width = ChestSize; chest.style.height = ChestSize;
        chest.style.backgroundSize = new BackgroundSize(BackgroundSizeType.Contain);
        chest.RegisterCallback<ClickEvent>(_ => OnChestTapped());
        stage.Add(chest);

        hint = MonetizationUI.Text("Tap to open!", 34, MonetizationUI.Cream, bold: true);
        hint.style.marginTop = 6;
        layer.Add(hint);

        linesColumn = new VisualElement();
        linesColumn.style.alignItems = Align.Center;
        linesColumn.style.minHeight = 240;
        layer.Add(linesColumn);

        actionButton = MonetizationUI.BrownButton("Claim", OnButton, 36);
        actionButton.name = "chest-action";
        actionButton.style.minWidth = 360;
        actionButton.style.marginTop = 24;
        layer.Add(actionButton);

        // Tapping the dim backdrop behaves like the chest (opens, then collects).
        layer.RegisterCallback<ClickEvent>(e => { if (e.target == layer) OnChestTapped(); });
        return true;
    }

    private void Next()
    {
        current = queue.Count > 0 ? queue.Dequeue() : null;
        if (current == null) { Hide(); return; }
        if (!EnsureBuilt())
        {
            FireGrant();
            ChestRevealRequest failed = current;
            current = null;
            try { failed.onClosed?.Invoke(); } catch (Exception e) { Debug.LogException(e); }
            Next();
            return;
        }

        opened = false; closing = false; grantFired = false; taps = 0;
        MonetizationArt art = MonetizationArt.Instance;
        chest.style.backgroundImage = art != null && art.chestClosed != null
            ? new StyleBackground(art.chestClosed) : new StyleBackground(StyleKeyword.None);
        chest.style.rotate = new Rotate(new Angle(0));
        chest.style.scale = new Scale(Vector2.one);
        rays.style.opacity = 0; glow.style.opacity = 0;
        banner.text = current.banner ?? "";
        banner.style.display = string.IsNullOrEmpty(current.banner) ? DisplayStyle.None : DisplayStyle.Flex;
        hint.style.display = DisplayStyle.Flex;
        linesColumn.Clear();
        // Spray icons from a chest that closed <0.7s ago lose their removal when tweens are cancelled below.
        foreach (VisualElement leftover in stage.Query(className: SprayClass).ToList()) leftover.RemoveFromHierarchy();
        actionButton.text = "Claim";
        layer.style.opacity = 1;
        layer.style.display = DisplayStyle.Flex;
        root.pickingMode = PickingMode.Position;

        // Drop in from above with a bounce.
        LeanTween.cancel(gameObject);
        LeanTween.value(gameObject, -420f, 0f, 0.45f).setEaseOutBounce().setIgnoreTimeScale(true)
            .setOnUpdate((float y) => chest.style.translate = new Translate(0, y));
    }

    private void OnChestTapped()
    {
        if (current == null || closing) return;
        if (opened) { if (Time.unscaledTime - openedAt > CollectGuard) Collect(); return; }
        taps++;
        OnFeedback?.Invoke("tap");
        if (taps >= 3) Open(); else Shake(taps);
    }

    private void OnButton()
    {
        if (current == null || closing) return;
        if (!opened) Open();
        else if (Time.unscaledTime - openedAt > CollectGuard) Collect();
    }

    private void Shake(int strength)
    {
        float amp = strength == 1 ? 7f : 14f;
        LeanTween.value(gameObject, 0f, 1f, 0.3f).setIgnoreTimeScale(true).setOnUpdate((float t) =>
        {
            chest.style.rotate = new Rotate(new Angle(Mathf.Sin(t * Mathf.PI * 6f) * amp * (1f - t)));
            float squash = 1f + 0.08f * Mathf.Sin(t * Mathf.PI) * strength;
            chest.style.scale = new Scale(new Vector2(squash, 2f - squash));
        });
        if (strength == 2) LeanTween.value(gameObject, 0f, 0.6f, 0.3f).setIgnoreTimeScale(true).setOnUpdate((float a) => glow.style.opacity = a);
    }

    private void Open()
    {
        opened = true;
        openedAt = Time.unscaledTime;
        OnFeedback?.Invoke("burst");
        FireGrant();

        MonetizationArt art = MonetizationArt.Instance;
        if (art != null && art.chestOpen != null) chest.style.backgroundImage = new StyleBackground(art.chestOpen);
        chest.style.rotate = new Rotate(new Angle(0));
        hint.style.display = DisplayStyle.None;
        actionButton.text = "Collect";

        LeanTween.value(gameObject, 1.35f, 1f, 0.35f).setEaseOutBack().setIgnoreTimeScale(true)
            .setOnUpdate((float s) => chest.style.scale = new Scale(new Vector2(s, s)));
        rays.style.opacity = 1; glow.style.opacity = 0.8f;
        float angle = 0f;
        spin?.Pause();
        spin = rays.schedule.Execute(() => { angle += 1.2f; rays.style.rotate = new Rotate(new Angle(angle)); }).Every(16);

        Spray(art);
        for (int i = 0; i < current.lines.Count; i++) AddLine(current.lines[i], i * 0.12f, art);
    }

    private void Spray(MonetizationArt art)
    {
        for (int i = 0; i < 10; i++)
        {
            Sprite s = art == null ? null : (i % 2 == 0 ? art.coinIcon : art.gemIcon);
            VisualElement p = MonetizationUI.Icon(s, 54);
            p.AddToClassList(SprayClass);
            p.style.position = Position.Absolute;
            p.style.left = 293; p.style.top = 233;
            stage.Add(p);
            float a = UnityEngine.Random.Range(-160f, -20f) * Mathf.Deg2Rad;
            Vector2 to = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * UnityEngine.Random.Range(180f, 290f);
            LeanTween.value(gameObject, 0f, 1f, 0.7f).setEaseOutQuad().setIgnoreTimeScale(true)
                .setOnUpdate((float t) =>
                {
                    p.style.translate = new Translate(to.x * t, to.y * t + 260f * t * t);
                    p.style.opacity = 1f - t;
                })
                .setOnComplete(() => p.RemoveFromHierarchy());
        }
    }

    private void AddLine(RewardLine line, float delay, MonetizationArt art)
    {
        var row = new VisualElement();
        row.style.flexDirection = FlexDirection.Row;
        row.style.alignItems = Align.Center;
        row.style.marginTop = 8;
        row.style.scale = new Scale(Vector2.zero);
        Sprite icon = art == null ? null : (line.currency == RewardCurrency.Gems ? art.gemIcon : art.coinIcon);
        row.Add(MonetizationUI.Icon(icon, 84));
        // Default SDF font, not the bitmap pixel font: the panel scales ~1.12x on tall phones, which breaks a
        // raster font at 2x. A walnut outline keeps the big number readable over the rays.
        Label amount = MonetizationUI.Text("+0", 92, MonetizationUI.Cream, bold: true);
        amount.style.unityTextOutlineWidth = 3f;
        amount.style.unityTextOutlineColor = MonetizationUI.WalnutDark;
        amount.style.marginLeft = 16;
        row.Add(amount);
        row.userData = line;
        linesColumn.Add(row);

        LeanTween.value(gameObject, 0f, 1f, 0.3f).setDelay(delay).setEaseOutBack().setIgnoreTimeScale(true)
            .setOnUpdate((float s) => row.style.scale = new Scale(new Vector2(s, s)));
        LeanTween.value(gameObject, 0f, line.amount, 0.6f).setDelay(delay).setEaseOutCubic().setIgnoreTimeScale(true)
            .setOnUpdate((float v) => amount.text = "+" + Mathf.RoundToInt(v).ToString("N0"))
            .setOnComplete(() => amount.text = "+" + line.amount.ToString("N0"));
    }

    private void Collect()
    {
        closing = true;
        OnFeedback?.Invoke("collect");
        Rect panel = root.worldBound;
        foreach (VisualElement row in linesColumn.Children())
        {
            if (!(row.userData is RewardLine line)) continue;
            string rowName = line.currency == RewardCurrency.Gems ? "row-gems" : "row-coins";
            Vector2 delta = new Vector2(0, -600);
            if (TopBarUITK.Instance != null && TopBarUITK.Instance.TryGetRowCenterNormalized(rowName, out Vector2 n))
            {
                Vector2 target = new Vector2(panel.x + n.x * panel.width, panel.y + n.y * panel.height);
                delta = target - row.worldBound.center;
            }
            VisualElement r = row;
            LeanTween.value(gameObject, 0f, 1f, 0.35f).setEaseInCubic().setIgnoreTimeScale(true).setOnUpdate((float t) =>
            {
                r.style.translate = new Translate(delta.x * t, delta.y * t);
                float s = 1f - 0.7f * t;
                r.style.scale = new Scale(new Vector2(s, s));
            });
        }
        LeanTween.value(gameObject, 1f, 0f, 0.15f).setDelay(0.3f).setIgnoreTimeScale(true)
            .setOnUpdate((float a) => layer.style.opacity = a)
            .setOnComplete(Finish);
    }

    private void Finish()
    {
        spin?.Pause();
        ChestRevealRequest done = current;
        current = null;
        layer.style.display = DisplayStyle.None;
        root.pickingMode = PickingMode.Ignore;
        try { done?.onClosed?.Invoke(); } catch (Exception e) { Debug.LogException(e); }
        if (current == null) Next(); // onClosed may already have shown another chest
    }

    private void Hide()
    {
        if (layer != null) layer.style.display = DisplayStyle.None;
        if (root != null) root.pickingMode = PickingMode.Ignore;
    }
}
