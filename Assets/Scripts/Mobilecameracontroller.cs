using UnityEngine;

/// <summary>
/// Ensures the camera is properly sized and centered for mobile devices
/// Auto-adjusts orthographic size to fit the grid on any screen size
/// </summary>
public class MobileCameraController : MonoBehaviour
{
    [Header("Grid Bounds")]
    [SerializeField] private float gridWidth = 3.5f;  // Total width of your farm grid
    [SerializeField] private float gridHeight = 1.5f; // Total height of your farm grid
    [SerializeField] private float padding = 0.5f;    // Extra space around grid

    [Header("Camera Settings")]
    [SerializeField] private float minOrthographicSize = 2f;
    [SerializeField] private float maxOrthographicSize = 10f;

    private Camera cam;

    private void Awake()
    {
        cam = GetComponent<Camera>();
        if (cam == null)
        {
            Debug.LogError("MobileCameraController requires a Camera component!");
            return;
        }
    }

    private void Start()
    {
        AdjustCameraToFitGrid();
    }

    /// <summary>
    /// Adjust camera orthographic size to fit the grid with padding
    /// Works for any screen aspect ratio (portrait or landscape)
    /// </summary>
    public void AdjustCameraToFitGrid()
    {
        if (cam == null) return;

        // Get screen aspect ratio
        float screenAspect = (float)Screen.width / (float)Screen.height;

        // Calculate required camera size to fit grid width
        float requiredHeightForWidth = (gridWidth + padding * 2) / screenAspect;

        // Calculate required camera size to fit grid height  
        float requiredHeightForHeight = (gridHeight + padding * 2);

        // Use whichever is larger (ensures both dimensions fit)
        float targetSize = Mathf.Max(requiredHeightForWidth, requiredHeightForHeight) / 2f;

        // Clamp to min/max
        targetSize = Mathf.Clamp(targetSize, minOrthographicSize, maxOrthographicSize);

        // Apply to camera
        cam.orthographicSize = targetSize;

        Debug.Log($"Camera adjusted: Ortho Size = {targetSize}, Screen = {Screen.width}x{Screen.height}, Aspect = {screenAspect:F2}");
    }

    /// <summary>
    /// Call this when grid size changes (e.g., grid expansion unlock)
    /// </summary>
    public void UpdateGridBounds(float newWidth, float newHeight)
    {
        gridWidth = newWidth;
        gridHeight = newHeight;
        AdjustCameraToFitGrid();
    }

    /// <summary>
    /// Recalculate on screen size change (device rotation, etc.)
    /// </summary>
    private void OnRectTransformDimensionsChange()
    {
        AdjustCameraToFitGrid();
    }

#if UNITY_EDITOR
    // Visualize grid bounds in editor
    private void OnDrawGizmos()
    {
        Gizmos.color = Color.yellow;
        Vector3 center = transform.position;
        center.z = 0;
        Gizmos.DrawWireCube(center, new Vector3(gridWidth, gridHeight, 0));
        
        Gizmos.color = Color.green;
        Gizmos.DrawWireCube(center, new Vector3(gridWidth + padding * 2, gridHeight + padding * 2, 0));
    }
#endif
}