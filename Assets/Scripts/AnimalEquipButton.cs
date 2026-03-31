using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class AnimalEquipButton : MonoBehaviour
{
    [SerializeField] private Button button;
    [SerializeField] private Image animalIcon;
    [SerializeField] private TextMeshProUGUI emojiText;
    [SerializeField] private Sprite silhouetteSprite;

    private void Start()
    {
        button.onClick.AddListener(OnClick);

        if (AnimalManager.Instance != null)
        {
            AnimalManager.Instance.OnAnimalEquipped += (_) => UpdateDisplay();
            AnimalManager.Instance.OnAnimalUnequipped += UpdateDisplay;
        }

        UpdateDisplay();
    }

    private void OnClick()
    {
        if (AnimalPopup.Instance != null)
            AnimalPopup.Instance.Show();
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
                emojiText.text = "❓";
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
