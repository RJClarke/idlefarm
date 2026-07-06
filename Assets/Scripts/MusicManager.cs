using System.Collections;
using UnityEngine;

/// <summary>
/// Plays looping background music in the game scene and crossfades between tracks based on the
/// camera's current Location: the Farm/Greenhouse share one mood, the Market gets its own.
///
/// Two AudioSources are used so a fade-out and fade-in can overlap (true crossfade). The Music
/// volume slider (Settings) is applied per-source via <see cref="SettingsManager.EffectiveMusicVolume"/>
/// and updates live while dragged; Master volume + Mute are handled globally by the AudioListener.
///
/// Wiring: drop this on a GameObject in the scene and assign farmMusic + marketMusic. It finds the
/// CameraPanController on the Main Camera itself and reacts to OnPanStarted, so music begins switching
/// the moment a trip to/from the Market starts (mirrors LocationModeController's visibility switch).
/// </summary>
public class MusicManager : MonoBehaviour
{
    public static MusicManager Instance { get; private set; }

    [Header("Tracks")]
    [Tooltip("Looping music for the Farm + Greenhouse (everything that isn't the Market).")]
    [SerializeField] private AudioClip farmMusic;
    [Tooltip("Looping music for the Market.")]
    [SerializeField] private AudioClip marketMusic;

    [Header("Crossfade")]
    [Tooltip("Seconds to fade from one track to the other. Keep it snappy.")]
    [SerializeField] private float fadeDuration = 1.0f;

    private AudioSource sourceA;
    private AudioSource sourceB;
    private AudioSource active;        // currently audible source
    private AudioClip currentClip;     // clip the active source is (or is becoming)
    private Coroutine fadeRoutine;
    private CameraPanController panController;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        sourceA = CreateSource("MusicSourceA");
        sourceB = CreateSource("MusicSourceB");
        active = sourceA;
    }

    private AudioSource CreateSource(string name)
    {
        var go = new GameObject(name);
        go.transform.SetParent(transform, false);
        var src = go.AddComponent<AudioSource>();
        src.playOnAwake = false;
        src.loop = true;
        src.volume = 0f;
        src.spatialBlend = 0f; // 2D
        return src;
    }

    private void Start()
    {
        SettingsManager.EnsureLoaded();
        SettingsManager.OnAudioSettingsChanged += ApplyVolume;

        panController = Camera.main != null ? Camera.main.GetComponent<CameraPanController>() : null;
        if (panController != null)
            panController.OnPanStarted += OnPanStarted;
        else
            Debug.LogWarning("[MusicManager] No CameraPanController on Main Camera — music won't switch at the Market.");

        // Fade the starting mood in from silence (the splash scene's music is already gone).
        CameraPanController.Location startLoc = panController != null ? panController.CurrentLocation : CameraPanController.Location.Farm;
        SwitchTo(ClipFor(startLoc), instant: false);
    }

    private void OnDestroy()
    {
        SettingsManager.OnAudioSettingsChanged -= ApplyVolume;
        if (panController != null) panController.OnPanStarted -= OnPanStarted;
        if (Instance == this) Instance = null;
    }

    private void OnPanStarted(CameraPanController.Location target) => SwitchTo(ClipFor(target), instant: false);

    private AudioClip ClipFor(CameraPanController.Location loc)
        => loc == CameraPanController.Location.Market ? marketMusic : farmMusic;

    private void SwitchTo(AudioClip clip, bool instant)
    {
        if (clip == null || clip == currentClip) return;
        currentClip = clip;

        AudioSource incoming = (active == sourceA) ? sourceB : sourceA;
        AudioSource outgoing = active;

        incoming.clip = clip;
        incoming.volume = 0f;
        incoming.Play();
        active = incoming;

        if (fadeRoutine != null) StopCoroutine(fadeRoutine);

        if (instant || fadeDuration <= 0f)
        {
            incoming.volume = SettingsManager.EffectiveMusicVolume;
            if (outgoing != null) { outgoing.volume = 0f; outgoing.Stop(); }
            return;
        }

        fadeRoutine = StartCoroutine(Crossfade(incoming, outgoing));
    }

    private IEnumerator Crossfade(AudioSource incoming, AudioSource outgoing)
    {
        float t = 0f;
        float outStart = outgoing != null ? outgoing.volume : 0f;
        while (t < fadeDuration)
        {
            t += Time.unscaledDeltaTime; // music shouldn't care about game-speed / pause
            float k = Mathf.Clamp01(t / fadeDuration);
            float target = SettingsManager.EffectiveMusicVolume; // read live so the slider responds mid-fade
            incoming.volume = target * k;
            if (outgoing != null) outgoing.volume = outStart * (1f - k);
            yield return null;
        }

        incoming.volume = SettingsManager.EffectiveMusicVolume;
        if (outgoing != null) { outgoing.volume = 0f; outgoing.Stop(); }
        fadeRoutine = null;
    }

    // Re-apply the music slider live. While a crossfade is running the coroutine already reads the
    // live value each frame, so we only need to set the steady-state volume here.
    private void ApplyVolume()
    {
        if (fadeRoutine != null) return;
        if (active != null && active.isPlaying) active.volume = SettingsManager.EffectiveMusicVolume;
    }
}
