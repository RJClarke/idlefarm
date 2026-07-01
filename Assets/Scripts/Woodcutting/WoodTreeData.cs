using UnityEngine;

/// <summary>Defines one tree type the player can chop in the Woods.</summary>
[CreateAssetMenu(fileName = "WoodTreeData", menuName = "IdleFarm/Wood Tree Data")]
public class WoodTreeData : ScriptableObject
{
    [Header("Identity")]
    public string treeName = "Softwood";

    [Header("Chopping")]
    [Tooltip("Base taps to fell before axe reduction.")]
    public int baseHitsToFell = 5;
    [Tooltip("Wood awarded when the tree falls.")]
    public int woodYield = 50;
    [Tooltip("Seconds for a stump to regrow into a full tree.")]
    public float regrowSeconds = 45f;
    [Tooltip("Minimum axe level required to fell this tree (0 = bare hands).")]
    public int requiredAxeLevel = 0;

    [Header("Visuals")]
    public Sprite standingSprite;
    public Sprite stumpSprite;
}
