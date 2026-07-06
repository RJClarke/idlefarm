using System;
using UnityEngine;

/// <summary>
/// Central read/write for user settings persisted to PlayerPrefs.
/// Stubs (haptics, reduce motion, etc.) persist but aren't yet read by gameplay code.
/// </summary>
public static class SettingsManager
{
    /// <summary>
    /// Raised whenever any audio setting (master/music/sfx/mute) changes, so live
    /// listeners (e.g. MusicManager) can re-apply volume while the slider is dragged.
    /// </summary>
    public static event Action OnAudioSettingsChanged;
    // ── Audio ──
    private const string KEY_MASTER_VOLUME = "setting_master_volume";
    private const string KEY_MUSIC_VOLUME  = "setting_music_volume";
    private const string KEY_SFX_VOLUME    = "setting_sfx_volume";
    private const string KEY_MUTE_ALL      = "setting_mute_all";

    // ── Gameplay ──
    private const string KEY_FLOATING_NUMBERS   = "setting_floating_numbers";
    private const string KEY_CURRENCY_ANIMATIONS = "setting_currency_animations";
    private const string KEY_HAPTICS            = "setting_haptics";
    private const string KEY_REDUCE_MOTION      = "setting_reduce_motion";
    private const string KEY_LOW_POWER          = "setting_low_power";
    private const string KEY_TARGET_FPS         = "setting_target_fps";

    // ── Dev / Testing ──
    private const string KEY_SHOW_FPS = "setting_show_fps";

    private static bool initialized;

    private static float _masterVolume, _musicVolume, _sfxVolume;
    private static bool  _muteAll;
    private static bool  _showFloatingNumbers, _currencyAnimations, _haptics, _reduceMotion, _lowPower;
    private static int   _targetFps;
    private static bool  _showFps;

    public static void EnsureLoaded()
    {
        if (initialized) return;
        _masterVolume        = PlayerPrefs.GetFloat(KEY_MASTER_VOLUME, 1f);
        _musicVolume         = PlayerPrefs.GetFloat(KEY_MUSIC_VOLUME,  1f);
        _sfxVolume           = PlayerPrefs.GetFloat(KEY_SFX_VOLUME,    1f);
        _muteAll             = PlayerPrefs.GetInt(KEY_MUTE_ALL, 0) == 1;
        _showFloatingNumbers = PlayerPrefs.GetInt(KEY_FLOATING_NUMBERS, 1) == 1;
        _currencyAnimations  = PlayerPrefs.GetInt(KEY_CURRENCY_ANIMATIONS, 1) == 1;
        _haptics             = PlayerPrefs.GetInt(KEY_HAPTICS, 1) == 1;
        _reduceMotion        = PlayerPrefs.GetInt(KEY_REDUCE_MOTION, 0) == 1;
        _lowPower            = PlayerPrefs.GetInt(KEY_LOW_POWER, 0) == 1;
        _targetFps           = ClampFps(PlayerPrefs.GetInt(KEY_TARGET_FPS, 60));
        _showFps             = PlayerPrefs.GetInt(KEY_SHOW_FPS, 0) == 1;
        initialized = true;
        ApplyAudio();
        ApplyFrameRate();
    }

    /// <summary>
    /// Applies the target frame rate cap at boot so it takes effect without any scene
    /// object present. Runs after the first scene loads.
    /// </summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void BootApplySettings()
    {
        EnsureLoaded();
    }

    // ── Audio ──
    public static float MasterVolume
    {
        get { EnsureLoaded(); return _masterVolume; }
        set { EnsureLoaded(); _masterVolume = Mathf.Clamp01(value); PlayerPrefs.SetFloat(KEY_MASTER_VOLUME, _masterVolume); PlayerPrefs.Save(); ApplyAudio(); OnAudioSettingsChanged?.Invoke(); }
    }
    public static float MusicVolume
    {
        get { EnsureLoaded(); return _musicVolume; }
        set { EnsureLoaded(); _musicVolume = Mathf.Clamp01(value); PlayerPrefs.SetFloat(KEY_MUSIC_VOLUME, _musicVolume); PlayerPrefs.Save(); OnAudioSettingsChanged?.Invoke(); }
    }
    public static float SfxVolume
    {
        get { EnsureLoaded(); return _sfxVolume; }
        set { EnsureLoaded(); _sfxVolume = Mathf.Clamp01(value); PlayerPrefs.SetFloat(KEY_SFX_VOLUME, _sfxVolume); PlayerPrefs.Save(); OnAudioSettingsChanged?.Invoke(); }
    }
    public static bool MuteAll
    {
        get { EnsureLoaded(); return _muteAll; }
        set { EnsureLoaded(); _muteAll = value; PlayerPrefs.SetInt(KEY_MUTE_ALL, _muteAll ? 1 : 0); PlayerPrefs.Save(); ApplyAudio(); OnAudioSettingsChanged?.Invoke(); }
    }

    /// <summary>
    /// Effective music gain to apply on a music AudioSource. Master &amp; mute are handled
    /// globally by <see cref="AudioListener.volume"/>, so this is just the music slider.
    /// </summary>
    public static float EffectiveMusicVolume
    {
        get { EnsureLoaded(); return _muteAll ? 0f : _musicVolume; }
    }

    private static void ApplyAudio()
    {
        // Master + mute drive the global AudioListener. The Music slider is applied per-source
        // by MusicManager via EffectiveMusicVolume; SFX is still stubbed (no mixer yet).
        AudioListener.volume = _muteAll ? 0f : _masterVolume;
    }

    private static void ApplyFrameRate()
    {
        // Uncap vSync so Application.targetFrameRate is honoured on platforms where vSync
        // would otherwise override it.
        QualitySettings.vSyncCount = 0;
        Application.targetFrameRate = _targetFps;
    }

    private static int ClampFps(int value) => (value == 30 || value == 60 || value == 120) ? value : 60;

    // ── Gameplay ──
    public static bool ShowFloatingNumbers
    {
        get { EnsureLoaded(); return _showFloatingNumbers; }
        set { EnsureLoaded(); _showFloatingNumbers = value; PlayerPrefs.SetInt(KEY_FLOATING_NUMBERS, value ? 1 : 0); PlayerPrefs.Save(); }
    }
    public static bool CurrencyAnimations
    {
        get { EnsureLoaded(); return _currencyAnimations; }
        set { EnsureLoaded(); _currencyAnimations = value; PlayerPrefs.SetInt(KEY_CURRENCY_ANIMATIONS, value ? 1 : 0); PlayerPrefs.Save(); }
    }
    /// <summary>Frame-rate cap. Allowed values 30/60/120; anything else clamps to 60.</summary>
    public static int TargetFps
    {
        get { EnsureLoaded(); return _targetFps; }
        set { EnsureLoaded(); _targetFps = ClampFps(value); PlayerPrefs.SetInt(KEY_TARGET_FPS, _targetFps); PlayerPrefs.Save(); ApplyFrameRate(); }
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
