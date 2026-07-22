using UnityEngine;
using UnityEngine.UI;

/// <summary>Top-bar button (beside the mailbox/basket) that opens the read-only Inventory panel.
/// Deliberately dumb — no notification state; inventory is always just "your stuff".</summary>
[RequireComponent(typeof(Button))]
public class InventoryButton : MonoBehaviour
{
    private Button button;

    private void Awake() { button = GetComponent<Button>(); }

    private void OnEnable()
    {
        button = button != null ? button : GetComponent<Button>();
        button.onClick.AddListener(OnClick);
    }

    private void OnDisable()
    {
        if (button != null) button.onClick.RemoveListener(OnClick);
    }

    private void OnClick() { InventoryPopupUITK.Instance?.Open(); }
}
