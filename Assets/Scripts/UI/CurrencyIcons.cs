/// <summary>
/// TMP rich-text sprite tags for the currency icons, backed by the
/// Resources/Sprites/CurrencySprites TMP Sprite Asset (built via
/// "Farm Game/Build Currency Sprite Asset"). Use in any TextMeshPro (UGUI) string,
/// e.g. $"{cost} {CurrencyIcons.Coin}". NOTE: UI Toolkit labels do NOT support
/// inline sprites — use an icon VisualElement there instead.
/// </summary>
public static class CurrencyIcons
{
    public const string Coin    = "<sprite=\"CurrencySprites\" name=\"coin\">";
    public const string Cash    = "<sprite=\"CurrencySprites\" name=\"cash\">";
    public const string Gem     = "<sprite=\"CurrencySprites\" name=\"gem\">";
    public const string Compost = "<sprite=\"CurrencySprites\" name=\"compost\">";
}
