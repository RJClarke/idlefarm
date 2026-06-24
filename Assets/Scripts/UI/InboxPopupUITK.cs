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
    private VisualElement popupRoot, listView, detailView, portrait, backdrop;
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
    public void Close() { isOpen = false; if (popupRoot != null) popupRoot.style.display = DisplayStyle.None; }

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
        mgr.MarkRead(letterId);

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

        bool hasReward = def.rewardKind != RewardKind.None && def.rewardAmount > 0;
        var entry = FindEntry(letterId);
        bool claimed = entry != null && entry.claimed;
        if (claimButton != null)
        {
            claimButton.style.display = hasReward ? DisplayStyle.Flex : DisplayStyle.None;
            claimButton.text = claimed ? "Claimed" : $"Claim {def.rewardAmount} {def.rewardKind}";
            claimButton.SetEnabled(hasReward && !claimed);
        }
        if (ctaButton != null)
        {
            ctaButton.style.display = def.ctaKind != CtaKind.None ? DisplayStyle.Flex : DisplayStyle.None;
            ctaButton.text = CtaLabel(def.ctaKind);
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
        if (def == null) return;
        switch (def.ctaKind)
        {
            case CtaKind.OpenEquipment: EquipmentPopupUITK.Instance?.Open(); Close(); break;
            case CtaKind.OpenResearch:  ResearchPopupUITK.Instance?.Open();  Close(); break;
            case CtaKind.OpenShop:      EquipmentPopupUITK.Instance?.Open(); Close(); break;
            default: Debug.Log($"[Inbox] CTA {def.ctaKind} not wired."); break;
        }
    }

    private static string CtaLabel(CtaKind kind)
    {
        switch (kind)
        {
            case CtaKind.OpenEquipment: return "Go to Equipment";
            case CtaKind.OpenResearch:  return "Go to Research";
            case CtaKind.OpenShop:      return "Go to Shop";
            default: return "Go";
        }
    }
}
