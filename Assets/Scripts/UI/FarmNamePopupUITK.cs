using UnityEngine;
using UnityEngine.UIElements;

/// <summary>First-run farm-naming modal. Required 3–30 char name, prefilled with a random
/// playful suggestion, re-rollable via the dice button. On save: stores the name, marks the
/// onboarding flag, and delivers the welcome letter. Reused by Settings for renaming.</summary>
[RequireComponent(typeof(UIDocument))]
public class FarmNamePopupUITK : MonoBehaviour
{
    public static FarmNamePopupUITK Instance { get; private set; }

    private UIDocument document;
    private VisualElement popupRoot;
    private TextField nameField;
    private Button diceButton;
    private Button saveButton;
    private Label errorLabel;

    private bool isOpen;
    private bool isFirstRun;
    private readonly System.Random rng = new System.Random();

    public bool IsOpen => isOpen;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        document = GetComponent<UIDocument>();
    }

    private void OnDestroy() { if (Instance == this) Instance = null; }

    private void Start()
    {
        CacheElements();
        WireCallbacks();
        // Auto-open on a fresh game: name not yet chosen.
        if (NarrativeManager.Instance != null && !NarrativeManager.Instance.HasFired("onboarding_named"))
            Open(isFirstRun: true);
        else if (popupRoot != null)
            popupRoot.style.display = DisplayStyle.None;
    }

    private void CacheElements()
    {
        var root = document.rootVisualElement;
        if (root == null) { Debug.LogError("[FarmNamePopupUITK] rootVisualElement is null"); return; }
        root.pickingMode = PickingMode.Ignore;
        popupRoot   = root.Q<VisualElement>("popup-root");
        nameField   = root.Q<TextField>("name-field");
        diceButton  = root.Q<Button>("dice-button");
        saveButton  = root.Q<Button>("save-button");
        errorLabel  = root.Q<Label>("error");
    }

    private void WireCallbacks()
    {
        diceButton?.RegisterCallback<ClickEvent>(_ => Reroll());
        saveButton?.RegisterCallback<ClickEvent>(_ => OnSave());
        nameField?.RegisterValueChangedCallback(_ => Validate());
    }

    public void Open(bool isFirstRun)
    {
        this.isFirstRun = isFirstRun;
        isOpen = true;
        if (nameField != null)
        {
            string current = NarrativeManager.Instance != null ? NarrativeManager.Instance.FarmName : "";
            string initial = isFirstRun || string.IsNullOrEmpty(current)
                ? FarmNameSuggestions.Random(rng)
                : current;
            nameField.SetValueWithoutNotify(initial);
        }
        Validate();
        if (popupRoot != null) popupRoot.style.display = DisplayStyle.Flex;
    }

    public void Close()
    {
        isOpen = false;
        if (popupRoot != null) popupRoot.style.display = DisplayStyle.None;
    }

    private void Reroll()
    {
        nameField?.SetValueWithoutNotify(FarmNameSuggestions.Random(rng));
        Validate();
    }

    private void Validate()
    {
        bool valid = FarmName.IsValid(nameField?.value);
        if (saveButton != null) saveButton.SetEnabled(valid);
        if (errorLabel != null)
            errorLabel.text = valid ? "" : $"Name must be {FarmName.Min}–{FarmName.Max} characters.";
    }

    private void OnSave()
    {
        if (!FarmName.IsValid(nameField?.value)) return;
        NarrativeManager.Instance?.SetFarmName(nameField.value);

        if (isFirstRun && NarrativeManager.Instance != null
            && NarrativeManager.Instance.MarkFired("onboarding_named"))
        {
            InboxManager.Instance?.Deliver("welcome");
        }
        Close();
    }
}
