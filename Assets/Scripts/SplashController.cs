using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using TMPro;

/// <summary>
/// Drives the splash / title ("login") screen: plays the music, async-preloads the game scene in the
/// background, and only enables the Start button once the game is ready. Pressing Start activates the
/// already-loaded scene, so the cut into gameplay is instant.
///
/// The on-screen UI (canvas, title, Start button, status text) is built in code — same approach as
/// RainOverlayUI — because it's reliable and avoids fiddly scene wiring. Tune the look via the fields
/// below. Unity quirk: with allowSceneActivation=false, LoadSceneAsync.progress holds at 0.9 until you
/// flip the flag, so "ready" is progress >= 0.9, not 1.0.
/// </summary>
[RequireComponent(typeof(AudioSource))]
public class SplashController : MonoBehaviour
{
    [Header("Scene")]
    [SerializeField] private string gameSceneName = "SampleScene";

    [Header("Text")]
    [SerializeField] private string titleText = "Farm Silo";
    [SerializeField] private string startLabel = "Start";

    [Header("Start button look")]
    [SerializeField] private Color buttonColor = new Color(0.95f, 0.76f, 0.28f, 1f);
    [SerializeField] private Color buttonTextColor = new Color(0.18f, 0.12f, 0.04f, 1f);
    [SerializeField] private float pulseScale = 1.08f;
    [SerializeField] private float pulseTime = 0.7f;

    private AudioSource music;
    private Button startButton;
    private TMP_Text statusText;
    private AsyncOperation load;

    private void Awake()
    {
        music = GetComponent<AudioSource>();
        EnsureEventSystem();
        BuildUI();
    }

    private void Start()
    {
        if (music != null && music.clip != null) { music.loop = true; if (!music.isPlaying) music.Play(); }

        if (startButton != null) startButton.gameObject.SetActive(false);
        if (statusText != null) statusText.text = "Loading…";

        load = SceneManager.LoadSceneAsync(gameSceneName);
        load.allowSceneActivation = false;
        StartCoroutine(WatchLoad());
    }

    private IEnumerator WatchLoad()
    {
        while (load != null && load.progress < 0.9f)
            yield return null;

        if (statusText != null) statusText.text = string.Empty;

        if (startButton != null)
        {
            startButton.gameObject.SetActive(true);
            startButton.interactable = true;
            LeanTween.scale(startButton.gameObject, Vector3.one * pulseScale, pulseTime)
                     .setEaseInOutSine()
                     .setLoopPingPong();
        }
    }

    private void OnStartPressed()
    {
        if (load != null) load.allowSceneActivation = true;
    }

    // ── UI construction ────────────────────────────────────────────────────

    private static void EnsureEventSystem()
    {
        if (FindObjectOfType<EventSystem>() != null) return;
        var es = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
        es.transform.SetParent(null);
    }

    private void BuildUI()
    {
        // Canvas
        var canvasGO = new GameObject("SplashCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        var canvas = canvasGO.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;
        var scaler = canvasGO.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080, 1920);
        scaler.matchWidthOrHeight = 0.5f;

        // Title (top) — dark so it reads against the light, hazy sky.
        var title = CreateText("Title", canvasGO.transform, titleText, 120, new Color(0.12f, 0.10f, 0.08f, 1f));
        var tr = title.rectTransform;
        tr.anchorMin = tr.anchorMax = new Vector2(0.5f, 1f);
        tr.pivot = new Vector2(0.5f, 1f);
        tr.sizeDelta = new Vector2(1000, 240);
        tr.anchoredPosition = new Vector2(0, -180);
        title.fontStyle = FontStyles.Bold;

        // Status text (above the button)
        statusText = CreateText("StatusText", canvasGO.transform, string.Empty, 40, new Color(0.15f, 0.13f, 0.10f, 0.9f));
        var sr = statusText.rectTransform;
        sr.anchorMin = sr.anchorMax = new Vector2(0.5f, 0f);
        sr.pivot = new Vector2(0.5f, 0f);
        sr.sizeDelta = new Vector2(800, 80);
        sr.anchoredPosition = new Vector2(0, 200);

        // Start button (bottom)
        var btnGO = new GameObject("StartButton", typeof(Image), typeof(Button));
        btnGO.transform.SetParent(canvasGO.transform, false);
        var img = btnGO.GetComponent<Image>();
        img.color = buttonColor;
        var br = img.rectTransform;
        br.anchorMin = br.anchorMax = new Vector2(0.5f, 0f);
        br.pivot = new Vector2(0.5f, 0f);
        br.sizeDelta = new Vector2(520, 160);
        br.anchoredPosition = new Vector2(0, 300);

        startButton = btnGO.GetComponent<Button>();
        startButton.onClick.AddListener(OnStartPressed);

        var label = CreateText("Label", btnGO.transform, startLabel, 70, buttonTextColor);
        label.fontStyle = FontStyles.Bold;
        var lr = label.rectTransform;
        lr.anchorMin = Vector2.zero; lr.anchorMax = Vector2.one;
        lr.offsetMin = Vector2.zero; lr.offsetMax = Vector2.zero;
    }

    private static TMP_Text CreateText(string name, Transform parent, string text, float size, Color color)
    {
        var go = new GameObject(name, typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        var t = go.GetComponent<TextMeshProUGUI>();
        t.text = text;
        t.fontSize = size;
        t.color = color;
        t.alignment = TextAlignmentOptions.Center;
        t.raycastTarget = false;
        return t;
    }
}
