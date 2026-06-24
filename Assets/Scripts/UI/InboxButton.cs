using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>Top-bar envelope button beside the daily-rewards basket. Opens the inbox and
/// shows an unread-count notification dot, refreshed on InboxManager.OnInboxChanged.</summary>
[RequireComponent(typeof(Button))]
public class InboxButton : MonoBehaviour
{
    [SerializeField] private GameObject notificationDot; // small dot child, toggled on unread
    [SerializeField] private TextMeshProUGUI unreadCountLabel; // optional count inside the dot

    private Button button;

    private void Awake() { button = GetComponent<Button>(); }

    private void Start()
    {
        if (notificationDot != null)
        {
            Image dotImage = notificationDot.GetComponent<Image>();
            if (dotImage != null && dotImage.sprite == null) dotImage.sprite = BuildCircleSprite(64);
        }
    }

    private void OnEnable()
    {
        button = button != null ? button : GetComponent<Button>();
        button.onClick.AddListener(OnClick);
        if (InboxManager.Instance != null) InboxManager.Instance.OnInboxChanged += Refresh;
        Refresh();
    }

    private void OnDisable()
    {
        button.onClick.RemoveListener(OnClick);
        if (InboxManager.Instance != null) InboxManager.Instance.OnInboxChanged -= Refresh;
    }

    private void OnClick() { InboxPopupUITK.Instance?.Open(); }

    private void Refresh()
    {
        int unread = InboxManager.Instance != null ? InboxManager.Instance.UnreadCount() : 0;
        if (notificationDot != null) notificationDot.SetActive(unread > 0);
        if (unreadCountLabel != null) unreadCountLabel.text = unread > 9 ? "9+" : unread.ToString();
    }

    private static Sprite BuildCircleSprite(int size)
    {
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };
        float center = size / 2f;
        float radius = center - 0.5f;
        Color[] pixels = new Color[size * size];
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dist = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(center, center));
                pixels[y * size + x] = new Color(1f, 1f, 1f, Mathf.Clamp01(radius - dist + 1f));
            }
        tex.SetPixels(pixels);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
    }
}
