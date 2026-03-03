/// <summary>
/// Categories for different crop types
/// Based on growth speed and botanical classification
/// </summary>
public enum CropType
{
    // By Growth Speed
    FastLeafy,      // Arugula, Kale (2-3.5 min)
    FastRoot,       // Radish (3 min)
    Medium,         // Peas, Cucumber, Green Bean (5-6 min)
    Slow,           // Tomato, Pepper, Eggplant, Broccoli (6-8.5 min)
    VerySlow,       // Onion, Garlic, Cabbage (8.5-12+ min)
    
    // By Botanical Type
    Root,           // Carrot, Potato, Beet, Radish, Onion, Garlic
    BerryBush,      // Blueberry, Blackberry
    BerryCane,      // Raspberry, Blackberry
    GroundBerry,    // Strawberry
    ViningFruit,    // Watermelon, Grapes
    LeafyGreen,     // Broccoli, Kale, Arugula, Cabbage
    SlowVegetable   // Tomato, Pepper, Corn, Eggplant, Cucumber
}