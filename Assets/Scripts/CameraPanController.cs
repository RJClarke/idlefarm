using System;
using UnityEngine;

/// <summary>
/// Snap-to-location camera panning between named spots (Farm, Greenhouse, ...).
/// Sits on Main Camera alongside MobileCameraController. Tweens transform.position
/// on X/Y only (Z preserved). Disabled while a run is active.
/// </summary>
public class CameraPanController : MonoBehaviour
{
    public enum Location { Farm, Greenhouse }

    [Serializable]
    public class LocationOffset
    {
        public Location location;
        public Vector2 offset; // world units, relative to Farm home position
    }

    [Header("Locations")]
    [Tooltip("Offsets from the camera's starting position. Farm should be (0,0).")]
    [SerializeField] private LocationOffset[] locations = new LocationOffset[]
    {
        new LocationOffset { location = Location.Farm,       offset = Vector2.zero },
        new LocationOffset { location = Location.Greenhouse, offset = new Vector2(5f, 8f) },
    };

    [Header("Tween")]
    [SerializeField] private float panDuration = 0.55f;
    [SerializeField] private LeanTweenType easeType = LeanTweenType.easeInOutQuad;

    [Header("Behavior")]
    [Tooltip("If true, pans are blocked while a run is active.")]
    [SerializeField] private bool blockDuringRun = true;

    public event Action<Location> OnPanStarted;
    public event Action<Location> OnPanCompleted;

    public Location CurrentLocation { get; private set; } = Location.Farm;
    public bool IsPanning { get; private set; }

    private Vector3 homePosition;
    private int activeTweenId = -1;

    private void Awake()
    {
        homePosition = transform.position;
    }

    public void PanTo(Location target)
    {
        if (target == CurrentLocation && !IsPanning) return;
        if (blockDuringRun && RunManager.Instance != null && RunManager.Instance.IsRunActive)
        {
            Debug.LogWarning("[CameraPanController] Pan blocked: run is active.");
            return;
        }

        Vector2 offset = GetOffset(target);
        Vector3 destination = new Vector3(homePosition.x + offset.x, homePosition.y + offset.y, transform.position.z);

        if (activeTweenId != -1) LeanTween.cancel(activeTweenId);

        IsPanning = true;
        OnPanStarted?.Invoke(target);

        activeTweenId = LeanTween.move(gameObject, destination, panDuration)
            .setEase(easeType)
            .setOnComplete(() =>
            {
                IsPanning = false;
                activeTweenId = -1;
                CurrentLocation = target;
                OnPanCompleted?.Invoke(target);
            })
            .id;
    }

    // Convenience for UnityEvent button hookups (UI Buttons can't pass enums directly).
    public void PanToFarm()       => PanTo(Location.Farm);
    public void PanToGreenhouse() => PanTo(Location.Greenhouse);

    public void ToggleFarmGreenhouse()
    {
        PanTo(CurrentLocation == Location.Farm ? Location.Greenhouse : Location.Farm);
    }

    private Vector2 GetOffset(Location target)
    {
        for (int i = 0; i < locations.Length; i++)
            if (locations[i].location == target) return locations[i].offset;
        return Vector2.zero;
    }
}
