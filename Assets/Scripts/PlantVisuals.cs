using UnityEngine;

/// <summary>
/// Handles visual representation of a plant as it grows
/// Updates sprites based on current growth stage
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
public class PlantVisuals : MonoBehaviour
{
    [Header("Visual Settings")]
    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private float spriteScale = 1.0f;

    [Header("Current Display (Read-Only)")]
    [SerializeField] private GrowthStage displayedStage;

    private void Awake()
    {
        if (spriteRenderer == null)
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
        }

        // Depth-sort crops by their tile Y so the helper/animals pass in front/behind correctly.
        YSort.Ensure(gameObject, isStatic: true);
    }

    /// <summary>
    /// Update the sprite based on current growth stage
    /// Phase 3.3: Can show dried-out (wilted) appearance
    /// Phase 4.2: Can show rotting (brown/moldy) appearance
    /// </summary>
    public void UpdateVisuals(GrowthStage stage, CropData cropData, bool isDriedOut = false, bool isRotting = false)
    {
        if (cropData == null)
        {
            Debug.LogWarning("Cannot update visuals - no crop data!");
            return;
        }

        displayedStage = stage;

        // Get sprite for current stage
        Sprite stageSprite = cropData.GetStageSprite(stage);

        if (stageSprite != null)
        {
            spriteRenderer.sprite = stageSprite;
            transform.localScale = Vector3.one * spriteScale;

            // Position sprites with Bottom Center pivot at tile center
            transform.localPosition = new Vector3(0, -0.5f, 0);
        }
        else
        {
            // No sprite assigned - use colored squares as placeholder
            SetPlaceholderVisual(stage);
        }

        // Phase 4.2: Rotting takes priority over dried out
        if (isRotting)
        {
            // Brown/moldy appearance for rotting plants
            spriteRenderer.color = new Color(0.4f, 0.3f, 0.2f, 1f); // Brown
        }
        // Phase 3.3: Apply dried-out visual effect
        else if (isDriedOut)
        {
            // Desaturate and darken - grey/wilted appearance
            spriteRenderer.color = new Color(0.5f, 0.5f, 0.5f, 1f);
        }
        else
        {
            // Normal color (placeholder already sets color in SetPlaceholderVisual)
            if (stageSprite != null)
            {
                spriteRenderer.color = Color.white;
            }
        }

        // Ensure renderer is enabled
        spriteRenderer.enabled = true;
    }

    /// <summary>
    /// Set placeholder visual (colored square) when no sprite is assigned
    /// Useful for testing before art is ready
    /// </summary>
    private void SetPlaceholderVisual(GrowthStage stage)
    {
        // Create a simple colored square to represent the stage
        Color stageColor = GetStageColor(stage);
        spriteRenderer.color = stageColor;

        // If no sprite at all, create a default white square sprite
        if (spriteRenderer.sprite == null)
        {
            spriteRenderer.sprite = CreateDefaultSprite();
        }
    }

    /// <summary>
    /// Get color representing each growth stage
    /// </summary>
    private Color GetStageColor(GrowthStage stage)
    {
        switch (stage)
        {
            case GrowthStage.Seed:
                return new Color(0.2f, 0.2f, 0.2f); // #333333 - Dark grey
            case GrowthStage.Sprout:
                return new Color(0.6f, 0.9f, 0.4f); // Light green
            case GrowthStage.Sapling:
                return new Color(0.2f, 0.6f, 0.2f); // Dark green
            case GrowthStage.Harvestable:
                return new Color(1.0f, 0.9f, 0.2f); // Yellow
            default:
                return Color.green;
        }
    }

    /// <summary>
    /// Create a simple circular sprite for placeholder visuals
    /// Circle shape lets you see the soil underneath!
    /// </summary>
    private Sprite CreateDefaultSprite()
    {
        // Create a 32x32 texture with a circle
        int size = 32;
        Texture2D tex = new Texture2D(size, size);
        Color[] pixels = new Color[size * size];
        
        Vector2 center = new Vector2(size / 2f, size / 2f);
        float radius = size / 2f - 2f; // Slightly smaller than full size
        
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                int index = y * size + x;
                Vector2 pos = new Vector2(x, y);
                float distance = Vector2.Distance(pos, center);
                
                // If inside circle radius, make it white, otherwise transparent
                if (distance <= radius)
                {
                    pixels[index] = Color.white;
                }
                else
                {
                    pixels[index] = Color.clear; // Transparent outside circle
                }
            }
        }
        
        tex.SetPixels(pixels);
        tex.Apply();

        // Create sprite from texture
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 32f);
    }

    /// <summary>
    /// Set sprite scale (size of the visual)
    /// </summary>
    public void SetScale(float scale)
    {
        spriteScale = scale;
        transform.localScale = Vector3.one * spriteScale;
    }

    /// <summary>
    /// Flash or highlight the plant (for feedback)
    /// </summary>
    public void Flash(Color flashColor, float duration = 0.2f)
    {
        // Store original color
        Color originalColor = spriteRenderer.color;

        // Flash to new color
        spriteRenderer.color = flashColor;

        // Return to original after duration
        LeanTween.color(spriteRenderer.gameObject, originalColor, duration);
    }
}