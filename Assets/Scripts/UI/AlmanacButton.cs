using UnityEngine;
using UnityEngine.UI;

/// <summary>HUD book button (top-left stack) that opens the Farmer's Almanac. Baked into the scene by
/// Farm Game > Almanac > Bake HUD Button (a copy of the mailbox button with the book icon).</summary>
[RequireComponent(typeof(Button))]
public class AlmanacButton : MonoBehaviour
{
    private Button button;

    private void Awake() => button = GetComponent<Button>();
    private void OnEnable() => button.onClick.AddListener(OnClick);
    private void OnDisable() => button.onClick.RemoveListener(OnClick);

    private void OnClick()
    {
        if (AlmanacPopupUITK.Instance != null) AlmanacPopupUITK.Instance.Open();
    }
}
