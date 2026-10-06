using UnityEngine;

/// <summary>Art for the Free Gift chest, Store and pitch. Resources/MonetizationArt so the self-bootstrapping
/// UI needs no scene wiring. Swap the chest sprites for a sack/basket/animation later.</summary>
public class MonetizationArt : ScriptableObject
{
    [Header("Chest")]
    public Sprite chestClosed;
    public Sprite chestOpen;

    [Header("Currency icons")]
    public Sprite gemIcon;
    public Sprite coinIcon;

    [Header("Store")]
    [Tooltip("Hero art for the Farmer's Pass card and pitch (user will supply). Falls back to the closed chest.")]
    public Sprite passArt;

    [Tooltip("Pixel font for the big reward numbers and titles (UITK TextCore FontAsset).")]
    public UnityEngine.TextCore.Text.FontAsset numberFont;

    private static MonetizationArt cached;
    public static MonetizationArt Instance =>
        cached != null ? cached : (cached = Resources.Load<MonetizationArt>("MonetizationArt"));
}
