using UnityEngine;

/// <summary>
/// Central read/write for user settings persisted to PlayerPrefs.
/// Stubs (haptics, reduce motion, etc.) persist but aren't yet read by gameplay code.
/// </summary>
public static class SettingsManager
{
    // ── Audio ──
    private const string KEY_MASTER_VOLUME = "setting_master_volume";
    private const string KEY_MUSIC_VOLUME  = "setting_music_volume";
    private const string KEY_SFX_VOLUME    = "setting_sfx_volume";
    private const string KEY_MUTE_ALL      = "setting_mute_all";

    // ── Gameplay ──
    private const string KEY_FLOATING_NUMBERS = "setting_floating_numbers";
    private const string KEY_HAPTICS          = "setting_haptics";
    private const string KEY_REDUCE_MOTION    = "setting_reduce_motion";
    private const string KEY_LOW_POWER        = "setting_low_power";

    // ── Dev / Testing ──
    private const string KEY_SHOW_FPS = "setting_show_fps";

    private static bool initialized;

    private static float _masterVolume, _musicVolume, _sfxVolume;
    private static bool  _muteAll;
    private static bool  _showFloatingNumbers, _haptics, _reduceMotion, _lowPower;
    private static bool  _showFps;

    public static void EnsureLoaded()
    {
        if (initialized) return;
        _masterVolume        = PlayerPrefs.GetFloat(KEY_MASTER_VOLUME, 1f);
        _musicVolume         = PlayerPrefs.GetFloat(KEY_MUSIC_VOLUME,  1f);
        _sfxVolume           = PlayerPrefs.GetFloat(KEY_SFX_VOLUME,    1f);
        _muteAll             = PlayerPrefs.GetInt(KEY_MUTE_ALL, 0) == 1;
        _showFloatingNumbers = PlayerPrefs.GetInt(KEY_FLOATING_NUMBERS, 1) == 1;
        _haptics             = PlayerPrefs.GetInt(KEY_HAPTICS, 1) == 1;
        _reduceMotion        = PlayerPrefs.GetInt(KEY_REDUCE_MOTION, 0) == 1;
        _lowPower            = PlayerPrefs.GetInt(KEY_LOW_POWER, 0) == 1;
        _showFps             = PlayerPrefs.GetInt(KEY_SHOW_FPS, 0) == 1;
        initialized = true;
        ApplyAudio();
    }

    // ── Audio ──
    public static float MasterVolume
    {
        get { EnsureLoaded(); return _masterVolume; }
        set { EnsureLoaded(); _masterVolume = Mathf.Clamp01(value); PlayerPrefs.SetFloat(KEY_MASTER_VOLUME, _masterVolume); PlayerPrefs.Save(); ApplyAudio(); }
    }
    public static float MusicVolume
    {
        get { EnsureLoaded(); return _musicVolume; }
        set { EnsureLoaded(); _musicVolume = Mathf.Clamp01(value); PlayerPrefs.SetFloat(KEY_MUSIC_VOLUME, _musicVolume); PlayerPrefs.Save(); }
    }
    public static float SfxVolume
    {
        get { EnsureLoaded(); return _sfxVolume; }
        set { EnsureLoaded(); _sfxVolume = Mathf.Clamp01(value); PlayerPrefs.SetFloat(KEY_SFX_VOLUME, _sfxVolume); PlayerPrefs.Save(); }
    }
    public static bool MuteAll
    {
        get { EnsureLoaded(); return _muteAll; }
        set { EnsureLoaded(); _muteAll = value; PlayerPrefs.SetInt(KEY_MUTE_ALL, _muteAll ? 1 : 0); PlayerPrefs.Save(); ApplyAudio(); }
    }

    private static void ApplyAudio()
    {
        // Drives the global AudioListener — music/SFX sliders are stubbed until we add an AudioMixer.
        AudioListener.volume = _muteAll ? 0f : _masterVolume;
    }

    // ── Gameplay ──
    public static bool ShowFloatingNumbers
    {
        get { EnsureLoaded(); return _showFloatingNumbers; }
        set { EnsureLoaded(); _showFloatingNumbers = value; PlayerPrefs.SetInt(KEY_FLOATING_NUMBERS, value ? 1 : 0); PlayerPrefs.Save(); }
    }
    public static bool Haptics
    {
        get { EnsureLoaded(); return _haptics; }
        set { EnsureLoaded(); _haptics = value; PlayerPrefs.SetInt(KEY_HAPTICS, value ? 1 : 0); PlayerPrefs.Save(); }
    }
    public static bool ReduceMotion
    {
        get { EnsureLoaded(); return _reduceMotion; }
        set { EnsureLoaded(); _reduceMotion = value; PlayerPrefs.SetInt(KEY_REDUCE_MOTION, value ? 1 : 0); PlayerPrefs.Save(); }
    }
    public static bool LowPowerMode
    {
        get { EnsureLoaded(); return _lowPower; }
        set { EnsureLoaded(); _lowPower = value; PlayerPrefs.SetInt(KEY_LOW_POWER, value ? 1 : 0); PlayerPrefs.Save(); }
    }

    // ── Dev ──
    public static bool ShowFps
    {
        get { EnsureLoaded(); return _showFps; }
        set { EnsureLoaded(); _showFps = value; PlayerPrefs.SetInt(KEY_SHOW_FPS, value ? 1 : 0); PlayerPrefs.Save(); }
    }
}
