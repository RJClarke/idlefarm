using UnityEngine;

/// <summary>
/// Defines one tree type the player can chop in the Woods. Trees grow continuously from a
/// sapling to full grown over <see cref="growSeconds"/>, stepping through <see cref="stageCount"/>
/// stages. You can chop at any stage — cutting early yields only a portion of the wood and
/// restarts growth from a fresh sapling.
/// </summary>
[CreateAssetMenu(fileName = "WoodTreeData", menuName = "IdleFarm/Wood Tree Data")]
public class WoodTreeData : ScriptableObject
{
    [Header("Identity")]
    public string treeName = "Softwood";

    [Header("Chopping")]
    [Tooltip("Base taps to fell a FULL-GROWN tree before axe reduction. Earlier stages fall in fewer.")]
    public int baseHitsToFell = 6;
    [Tooltip("Wood awarded for felling a FULL-GROWN tree. Earlier stages give a proportional share.")]
    public int woodYield = 50;
    [Tooltip("Minimum axe level required to fell this tree (0 = bare hands).")]
    public int requiredAxeLevel = 0;

    [Header("Growth")]
    [Tooltip("Seconds for a fresh sapling to reach full grown.")]
    public float growSeconds = 60f;
    [Tooltip("Number of growth stages (sapling .. full grown).")]
    public int stageCount = 5;
    [Tooltip("World scale of a stage-0 sapling.")]
    public float saplingScale = 1.2f;
    [Tooltip("World scale of a full-grown tree.")]
    public float fullScale = 4f;

    [Header("Visuals")]
    [Tooltip("Sprite used at every stage when stageSprites is empty.")]
    public Sprite standingSprite;
    [Tooltip("Optional per-stage art (sapling..full). If set, overrides standingSprite by stage index.")]
    public Sprite[] stageSprites;

    /// <summary>Sprite to show at a given stage — per-stage art if provided, else the single standing sprite.</summary>
    public Sprite SpriteForStage(int stageIndex)
    {
        if (stageSprites != null && stageSprites.Length > 0)
            return stageSprites[Mathf.Clamp(stageIndex, 0, stageSprites.Length - 1)];
        return standingSprite;
    }
}
