using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class EggClaimButton : MonoBehaviour
{
    [SerializeField] private Button button;
    [SerializeField] private Image buttonImage;
    [SerializeField] private GameObject notificationDot;
    [SerializeField] private TextMeshProUGUI emojiText;

    [SerializeField] private Sprite eggSprite;

    [Header("Colors")]
    [SerializeField] private Color readyColor = new Color(0.55f, 0.35f, 0.17f, 0.9f);
    [SerializeField] private Color cooldownColor = new Color(0.35f, 0.35f, 0.35f, 0.5f);

    private System.Action<AnimalData> onEquipped;
    private System.Action onUnequipped;
    private System.Action onEggReady;
    private System.Action onEggClaimed;

    private void Start()
    {
        button.onClick.AddListener(OnClick);

        if (eggSprite != null && buttonImage != null)
        {
            buttonImage.sprite = eggSprite;
            if (emojiText != null) emojiText.gameObject.SetActive(false);
        }

        if (notificationDot != null)
        {
            Image dotImage = notificationDot.GetComponent<Image>();
            if (dotImage != null) dotImage.sprite = BuildCircleSprite(64);
        }

        if (AnimalManager.Instance != null)
        {
            onEquipped = (_) => UpdateVisibility();
            onUnequipped = UpdateVisibility;
            onEggReady = OnEggReady;
            onEggClaimed = UpdateState;
            AnimalManager.Instance.OnAnimalEquipped += onEquipped;
            AnimalManager.Instance.OnAnimalUnequipped += onUnequipped;
            AnimalManager.Instance.OnEggReady += onEggReady;
            AnimalManager.Instance.OnEggClaimed += onEggClaimed;
        }

        UpdateVisibility();
    }

    private void OnDestroy()
    {
        if (AnimalManager.Instance != null)
        {
            AnimalManager.Instance.OnAnimalEquipped -= onEquipped;
            AnimalManager.Instance.OnAnimalUnequipped -= onUnequipped;
            AnimalManager.Instance.OnEggReady -= onEggReady;
            AnimalManager.Instance.OnEggClaimed -= onEggClaimed;
        }
    }

    private void OnClick()
    {
        if (AnimalManager.Instance != null && AnimalManager.Instance.IsEggReady)
        {
            AnimalManager.Instance.ClaimEgg();
        }
    }

    private void UpdateVisibility()
    {
        AnimalData equipped = AnimalManager.Instance?.GetEquippedAnimal();
        bool showButton = equipped != null && equipped.abilityType == AnimalAbilityType.PassiveTimer;
        gameObject.SetActive(showButton);

        if (showButton)
        {
            UpdateState();
        }
    }

    private void UpdateState()
    {
        bool ready = AnimalManager.Instance != null && AnimalManager.Instance.IsEggReady;

        if (buttonImage != null)
            buttonImage.color = ready ? readyColor : cooldownColor;

        if (notificationDot != null)
            notificationDot.SetActive(ready);

        if (emojiText != null)
            emojiText.text = "🥚";

        float targetScale = ready ? 1f : 0.75f;
        LeanTween.cancel(gameObject);
        LeanTween.scale(gameObject, Vector3.one * targetScale, 0.2f).setEaseOutBack();
    }

    private static Sprite BuildCircleSprite(int size)
    {
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
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

    private void OnEggReady()
    {
        UpdateState();

        // Pop to full size with a little overshoot
        LeanTween.cancel(gameObject);
        LeanTween.scale(gameObject, Vector3.one * 1.15f, 0.2f)
            .setEaseOutBack()
            .setOnComplete(() => LeanTween.scale(gameObject, Vector3.one, 0.1f).setEaseInQuad());
    }
}
