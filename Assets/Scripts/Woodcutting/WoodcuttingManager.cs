using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Owns woodcutting meta-state: the axe level, its tuning, and the upgrade transaction.
/// Wood the resource lives on CurrencyManager; this is the domain state around it.
/// Axe level persists via GameData.axeLevel (SaveManager).
/// </summary>
public class WoodcuttingManager : MonoBehaviour
{
    public static WoodcuttingManager Instance { get; private set; }

    [Header("Axe Tuning")]
    [SerializeField] private int maxAxeLevel = 3;
    [SerializeField] private int hitsReductionPerLevel = 1;
    [Tooltip("Coin cost per next axe level, indexed by current level (0 -> level 1, ...).")]
    [SerializeField] private int[] axeCoinCosts = { 250, 750, 2000 };
    [Tooltip("Wood cost per next axe level, indexed by current level.")]
    [SerializeField] private int[] axeWoodCosts = { 20, 60, 150 };

    [Header("First Axe Purchase")]
    [Tooltip("Coins-only cost to buy your first axe. Wood-free because you can't gather Wood until you own one.")]
    [SerializeField] private int firstAxeCoinCost = 75;

    [Header("Hints")]
    [Tooltip("Text of the world-space popup shown when the player taps a tree without owning an axe.")]
    [SerializeField] private string noAxeHintText = "You need to buy an axe first.";

    [Header("Tapping")]
    [Tooltip("World-space forgiveness radius: a tap within this distance of a tree's center chops the " +
             "nearest such tree. Larger = easier to hit small/low placeholder trees. Trees are ~5+ units " +
             "apart, so keep it under that to avoid ambiguity.")]
    [SerializeField] private float tapRadius = 6f;

    private int axeLevel;
    private bool hasAxe;
    private WorldHintPopup activeHint;

    public int AxeLevel => axeLevel;
    public int MaxAxeLevel => maxAxeLevel;
    public int HitsReductionPerLevel => hitsReductionPerLevel;
    /// <summary>True once the player has bought their first axe. Until then, no tree can be chopped
    /// and the axe-upgrade rows stay hidden ("no leveling until you've bought the axe").</summary>
    public bool HasAxe => hasAxe;
    public int FirstAxeCoinCost => firstAxeCoinCost;
    public event Action<int> OnAxeLevelChanged;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    // ── Tap routing ──────────────────────────────────────────────
    // One place reads the pointer and routes a tap to the nearest tree within tapRadius. This is
    // forgiving on small/low placeholder trees (exact per-collider hit-testing missed constantly)
    // and guarantees a single tree handles each tap.
    private void Update()
    {
        if (!AtWoods()) return;
        if (!TryReadTap(out Vector2 screenPos)) return;
        TreeNode tree = NearestTreeToTap(screenPos);
        if (tree != null) tree.HandleTap();
    }

    private TreeNode NearestTreeToTap(Vector2 screenPos)
    {
        Camera cam = Camera.main;
        if (cam == null) return null;
        Vector3 world = cam.ScreenToWorldPoint(new Vector3(screenPos.x, screenPos.y, -cam.transform.position.z));

        TreeNode best = null;
        float bestDist = tapRadius; // ignore taps farther than tapRadius from every tree
        foreach (var t in registeredTrees)
        {
            if (t == null) continue;
            float d = Vector2.Distance(world, t.transform.position);
            if (d < bestDist) { bestDist = d; best = t; }
        }
        return best;
    }

    private static bool AtWoods()
    {
        var pan = Camera.main != null ? Camera.main.GetComponent<CameraPanController>() : null;
        return pan != null && !pan.IsPanning && pan.CurrentLocation == CameraPanController.Location.Woods;
    }

    private static bool TryReadTap(out Vector2 screenPos)
    {
        screenPos = default;
        if (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.wasPressedThisFrame)
        {
            screenPos = Touchscreen.current.primaryTouch.position.ReadValue();
            return true;
        }
        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
        {
            screenPos = Mouse.current.position.ReadValue();
            return true;
        }
        return false;
    }

    public int NextUpgradeCoinCost()
    {
        if (axeLevel >= maxAxeLevel || axeCoinCosts.Length == 0) return int.MaxValue;
        return axeCoinCosts[Mathf.Min(axeLevel, axeCoinCosts.Length - 1)];
    }

    public int NextUpgradeWoodCost()
    {
        if (axeLevel >= maxAxeLevel || axeWoodCosts.Length == 0) return int.MaxValue;
        return axeWoodCosts[Mathf.Min(axeLevel, axeWoodCosts.Length - 1)];
    }

    // ── First axe purchase (Coins only, one-time) ────────────────
    public bool CanBuyAxe()
    {
        var cm = CurrencyManager.Instance;
        if (cm == null) return false;
        return WoodcuttingMath.CanBuyAxe(hasAxe, cm.Coins, firstAxeCoinCost);
    }

    /// <summary>Spends Coins and grants the first axe. Returns false if already owned or unaffordable.</summary>
    public bool TryBuyAxe()
    {
        if (!CanBuyAxe()) return false;
        var cm = CurrencyManager.Instance;
        if (!cm.SpendCoins(firstAxeCoinCost)) return false;
        hasAxe = true;
        OnAxeLevelChanged?.Invoke(axeLevel); // refresh listeners (Carpenter UI)
        return true;
    }

    public void SetHasAxe(bool value)
    {
        hasAxe = value;
        OnAxeLevelChanged?.Invoke(axeLevel);
    }

    /// <summary>Spawn the "buy an axe first" hint at a world position, replacing any live one so a
    /// repeated tap re-shows it anchored to the newly-tapped tree.</summary>
    public void ShowAxeHint(Vector3 worldPos)
    {
        if (activeHint != null) Destroy(activeHint.gameObject);
        activeHint = WorldHintPopup.Create(worldPos, noAxeHintText);
    }

    public bool CanUpgradeAxe()
    {
        var cm = CurrencyManager.Instance;
        if (cm == null || !hasAxe) return false;
        return WoodcuttingMath.CanUpgradeAxe(axeLevel, maxAxeLevel, cm.Coins, NextUpgradeCoinCost(), cm.Wood, NextUpgradeWoodCost());
    }

    /// <summary>Spends Coins + Wood and raises the axe level by one. Returns false if not allowed.</summary>
    public bool TryUpgradeAxe()
    {
        if (!CanUpgradeAxe()) return false;
        var cm = CurrencyManager.Instance;
        int coinCost = NextUpgradeCoinCost();
        int woodCost = NextUpgradeWoodCost();
        if (!cm.SpendCoins(coinCost)) return false;
        if (!cm.SpendWood(woodCost)) { cm.AddCoins(coinCost); return false; } // refund coins if wood spend fails
        axeLevel++;
        OnAxeLevelChanged?.Invoke(axeLevel);
        return true;
    }

    public void SetAxeLevel(int level)
    {
        axeLevel = Mathf.Clamp(level, 0, maxAxeLevel);
        OnAxeLevelChanged?.Invoke(axeLevel);
    }

    // ── Tree growth persistence ──────────────────────────────────
    // Trees are per-scene objects; this DontDestroyOnLoad manager owns their saved growth
    // state. Registration and LoadTreeStates reconcile in either order (load-race safe): a
    // tree registering after load pulls its pending state; a load after registration pushes
    // state onto already-present trees.

    private readonly List<TreeNode> registeredTrees = new List<TreeNode>();
    private Dictionary<string, TreeSaveState> pendingTreeStates;

    public void RegisterTree(TreeNode tree)
    {
        if (tree == null || registeredTrees.Contains(tree)) return;
        registeredTrees.Add(tree);
        if (pendingTreeStates != null && pendingTreeStates.TryGetValue(tree.TreeId, out var state))
            tree.ApplySaveState(state);
    }

    public void UnregisterTree(TreeNode tree)
    {
        registeredTrees.Remove(tree);
    }

    /// <summary>Snapshot every live tree's growth state for saving.</summary>
    public TreeSaveState[] GetTreeSaveStates()
    {
        var list = new List<TreeSaveState>(registeredTrees.Count);
        foreach (var tree in registeredTrees)
            if (tree != null) list.Add(tree.CaptureSaveState());
        return list.ToArray();
    }

    /// <summary>Stash loaded tree states and apply to any tree that's already registered.</summary>
    public void LoadTreeStates(TreeSaveState[] states)
    {
        pendingTreeStates = new Dictionary<string, TreeSaveState>();
        if (states != null)
            foreach (var s in states)
                if (s != null && !string.IsNullOrEmpty(s.treeId))
                    pendingTreeStates[s.treeId] = s;

        foreach (var tree in registeredTrees)
            if (tree != null && pendingTreeStates.TryGetValue(tree.TreeId, out var state))
                tree.ApplySaveState(state);
    }
}
