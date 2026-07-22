using UnityEngine;

/// <summary>
/// Tiny pooled sound-effect player. Plays a (randomly chosen) clip with a little pitch jitter so
/// repeated triggers — like tapping an animal over and over — don't sound like a copy-paste loop.
///
/// Master volume + mute are handled globally by <see cref="SettingsManager"/> via AudioListener.volume;
/// the SFX slider (SettingsManager.SfxVolume) is applied per play here. Auto-bootstraps on first use,
/// so nothing needs to be placed in the scene.
/// </summary>
public class SfxManager : MonoBehaviour
{
    private static SfxManager _instance;
    public static SfxManager Instance
    {
        get
        {
            if (_instance == null)
            {
                var go = new GameObject("SfxManager");
                _instance = go.AddComponent<SfxManager>(); // Awake sets _instance + builds the pool
                DontDestroyOnLoad(go);
            }
            return _instance;
        }
    }

    private const int PoolSize = 6; // enough voices for rapid overlapping taps
    private AudioSource[] pool;
    private int next;

    private void Awake()
    {
        if (_instance != null && _instance != this) { Destroy(gameObject); return; }
        _instance = this;
        pool = new AudioSource[PoolSize];
        for (int i = 0; i < PoolSize; i++)
        {
            var src = gameObject.AddComponent<AudioSource>();
            src.playOnAwake = false;
            src.spatialBlend = 0f; // 2D UI-ish sfx
            pool[i] = src;
        }
    }

    /// <summary>Play one random clip from the set with a slight random pitch. No-op if the set is empty
    /// (so callers can wire this up before the art/audio is dropped in).</summary>
    public void PlayRandom(AudioClip[] clips, float pitchMin = 0.94f, float pitchMax = 1.06f, float volume = 1f)
    {
        if (clips == null || clips.Length == 0) return;
        AudioClip clip = clips[Random.Range(0, clips.Length)];
        if (clip == null) return;

        var src = pool[next];
        next = (next + 1) % pool.Length;
        src.pitch = Random.Range(pitchMin, pitchMax);
        src.PlayOneShot(clip, Mathf.Clamp01(volume) * SettingsManager.SfxVolume);
    }
}
