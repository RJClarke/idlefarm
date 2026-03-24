using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

/// <summary>
/// Equipment upgrade panel for the DrawerUI.
/// Expects pre-built UI hierarchy (created by Tools > Build Equipment Panel).
/// Finds tile references on Start, then updates tile states on open and periodically.
/// </summary>
public class EquipmentMenuPanel : MenuPanel
{
    [Header("Unlock IDs (must be purchased in Market first)")]
    [SerializeField] private string scarecrowUnlockID = "scarecrow_unlock";
    [SerializeField] private string fenceUnlockID = "fence_unlock";
    [SerializeField] private string sprinklerUnlockID = "sprinkler_unlock";

    [Header("Upgrade Data (3 per equipment: ordered to match rows)")]
    [SerializeField] private UpgradeData[] scarecrowUpgrades; // AoE, Capacity, Cooldown
    [SerializeField] private UpgradeData[] fenceUpgrades;
    [SerializeField] private UpgradeData[] sprinklerUpgrades;

    [Header("Colors")]
    [SerializeField] private Color tilePurchasedBg = new Color(0.890f, 0.847f, 0.792f, 0.4f);
    [SerializeField] private Color tileAffordableBg = new Color(0.890f, 0.847f, 0.792f, 1f);
    [SerializeField] private Color tileCantAffordBg = new Color(0.890f, 0.847f, 0.792f, 0.4f);
    [SerializeField] private Color textNormal = new Color(0f, 0f, 0f, 1f);
    [SerializeField] private Color textMuted = new Color(0f, 0f, 0f, 0.5f);
    [SerializeField] private Color purchasedColor = new Color(0.2f, 0.6f, 0.2f, 0.8f);
    [SerializeField] private Color lockedCardBg = new Color(0.5f, 0.5f, 0.5f, 0.6f);
    [SerializeField] private Color lockedTextColor = new Color(0f, 0f, 0f, 0.3f);

    private List<TileRef> allTiles = new List<TileRef>();
    private bool isWired = false;
    private float refreshTimer = 0f;

    private struct CardRef
    {
        public string unlockID;
        public Transform cardTransform;
        public Image bgImage;
        public CanvasGroup canvasGroup;
    }
    private List<CardRef> allCards = new List<CardRef>();

    private struct TileRef
    {
        public UpgradeData data;
        public int level;
        public Image bgImage;
        public TextMeshProUGUI levelLabel;
        public TextMeshProUGUI bonusLabel;
        public TextMeshProUGUI costLabel;
        public Button button;
    }

    private void Start()
    {
        WireUpReferences();
    }

    protected override void OnPanelOpened()
    {
        if (!isWired) WireUpReferences();
        RefreshCardLockStates();
        RefreshAllTiles();
    }

    /// <summary>
    /// Walk the pre-built hierarchy and collect tile references.
    /// Expected structure: Card_X/Rows/X_RowN/Tiles/Tile_LvN
    /// </summary>
    private void WireUpReferences()
    {
        allTiles.Clear();
        allCards.Clear();

        // Map card names to their upgrade arrays and unlock IDs
        var cardMap = new Dictionary<string, UpgradeData[]>
        {
            { "Card_Scarecrow", scarecrowUpgrades },
            { "Card_Fence", fenceUpgrades },
            { "Card_Sprinkler", sprinklerUpgrades }
        };
        var unlockMap = new Dictionary<string, string>
        {
            { "Card_Scarecrow", scarecrowUnlockID },
            { "Card_Fence", fenceUnlockID },
            { "Card_Sprinkler", sprinklerUnlockID }
        };

        foreach (Transform cardTransform in transform)
        {
            if (!cardMap.TryGetValue(cardTransform.name, out UpgradeData[] upgrades))
                continue;

            // Track card for unlock gating
            var cg = cardTransform.GetComponent<CanvasGroup>();
            if (cg == null) cg = cardTransform.gameObject.AddComponent<CanvasGroup>();
            allCards.Add(new CardRef
            {
                unlockID = unlockMap.ContainsKey(cardTransform.name) ? unlockMap[cardTransform.name] : "",
                cardTransform = cardTransform,
                bgImage = cardTransform.GetComponent<Image>(),
                canvasGroup = cg
            });
            if (upgrades == null || upgrades.Length < 3)
                continue;

            // Find Rows container directly under card
            Transform rows = cardTransform.Find("Rows");
            if (rows == null) continue;

            int rowIndex = 0;
            foreach (Transform row in rows)
            {
                if (rowIndex >= upgrades.Length) break;
                UpgradeData upgradeData = upgrades[rowIndex];
                if (upgradeData == null) { rowIndex++; continue; }

                // Update title/desc text from UpgradeData
                var titleTMP = row.Find("Title")?.GetComponent<TextMeshProUGUI>();
                if (titleTMP != null) titleTMP.text = upgradeData.displayName;

                var descTMP = row.Find("Desc")?.GetComponent<TextMeshProUGUI>();
                if (descTMP != null) descTMP.text = upgradeData.description;

                // Find tiles
                Transform tilesContainer = row.Find("Tiles");
                if (tilesContainer == null) { rowIndex++; continue; }

                for (int t = 0; t < tilesContainer.childCount; t++)
                {
                    Transform tile = tilesContainer.GetChild(t);
                    int level = t + 1;

                    var tileRef = new TileRef
                    {
                        data = upgradeData,
                        level = level,
                        bgImage = tile.GetComponent<Image>(),
                        levelLabel = tile.Find("LevelLabel")?.GetComponent<TextMeshProUGUI>(),
                        bonusLabel = tile.Find("BonusLabel")?.GetComponent<TextMeshProUGUI>(),
                        costLabel = tile.Find("CostLabel")?.GetComponent<TextMeshProUGUI>(),
                        button = tile.GetComponent<Button>()
                    };

                    // Set bonus text from data
                    if (tileRef.bonusLabel != null)
                        tileRef.bonusLabel.text = upgradeData.GetBonusText(level);

                    // Wire button click
                    if (tileRef.button != null)
                    {
                        int capturedLevel = level;
                        UpgradeData capturedData = upgradeData;
                        tileRef.button.onClick.RemoveAllListeners();
                        tileRef.button.onClick.AddListener(() => OnTileClicked(capturedData, capturedLevel));
                    }

                    allTiles.Add(tileRef);
                }

                rowIndex++;
            }
        }

        isWired = allTiles.Count > 0;
    }

    private void OnTileClicked(UpgradeData data, int level)
    {
        if (UpgradeManager.Instance == null) return;

        int currentPermanent = UpgradeManager.Instance.GetPermanentLevel(data.upgradeID);
        if (level != currentPermanent + 1) return;

        int cost = data.GetCoinCost(level);
        if (CurrencyManager.Instance == null || !CurrencyManager.Instance.CanAffordCoins(cost))
            return;

        if (UpgradeManager.Instance.PurchasePermanentUpgrade(data.upgradeID, cost))
            RefreshAllTiles();
    }

    private bool IsEquipmentUnlocked(string unlockID)
    {
        if (string.IsNullOrEmpty(unlockID)) return true;
        if (UpgradeManager.Instance == null) return false;
        return UpgradeManager.Instance.GetPermanentLevel(unlockID) > 0;
    }

    private void RefreshCardLockStates()
    {
        for (int i = 0; i < allCards.Count; i++)
        {
            CardRef card = allCards[i];
            bool unlocked = IsEquipmentUnlocked(card.unlockID);

            if (card.canvasGroup != null)
            {
                card.canvasGroup.alpha = unlocked ? 1f : 0.4f;
                card.canvasGroup.interactable = unlocked;
                card.canvasGroup.blocksRaycasts = unlocked;
            }
        }
    }

    private void RefreshAllTiles()
    {
        if (UpgradeManager.Instance == null) return;

        for (int i = 0; i < allTiles.Count; i++)
        {
            TileRef tile = allTiles[i];
            int permanentLevel = UpgradeManager.Instance.GetPermanentLevel(tile.data.upgradeID);
            int nextLevel = permanentLevel + 1;

            if (tile.level <= permanentLevel)
                SetTilePurchased(tile);
            else if (tile.level == nextLevel)
            {
                int cost = tile.data.GetCoinCost(tile.level);
                bool canAfford = CurrencyManager.Instance != null &&
                                 CurrencyManager.Instance.CanAffordCoins(cost);
                if (canAfford) SetTileAffordable(tile, cost);
                else SetTileCantAfford(tile, cost);
            }
            else
                SetTileCantAfford(tile, tile.data.GetCoinCost(tile.level));
        }
    }

    private void SetTilePurchased(TileRef tile)
    {
        if (tile.bgImage != null) tile.bgImage.color = tilePurchasedBg;
        if (tile.levelLabel != null) tile.levelLabel.color = textMuted;
        if (tile.bonusLabel != null) tile.bonusLabel.color = textMuted;
        if (tile.costLabel != null)
        {
            tile.costLabel.text = "\u2713";
            tile.costLabel.fontSize = 14;
            tile.costLabel.color = purchasedColor;
        }
        if (tile.button != null) tile.button.interactable = false;
    }

    private void SetTileAffordable(TileRef tile, int cost)
    {
        if (tile.bgImage != null) tile.bgImage.color = tileAffordableBg;
        if (tile.levelLabel != null) tile.levelLabel.color = textNormal;
        if (tile.bonusLabel != null) tile.bonusLabel.color = textNormal;
        if (tile.costLabel != null)
        {
            tile.costLabel.text = $"{cost}c";
            tile.costLabel.fontSize = 9;
            tile.costLabel.color = textNormal;
        }
        if (tile.button != null) tile.button.interactable = true;
    }

    private void SetTileCantAfford(TileRef tile, int cost)
    {
        if (tile.bgImage != null) tile.bgImage.color = tileCantAffordBg;
        if (tile.levelLabel != null) tile.levelLabel.color = textMuted;
        if (tile.bonusLabel != null) tile.bonusLabel.color = textMuted;
        if (tile.costLabel != null)
        {
            tile.costLabel.text = $"{cost}c";
            tile.costLabel.fontSize = 9;
            tile.costLabel.color = textMuted;
        }
        if (tile.button != null) tile.button.interactable = false;
    }

    private void Update()
    {
        if (isWired && gameObject.activeInHierarchy)
        {
            refreshTimer += Time.deltaTime;
            if (refreshTimer >= 1f)
            {
                refreshTimer = 0f;
                RefreshCardLockStates();
                RefreshAllTiles();
            }
        }
    }
}
