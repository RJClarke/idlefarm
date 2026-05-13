using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class AnimalEquipButton : MonoBehaviour
{
    [SerializeField] private Button button;
    [SerializeField] private Image animalIcon;
    [SerializeField] private TextMeshProUGUI emojiText;
    [SerializeField] private Sprite silhouetteSprite;

    private System.Action<AnimalData> onEquipped;
    private System.Action onUnequipped;

    private void Start()
    {
        button.onClick.AddListener(OnClick);

        if (AnimalManager.Instance != null)
        {
            onEquipped = (_) => UpdateDisplay();
            onUnequipped = UpdateDisplay;
            AnimalManager.Instance.OnAnimalEquipped += onEquipped;
            AnimalManager.Instance.OnAnimalUnequipped += onUnequipped;
        }

        UpdateDisplay();
    }

    private void OnDestroy()
    {
        if (AnimalManager.Instance != null)
        {
            AnimalManager.Instance.OnAnimalEquipped -= onEquipped;
            AnimalManager.Instance.OnAnimalUnequipped -= onUnequipped;
        }
    }

    private void OnClick()
    {
        if (AnimalPopupUITK.Instance != null)
            AnimalPopupUITK.Instance.Open();
    }

    private void UpdateDisplay()
    {
        AnimalData equipped = AnimalManager.Instance?.GetEquippedAnimal();

        if (equipped != null)
        {
            // Show equipped animal
            if (emojiText != null) emojiText.text = equipped.animalEmoji;
            if (animalIcon != null && equipped.iconSprite != null)
            {
                animalIcon.sprite = equipped.iconSprite;
                animalIcon.enabled = true;
                if (emojiText != null) emojiText.gameObject.SetActive(false);
            }
            else
            {
                if (animalIcon != null) animalIcon.enabled = false;
                if (emojiText != null) emojiText.gameObject.SetActive(true);
            }
        }
        else
        {
            // Show silhouette / empty state
            if (emojiText != null)
            {
                emojiText.gameObject.SetActive(true);
                emojiText.text = "?";
            }
            if (animalIcon != null)
            {
                if (silhouetteSprite != null)
                {
                    animalIcon.sprite = silhouetteSprite;
                    animalIcon.enabled = true;
                }
                else
                {
                    animalIcon.enabled = false;
                }
            }
        }
    }
}
