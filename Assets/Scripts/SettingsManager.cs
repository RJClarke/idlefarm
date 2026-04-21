using UnityEngine;

public static class SettingsManager
{
    private const string KEY_FLOATING_NUMBERS = "setting_floating_numbers";

    private static int? _showFloatingNumbers;

    public static bool ShowFloatingNumbers
    {
        get
        {
            if (_showFloatingNumbers == null)
                _showFloatingNumbers = PlayerPrefs.GetInt(KEY_FLOATING_NUMBERS, 1);
            return _showFloatingNumbers.Value == 1;
        }
        set
        {
            _showFloatingNumbers = value ? 1 : 0;
            PlayerPrefs.SetInt(KEY_FLOATING_NUMBERS, _showFloatingNumbers.Value);
            PlayerPrefs.Save();
        }
    }
}
