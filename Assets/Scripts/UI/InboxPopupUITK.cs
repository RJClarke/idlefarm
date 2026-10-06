using UnityEngine;
using UnityEngine.UIElements;

/// <summary>Mailbox UI: a list of received letters → a detail view per letter with an
/// optional Claim (reward) and CTA (navigation). Reads content from InboxManager's
/// catalog; resolves {farmName} tokens at display time.</summary>
[RequireComponent(typeof(UIDocument))]
public class InboxPopupUITK : MonoBehaviour
{
    public static InboxPopupUITK Instance { get; private set; }

    private UIDocument document;
    private VisualElement popupRoot, listView, detailView, portrait, backdrop, rewardItems, rewardItemsList;
    private Button backButton, closeButton, claimButton, ctaButton;
    private Label headerTitle, senderName, detailSubject, detailBody;

    private bool isOpen;
    private string currentLetterId;
    public bool IsOpen => isOpen;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        document = GetComponent<UIDocument>();
    }

    private void OnDestroy()
    {
        if (InboxManager.Instance != null) InboxManager.Instance.OnInboxChanged -= OnInboxChanged;
        if (Instance == this) Instance = null;
    }

    private void OnEnable() { CacheElements(); WireCallbacks(); }

    private void Start()
    {
        if (InboxManager.Instance != null) InboxManager.Instance.OnInboxChanged += OnInboxChanged;
        if (popupRoot != null) popupRoot.style.display = DisplayStyle.None;
    }

    private void CacheElements()
    {
        var root = document.rootVisualElement;
        if (root == null) { Debug.LogError("[InboxPopupUITK] rootVisualElement is null"); return; }
        root.pickingMode = PickingMode.Ignore;
        popupRoot     = root.Q<VisualElement>("popup-root");
        backdrop      = root.Q<VisualElement>("backdrop");
        listView      = root.Q<ScrollView>("list-view");
        detailView    = root.Q<VisualElement>("detail-view");
        rewardItems   = root.Q<VisualElement>("reward-items");
        rewardItemsList = root.Q<VisualElement>("reward-items-list");
        portrait      = root.Q<VisualElement>("portrait");
        backButton    = root.Q<Button>("back-button");
        closeButton   = root.Q<Button>("close-button");
        claimButton   = root.Q<Button>("claim-button");
        ctaButton     = root.Q<Button>("cta-button");
        headerTitle   = root.Q<Label>("header-title");
        senderName    = root.Q<Label>("sender-name");
        detailSubject = root.Q<Label>("detail-subject");
        detailBody    = root.Q<Label>("detail-body");
    }

    private void WireCallbacks()
    {
        backdrop?.RegisterCallback<ClickEvent>(_ => Close());
        closeButton?.RegisterCallback<ClickEvent>(_ => Close());
        backButton?.RegisterCallback<ClickEvent>(_ => ShowList());
        claimButton?.RegisterCallback<ClickEvent>(_ => OnClaim());
        ctaButton?.RegisterCallback<ClickEvent>(_ => OnCta());
    }

    public void Open() { isOpen = true; if (popupRoot != null) popupRoot.style.display = DisplayStyle.Flex; ShowList(); }
    public void Close()
    {
        isOpen = false;
        if (popupRoot != null) popupRoot.style.display = DisplayStyle.None;
        OnboardingTutorials.OnInboxClosed();
    }

    private void OnInboxChanged()
    {
        if (isOpen && detailView != null && detailView.style.display == DisplayStyle.None) ShowList();
    }

    private void ShowList()
    {
        currentLetterId = null;
        if (backButton != null) backButton.style.display = DisplayStyle.None;
        if (headerTitle != null) headerTitle.text = "Mailbox";
        if (detailView != null) detailView.style.display = DisplayStyle.None;
        if (listView != null) listView.style.display = DisplayStyle.Flex;
        if (listView == null) return;

        listView.Clear();
        var mgr = InboxManager.Instance;
        if (mgr == null || mgr.Entries.Count == 0)
        {
            var empty = new Label("No letters yet. Check back later!") { name = "empty" };
            empty.AddToClassList("inbox-empty");
            listView.Add(empty);
            return;
        }

        foreach (var entry in mgr.Entries)
        {
            var def = mgr.GetDef(entry.letterId);
            if (def == null) continue;

            var row = new VisualElement();
            row.AddToClassList("inbox-row");
            if (!entry.read) row.AddToClassList("inbox-row-unread");

            var textCol = new VisualElement(); textCol.AddToClassList("inbox-row-text");
            var s = new Label(def.senderName ?? ""); s.AddToClassList("inbox-row-sender");
            var subj = new Label(NarrativeText.Resolve(def.subject, NarrativeManager.Instance?.FarmName));
            subj.AddToClassList("inbox-row-subject");
            textCol.Add(s); textCol.Add(subj);
            row.Add(textCol);

            if (!entry.read)
            {
                var dot = new VisualElement(); dot.AddToClassList("inbox-row-dot");
                row.Add(dot);
            }

            string id = entry.letterId;
            row.RegisterCallback<ClickEvent>(_ => ShowDetail(id));
            listView.Add(row);
        }
    }

    private void ShowDetail(string letterId)
    {
        var mgr = InboxManager.Instance;
        var def = mgr?.GetDef(letterId);
        if (def == null) return;

        currentLetterId = letterId;

        if (backButton != null) backButton.style.display = DisplayStyle.Flex;
        if (headerTitle != null) headerTitle.text = "";
        if (listView != null) listView.style.display = DisplayStyle.None;
        if (detailView != null) detailView.style.display = DisplayStyle.Flex;

        string farm = NarrativeManager.Instance?.FarmName;
        if (senderName != null) senderName.text = def.senderName ?? "";
        if (detailSubject != null) detailSubject.text = NarrativeText.Resolve(def.subject, farm);
        if (detailBody != null) detailBody.text = NarrativeText.Resolve(def.body, farm);
        if (portrait != null)
            portrait.style.backgroundImage = def.senderPortrait != null
                ? new StyleBackground(def.senderPortrait) : new StyleBackground();

        bool hasReward = def.HasReward;
        var entry = FindEntry(letterId);
        bool claimed = entry != null && entry.claimed;

        // Enclosed items live in their own inset list (below the message) rather than crammed onto the
        // button — so future letters can grant several things without running out of button.
        BuildRewardItems(def, hasReward);

        if (claimButton != null)
        {
            claimButton.style.display = hasReward ? DisplayStyle.Flex : DisplayStyle.None;
            // Amount now shows in the items list; the button is just the action / done-state.
            claimButton.text = claimed ? "Items Claimed" : "Claim";
            if (claimed) claimButton.AddToClassList("inbox-claim--claimed");
            else claimButton.RemoveFromClassList("inbox-claim--claimed");
            // Kept "enabled" when claimed so it stays a readable brown pill (not the gray :disabled
            // wash); it's made non-interactive instead, and OnClaim is idempotent regardless.
            claimButton.SetEnabled(true);
            claimButton.pickingMode = claimed ? PickingMode.Ignore : PickingMode.Position;
        }
        if (ctaButton != null)
        {
            ctaButton.style.display = def.ctaKind != CtaKind.None ? DisplayStyle.Flex : DisplayStyle.None;
            ctaButton.text = CtaLabel(def.ctaKind);
        }

        // Mark read LAST: it fires OnInboxChanged, and while the detail view was still hidden that
        // handler rebuilt the list — which clears currentLetterId, so on a letter's first open
        // Claim and the call-to-action silently did nothing until it was reopened.
        mgr.MarkRead(letterId);
    }

    // Renders the letter's reward(s) into the inset "Enclosed" list. One reward today, but built as a
    // list so multi-item letters just add more rows here without touching the button.
    private void BuildRewardItems(LetterDef def, bool hasReward)
    {
        if (rewardItems == null || rewardItemsList == null) return;
        rewardItemsList.Clear();
        if (!hasReward) { rewardItems.style.display = DisplayStyle.None; return; }

        rewardItems.style.display = DisplayStyle.Flex;

        if (def.HasGiftSeed)
        {
            CropData crop = CropOwnership.Find(def.giftSeed);
            var icon = RewardItemRow($"{(crop != null ? crop.cropName : def.giftSeed)} seed packet");
            Sprite packet = crop != null ? crop.seedPacketSprite : null;
            if (packet != null) icon.style.backgroundImage = new StyleBackground(packet);
        }
        if (def.HasCurrencyReward)
            RewardItemRow($"{def.rewardAmount:N0} {def.rewardKind}").AddToClassList(RewardIconClass(def.rewardKind));
    }

    // One "Enclosed" row: an icon and a label. Returns the icon so the caller can dress it.
    private VisualElement RewardItemRow(string text)
    {
        var itemRow = new VisualElement(); itemRow.AddToClassList("inbox-item");
        var icon = new VisualElement(); icon.AddToClassList("inbox-item-icon");
        icon.pickingMode = PickingMode.Ignore;
        var label = new Label(text);
        label.AddToClassList("inbox-item-label");
        itemRow.Add(icon); itemRow.Add(label);
        rewardItemsList.Add(itemRow);
        return icon;
    }

    private static string RewardIconClass(RewardKind kind)
    {
        switch (kind)
        {
            case RewardKind.Gems:    return "inbox-item-icon--gems";
            case RewardKind.Compost: return "inbox-item-icon--compost";
            default:                 return "inbox-item-icon--coins";
        }
    }

    private InboxEntry FindEntry(string letterId)
    {
        if (InboxManager.Instance == null) return null;
        foreach (var e in InboxManager.Instance.Entries) if (e.letterId == letterId) return e;
        return null;
    }

    private void OnClaim()
    {
        if (currentLetterId == null) return;
        if (InboxManager.Instance != null && InboxManager.Instance.ClaimReward(currentLetterId))
            ShowDetail(currentLetterId); // refresh button → "Claimed"
    }

    private void OnCta()
    {
        var def = InboxManager.Instance?.GetDef(currentLetterId);
        Debug.Log($"[Inbox] CTA pressed on '{currentLetterId}' -> {(def != null ? def.ctaKind.ToString() : "no letter")}");
        if (def == null) return;
        switch (def.ctaKind)
        {
            case CtaKind.OpenEquipment: EquipmentPopupUITK.Instance?.Open(); Close(); break;
            case CtaKind.OpenResearch:  ResearchPopupUITK.Instance?.Open();  Close(); break;
            case CtaKind.OpenShop:      EquipmentPopupUITK.Instance?.Open(); Close(); break;
            case CtaKind.OpenFarmUpgrades: Close(); FarmPopupUITK.Instance?.Open(); break;
            case CtaKind.OpenCarpenter:    Close(); PanTo(CameraPanController.Location.Market); CarpenterPopupUITK.Instance?.Open(); break;
            case CtaKind.OpenBarn:         Close(); BarnPopupUITK.Instance?.Open(); break;
            case CtaKind.OpenAnimals:      Close(); AnimalPopupUITK.Instance?.Open(); break;
            case CtaKind.OpenFieldPicker:  Close(); FindFirstObjectByType<RunUI>()?.OpenFieldPicker(); break;
            case CtaKind.OpenMarket:       Close(); PanTo(CameraPanController.Location.Market); break;
            case CtaKind.OpenTownRequests: Close(); PanTo(CameraPanController.Location.Market); TownRequestsPopupUITK.Instance?.Open(); break;
            case CtaKind.OpenPlantsShop:   Close(); PanTo(CameraPanController.Location.Market); ShopPopupUITK.TryOpen(ShopPopupUITK.Section.Plants); break;
            default: Debug.Log($"[Inbox] CTA {def.ctaKind} not wired."); break;
        }
    }

    private static void PanTo(CameraPanController.Location location)
    {
        var pan = FindFirstObjectByType<CameraPanController>();
        if (pan != null && pan.CurrentLocation != location) pan.PanTo(location);
    }

    private static string CtaLabel(CtaKind kind)
    {
        switch (kind)
        {
            case CtaKind.OpenEquipment: return "Go to Equipment";
            case CtaKind.OpenResearch:  return "Go to Research";
            case CtaKind.OpenShop:      return "Go to Shop";
            case CtaKind.OpenFarmUpgrades: return "See Farm Upgrades";
            case CtaKind.OpenCarpenter:    return "Visit Harry's Shop";
            case CtaKind.OpenBarn:         return "Open the Barn";
            case CtaKind.OpenAnimals:      return "See Animals";
            case CtaKind.OpenFieldPicker:  return "Choose Seeds";
            case CtaKind.OpenMarket:       return "Go to Market";
            case CtaKind.OpenTownRequests: return "See Requests";
            case CtaKind.OpenPlantsShop:   return "Visit Hazel's Stall";
            default: return "Go";
        }
    }
}
