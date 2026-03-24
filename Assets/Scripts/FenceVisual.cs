using UnityEngine;

/// <summary>
/// Scene-placed fence for a single zone. All fence post GameObjects are
/// children, hand-positioned in the editor with their own sprites.
///
/// Each level group contains the NEW posts added at that level (extending
/// both horizontal and vertical edges from the corner).
///
/// Everything starts hidden. EquipmentManager calls Show(level) when a
/// fence is equipped and a run starts, and Hide() when the run ends.
/// </summary>
public class FenceVisual : MonoBehaviour
{
    [Header("Zone Configuration")]
    [Tooltip("Which zone this fence belongs to (1-4)")]
    public int zoneId = 1;

    [Header("Level Groups — drag in the NEW posts added at each level")]
    [Tooltip("Level 1: Corner piece(s)")]
    [SerializeField] private GameObject[] level1Posts;

    [Tooltip("Level 2: Extensions added at level 2 (horizontal + vertical)")]
    [SerializeField] private GameObject[] level2Posts;

    [Tooltip("Level 3: Extensions added at level 3")]
    [SerializeField] private GameObject[] level3Posts;

    [Tooltip("Level 4: Extensions added at level 4")]
    [SerializeField] private GameObject[] level4Posts;

    [Tooltip("Level 5: Final extensions to complete both edges")]
    [SerializeField] private GameObject[] level5Posts;

    private GameObject[][] allLevels;

    private void Awake()
    {
        allLevels = new GameObject[][] { level1Posts, level2Posts, level3Posts, level4Posts, level5Posts };

        // Start with everything hidden
        Hide();
    }

    /// <summary>
    /// Show fence posts up to the given level (1-5).
    /// Called by EquipmentManager when a run starts with a fence equipped.
    /// </summary>
    public void Show(int level)
    {
        for (int i = 0; i < allLevels.Length; i++)
        {
            bool show = i < level;
            SetGroupActive(allLevels[i], show);
        }
    }

    /// <summary>
    /// Hide all fence posts.
    /// Called by EquipmentManager when the run ends.
    /// </summary>
    public void Hide()
    {
        for (int i = 0; i < allLevels.Length; i++)
            SetGroupActive(allLevels[i], false);
    }

    private void SetGroupActive(GameObject[] group, bool active)
    {
        if (group == null) return;
        for (int i = 0; i < group.Length; i++)
        {
            if (group[i] != null)
                group[i].SetActive(active);
        }
    }
}
