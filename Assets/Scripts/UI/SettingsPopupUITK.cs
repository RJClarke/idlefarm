using System;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

[RequireComponent(typeof(UIDocument))]
public class SettingsPopupUITK : MonoBehaviour
{
    public static SettingsPopupUITK Instance { get; private set; }

    [Header("Templates")]
    [SerializeField] private VisualTreeAsset sectionTemplate;
    [SerializeField] private VisualTreeAsset toggleRowTemplate;
    [SerializeField] private VisualTreeAsset sliderRowTemplate;
    [SerializeField] private VisualTreeAsset buttonRowTemplate;

    [Header("Dev Section")]
    [Tooltip("Show the Dev / Testing section. Auto-hidden in non-dev builds.")]
    [SerializeField] private bool showDevSection = true;

    private UIDocument document;
    private VisualElement root;
    private VisualElement popupRoot;
    private VisualElement backdrop;
    private Button closeButton;
    private ScrollView sectionList;

    private bool isOpen;
    public bool IsOpen => isOpen;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        document = GetComponent<UIDocument>();
        SettingsManager.EnsureLoaded();
    }

    private void OnEnable()
    {
        CacheElements();
        WireCallbacks();
    }

    private void CacheElements()
    {
        root = document.rootVisualElement;
        if (root == null) { Debug.LogError("[SettingsPopupUITK] rootVisualElement is null"); return; }
        root.pickingMode = PickingMode.Ignore;

        popupRoot   = root.Q<VisualElement>("popup-root");
        backdrop    = root.Q<VisualElement>("backdrop");
        closeButton = root.Q<Button>("close-button");
        sectionList = root.Q<ScrollView>("section-list");
    }

    private void WireCallbacks()
    {
        if (backdrop != null)    backdrop.RegisterCallback<ClickEvent>(_ => Close());
        if (closeButton != null) closeButton.RegisterCallback<ClickEvent>(_ => Close());
    }

    public void Open()
    {
        if (isOpen) return;
        isOpen = true;
        if (popupRoot != null)
        {
            popupRoot.style.display = DisplayStyle.Flex;
            popupRoot.schedule.Execute(() => popupRoot.AddToClassList("open")).StartingIn(0);
        }
        RefreshAll();
    }

    public void Close()
    {
        if (!isOpen) return;
        isOpen = false;
        if (popupRoot == null) return;
        popupRoot.RemoveFromClassList("open");
        popupRoot.schedule.Execute(() =>
        {
            if (isOpen) return;
            popupRoot.style.display = DisplayStyle.None;
        }).StartingIn(260);
    }

    public void RefreshAll()
    {
        if (sectionList == null) return;
        sectionList.Clear();

        BuildAudioSection();
        BuildGameplaySection();
        BuildAccountSection();
        if (ShouldShowDevSection()) BuildDevSection();
    }

    private bool ShouldShowDevSection()
    {
        if (!showDevSection) return false;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        return true;
#else
        return false;
#endif
    }

    // ─── Sections ────────────────────────────────────────────

    private void BuildAudioSection()
    {
        VisualElement rows = SpawnSection("Audio");
        if (rows == null) return;

        SpawnSliderRow(rows, "Master Volume", "Overall game volume",
            SettingsManager.MasterVolume, v => SettingsManager.MasterVolume = v);

        SpawnSliderRow(rows, "Music Volume", "Background music (stub)",
            SettingsManager.MusicVolume, v => SettingsManager.MusicVolume = v);

        SpawnSliderRow(rows, "SFX Volume", "Sound effects (stub)",
            SettingsManager.SfxVolume, v => SettingsManager.SfxVolume = v);

        SpawnToggleRow(rows, "Mute All", "Silence everything",
            SettingsManager.MuteAll, v => SettingsManager.MuteAll = v);
    }

    private void BuildGameplaySection()
    {
        VisualElement rows = SpawnSection("Gameplay");
        if (rows == null) return;

        SpawnToggleRow(rows, "Floating Numbers", "Show +$ popups on harvest",
            SettingsManager.ShowFloatingNumbers, v => SettingsManager.ShowFloatingNumbers = v);

        SpawnToggleRow(rows, "Haptics", "Vibration feedback (stub)",
            SettingsManager.Haptics, v => SettingsManager.Haptics = v);

        SpawnToggleRow(rows, "Reduce Motion", "Less screen shake & animation (stub)",
            SettingsManager.ReduceMotion, v => SettingsManager.ReduceMotion = v);

        SpawnToggleRow(rows, "Low Power Mode", "Lower frame rate to save battery (stub)",
            SettingsManager.LowPowerMode, v => SettingsManager.LowPowerMode = v);

        SpawnButtonRow(rows, "Language", "Currently: English", "Change",
            () => Debug.Log("[Settings] Language picker stub"));
    }

    private void BuildAccountSection()
    {
        VisualElement rows = SpawnSection("Account");
        if (rows == null) return;

        SpawnButtonRow(rows, "Sign In", "Connect a Google or Apple account", "Sign In",
            () => Debug.Log("[Settings] Sign-in stub"));

        SpawnToggleRow(rows, "Cloud Sync", "Sync save across devices (stub)",
            false, v => Debug.Log($"[Settings] Cloud sync toggled: {v}"));

        SpawnButtonRow(rows, "Display Name", "Currently: Player", "Edit",
            () => Debug.Log("[Settings] Display name editor stub"));
    }

    private void BuildDevSection()
    {
        VisualElement rows = SpawnSection("Dev / Testing");
        if (rows == null) return;

        SpawnButtonRow(rows, "Grant Coins", "+10,000 coins", "+10k",
            () =>
            {
                if (CurrencyManager.Instance != null) CurrencyManager.Instance.AddCoins(10_000);
            });

        SpawnButtonRow(rows, "Grant Gems", "+1,000 gems", "+1k",
            () =>
            {
                if (CurrencyManager.Instance != null) CurrencyManager.Instance.AddGems(1_000);
            });

        SpawnButtonRow(rows, "Force End Run", "Ends the active run immediately", "End",
            () =>
            {
                if (RunManager.Instance != null && RunManager.Instance.IsRunActive)
                    RunManager.Instance.EndRun();
                else
                    Debug.Log("[Settings] No active run to end.");
            });

        SpawnToggleRow(rows, "Show FPS", "Display FPS counter overlay (stub)",
            SettingsManager.ShowFps, v => SettingsManager.ShowFps = v);

        SpawnButtonRow(rows, "Reset Save", "⚠ Deletes save & reloads scene", "Reset",
            () =>
            {
                if (SaveManager.Instance != null) SaveManager.Instance.DeleteSave();
                PlayerPrefs.DeleteAll();
                PlayerPrefs.Save();
                Debug.LogWarning("[Settings] Save reset — reloading scene.");
                SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
            },
            requireConfirm: true);
    }

    // ─── Row spawn helpers ─────────────────────────────────

    private VisualElement SpawnSection(string title)
    {
        if (sectionTemplate == null) return null;
        TemplateContainer section = sectionTemplate.Instantiate();
        sectionList.Add(section);
        Label header = section.Q<Label>("section-title");
        if (header != null) header.text = title;
        return section.Q<VisualElement>("section-rows");
    }

    private void SpawnToggleRow(VisualElement parent, string title, string desc, bool initialValue, Action<bool> onChanged)
    {
        if (toggleRowTemplate == null) return;
        TemplateContainer row = toggleRowTemplate.Instantiate();
        parent.Add(row);

        Label titleLabel = row.Q<Label>("row-title");
        Label descLabel  = row.Q<Label>("row-desc");
        Toggle toggle    = row.Q<Toggle>("row-toggle");

        if (titleLabel != null) titleLabel.text = title;
        if (descLabel != null)  descLabel.text  = desc;
        if (toggle != null)
        {
            toggle.SetValueWithoutNotify(initialValue);
            toggle.RegisterValueChangedCallback(evt => onChanged?.Invoke(evt.newValue));
        }
    }

    private void SpawnSliderRow(VisualElement parent, string title, string desc, float initialValue, Action<float> onChanged)
    {
        if (sliderRowTemplate == null) return;
        TemplateContainer row = sliderRowTemplate.Instantiate();
        parent.Add(row);

        Label titleLabel = row.Q<Label>("row-title");
        Label valueLabel = row.Q<Label>("row-value");
        Slider slider    = row.Q<Slider>("row-slider");

        if (titleLabel != null) titleLabel.text = title;
        if (valueLabel != null) valueLabel.text = FormatPercent(initialValue);
        if (slider != null)
        {
            slider.lowValue = 0f;
            slider.highValue = 1f;
            slider.SetValueWithoutNotify(initialValue);
            slider.RegisterValueChangedCallback(evt =>
            {
                if (valueLabel != null) valueLabel.text = FormatPercent(evt.newValue);
                onChanged?.Invoke(evt.newValue);
            });
        }
    }

    private void SpawnButtonRow(VisualElement parent, string title, string desc, string buttonText,
                                 Action onClick, bool requireConfirm = false, string confirmText = "Tap to confirm",
                                 float confirmTimeoutMs = 3000f)
    {
        if (buttonRowTemplate == null) return;
        TemplateContainer row = buttonRowTemplate.Instantiate();
        parent.Add(row);

        Label titleLabel = row.Q<Label>("row-title");
        Label descLabel  = row.Q<Label>("row-desc");
        Button button    = row.Q<Button>("row-button");

        if (titleLabel != null) titleLabel.text = title;
        if (descLabel != null)  descLabel.text  = desc;
        if (button == null) return;

        button.text = buttonText;

        if (!requireConfirm)
        {
            button.RegisterCallback<ClickEvent>(_ => onClick?.Invoke());
            return;
        }

        // Tap-to-confirm: first tap arms (red, swapped label, auto-revert); second tap fires.
        IVisualElementScheduledItem revertScheduled = null;
        bool armed = false;

        void Disarm()
        {
            armed = false;
            button.text = buttonText;
            button.RemoveFromClassList("settings-row-button--confirm");
            revertScheduled?.Pause();
            revertScheduled = null;
        }

        button.RegisterCallback<ClickEvent>(_ =>
        {
            if (!armed)
            {
                armed = true;
                button.text = confirmText;
                button.AddToClassList("settings-row-button--confirm");
                revertScheduled = button.schedule.Execute(Disarm).StartingIn((long)confirmTimeoutMs);
                return;
            }
            Disarm();
            onClick?.Invoke();
        });
    }

    private static string FormatPercent(float v) => $"{Mathf.RoundToInt(v * 100f)}%";
}
