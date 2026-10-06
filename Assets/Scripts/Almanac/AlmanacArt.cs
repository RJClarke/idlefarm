using UnityEngine;

/// <summary>
/// The Farmer's Almanac's own art, beyond the Barn board it borrows: the title font, the notebook
/// tab and page sprites, and pest portraits. Lives at Resources/AlmanacArt so the self-bootstrapping
/// popup can find it with no scene wiring. Built by Farm Game > Almanac > Build Art Asset.
/// </summary>
public class AlmanacArt : ScriptableObject
{
    [Tooltip("Pixel font for the \"Farmer's Almanac\" title (UITK TextCore FontAsset), drawn at its bake size.")]
    public UnityEngine.TextCore.Text.FontAsset titleFont;
    [Tooltip("Pixel font for the tabs. Falls back to the title font.")]
    public UnityEngine.TextCore.Text.FontAsset tabFont;

    [Header("Tabs (9-sliced)")]
    public Sprite tabOff;
    public Sprite tabOn;
    public int tabSlice = 4;

    [Header("Entry page (9-sliced paper)")]
    public Sprite page;
    public int pageSlice = 3;

    [Header("Pest portraits")]
    public Sprite deerIcon;
    public Sprite crowIcon;

    private static AlmanacArt cached;
    public static AlmanacArt Instance => cached != null ? cached : (cached = Resources.Load<AlmanacArt>("AlmanacArt"));
}
