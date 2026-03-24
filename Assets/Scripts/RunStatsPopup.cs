using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

/// <summary>
/// Popup that displays run statistics in a scrollable two-column layout.
/// Left column: stat labels (left-aligned). Right column: values (right-aligned).
/// Shown after a run ends. Can be re-opened via "Prev. Run Stats" button.
/// </summary>
public class RunStatsPopup : MonoBehaviour
{
    public static RunStatsPopup Instance { get; private set; }

    [Header("UI References")]
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private Button closeButton;
    [SerializeField] private Button backdropButton;
    [SerializeField] private TextMeshProUGUI titleText;
    [SerializeField] private TextMeshProUGUI labelsText;
    [SerializeField] private TextMeshProUGUI valuesText;

    [Header("Prev. Run Stats Button")]
    [SerializeField] private GameObject prevRunStatsButton;
    private TMPro.TextMeshProUGUI prevRunStatsButtonText;

    [Header("Animation")]
    [SerializeField] private RectTransform popupContainer;
    [SerializeField] private float fadeInDuration = 0.3f;
    [SerializeField] private LeanTweenType easeType = LeanTweenType.easeOutQuad;

    private bool isOpen = false;
    private bool hasStatsToShow = false;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        if (canvasGroup == null)
        {
            canvasGroup = GetComponent<CanvasGroup>();
            if (canvasGroup == null)
                canvasGroup = gameObject.AddComponent<CanvasGroup>();
        }
    }

    private void Start()
    {
        if (closeButton != null)
            closeButton.onClick.AddListener(Hide);

        if (backdropButton != null)
            backdropButton.onClick.AddListener(Hide);

        if (prevRunStatsButton != null)
        {
            Button btn = prevRunStatsButton.GetComponent<Button>();
            if (btn != null)
                btn.onClick.AddListener(Show);
            prevRunStatsButtonText = prevRunStatsButton.GetComponentInChildren<TMPro.TextMeshProUGUI>();
            prevRunStatsButton.SetActive(false);
        }

        if (RunManager.Instance != null)
        {
            RunManager.Instance.OnRunStarted += OnRunStarted;
            RunManager.Instance.OnRunEnded += OnRunEnded;
        }

        HideImmediate();
    }

    private void OnDestroy()
    {
        if (closeButton != null)
            closeButton.onClick.RemoveListener(Hide);

        if (backdropButton != null)
            backdropButton.onClick.RemoveListener(Hide);

        if (prevRunStatsButton != null)
        {
            Button btn = prevRunStatsButton.GetComponent<Button>();
            if (btn != null)
                btn.onClick.RemoveListener(Show);
        }

        if (RunManager.Instance != null)
        {
            RunManager.Instance.OnRunStarted -= OnRunStarted;
            RunManager.Instance.OnRunEnded -= OnRunEnded;
        }
    }

    private void OnRunStarted()
    {
        // Show button during run with "Run Stats" label
        if (prevRunStatsButton != null)
            prevRunStatsButton.SetActive(true);
        if (prevRunStatsButtonText != null)
            prevRunStatsButtonText.text = "Run Stats";
    }

    private void OnRunEnded()
    {
        // Switch to "Prev. Run Stats" label after run ends
        hasStatsToShow = true;
        if (prevRunStatsButton != null)
            prevRunStatsButton.SetActive(true);
        if (prevRunStatsButtonText != null)
            prevRunStatsButtonText.text = "Prev. Run Stats";
    }

    /// <summary>
    /// Show the stats popup with current RunStats data.
    /// </summary>
    public void Show()
    {
        if (isOpen) return;
        if (RunStats.Instance == null) return;

        // Render on top of all other UI
        transform.SetAsLastSibling();

        // Build the two-column text
        List<(string label, string value)> stats = RunStats.Instance.GetDisplayStats();

        string leftText = "";
        string rightText = "";

        for (int i = 0; i < stats.Count; i++)
        {
            if (i > 0)
            {
                leftText += "\n";
                rightText += "\n";
            }

            if (stats[i].value == null)
            {
                // Section header — show on left, blank on right
                leftText += stats[i].label;
                rightText += " ";
            }
            else
            {
                leftText += stats[i].label;
                rightText += stats[i].value;
            }
        }

        if (labelsText != null) labelsText.text = leftText;
        if (valuesText != null) valuesText.text = rightText;

        // Animate in
        isOpen = true;
        canvasGroup.alpha = 0f;
        canvasGroup.blocksRaycasts = true;
        canvasGroup.interactable = true;

        if (popupContainer != null)
        {
            popupContainer.localScale = Vector3.one * 0.8f;
            LeanTween.scale(popupContainer.gameObject, Vector3.one, fadeInDuration)
                .setEase(easeType);
        }

        LeanTween.alphaCanvas(canvasGroup, 1f, fadeInDuration)
            .setEase(easeType);
    }

    /// <summary>
    /// Hide the stats popup.
    /// </summary>
    public void Hide()
    {
        if (!isOpen) return;
        isOpen = false;

        canvasGroup.blocksRaycasts = false;
        canvasGroup.interactable = false;

        if (popupContainer != null)
        {
            LeanTween.scale(popupContainer.gameObject, Vector3.one * 0.8f, fadeInDuration)
                .setEase(easeType);
        }

        LeanTween.alphaCanvas(canvasGroup, 0f, fadeInDuration)
            .setEase(easeType);
    }

    private void HideImmediate()
    {
        canvasGroup.alpha = 0f;
        canvasGroup.blocksRaycasts = false;
        canvasGroup.interactable = false;
    }
}
