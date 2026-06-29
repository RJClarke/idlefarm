using UnityEngine;

/// <summary>Live, eased weather values that every effect reads. 0..1 channels + a wind sign.</summary>
public struct WeatherState
{
    public float severity;      // master energy/danger
    public float wind;          // breeze -> gale
    public float cloudiness;    // cloud-shadow count/size/opacity
    public float precipitation; // rain rate + overlay darkness
    public float windDirection; // -1 left / +1 right
}

/// <summary>A named target the live state eases toward (the 0..1 channels). Tuned on WeatherData.</summary>
[System.Serializable]
public class WeatherProfile
{
    public string name = "Clear";
    [Range(0f, 1f)] public float severity;
    [Range(0f, 1f)] public float wind;
    [Range(0f, 1f)] public float cloudiness;
    [Range(0f, 1f)] public float precipitation;
}

public enum CasualWeather { Clear = 0, Cloudy = 1, Windy = 2 }
