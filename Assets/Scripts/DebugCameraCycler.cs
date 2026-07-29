using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// DEBUG-only helper: cycles the Game view through each area camera with a single keypress
/// (Farm → Market → Lake → Greenhouse → Woods → back to Farm). "Farm" is simply all debug
/// cameras off, so the real Main Camera renders. Enforces exactly one zone camera active at a
/// time (they share a depth and would otherwise fight). Attach to the "DebugCameras" parent and
/// assign the zone cameras in cycle order.
/// </summary>
public class DebugCameraCycler : MonoBehaviour
{
    [Tooltip("Key that advances to the next area view (new Input System).")]
    [SerializeField] private Key cycleKey = Key.Backquote;

    [Tooltip("Zone cameras in cycle order AFTER Farm. Farm itself is index 0 = all of these off.")]
    [SerializeField] private GameObject[] zoneCameras;

    // 0 = Farm (Main Camera, all zone cams off); 1..N = zoneCameras[index-1].
    private int index;

    private void Awake()
    {
        index = 0;
        Apply();
    }

    private void Update()
    {
        if (Keyboard.current == null || zoneCameras == null || zoneCameras.Length == 0) return;
        if (Keyboard.current[cycleKey].wasPressedThisFrame)
        {
            index = (index + 1) % (zoneCameras.Length + 1);
            Apply();
        }
    }

    private void Apply()
    {
        for (int i = 0; i < zoneCameras.Length; i++)
            if (zoneCameras[i] != null) zoneCameras[i].SetActive(index == i + 1);

        string view = index == 0 ? "Farm (Main Camera)"
                    : (zoneCameras[index - 1] != null ? zoneCameras[index - 1].name : "?");
        Debug.Log($"[DebugCameraCycler] View → {view}  (press {cycleKey} to advance)");
    }
}
