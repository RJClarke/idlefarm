using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class EggClaimButton : MonoBehaviour
{
    [SerializeField] private Button button;
    [SerializeField] private Image buttonImage;
    [SerializeField] private GameObject notificationDot;
    [SerializeField] private TextMeshProUGUI emojiText;

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
    }

    private void OnEggReady()
    {
        UpdateState();

        // Subtle bounce animation to draw attention
        LeanTween.cancel(gameObject);
        transform.localScale = Vector3.one;
        LeanTween.scale(gameObject, Vector3.one * 1.2f, 0.15f)
            .setEaseOutQuad()
            .setLoopPingPong(1);
    }
}
