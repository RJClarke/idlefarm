using System;
using System.Collections.Generic;
using UnityEngine;

namespace Research
{
    /// <summary>
    /// Maps a <see cref="ResearchData.branchID"/> to the icon that represents that branch.
    /// Authored once here and read by both the research picker's section headers
    /// (ResearchPopupUITK) and the research toast (ToastManager), so the two cannot drift apart.
    ///
    /// The asset lives at Resources/Research/BranchIcons and resolves through
    /// <see cref="Resources.Load"/>, so there is nothing to wire in the scene — the same
    /// convention ResearchManager uses for Resources.LoadAll&lt;ResearchData&gt;("Research").
    /// </summary>
    [CreateAssetMenu(menuName = "Farm Game/Research Branch Icons", fileName = "BranchIcons", order = 10)]
    public class ResearchBranchIcons : ScriptableObject
    {
        public const string ResourcePath = "Research/BranchIcons";

        [Serializable]
        public class Entry
        {
            public string branchID;
            public Sprite icon;
        }

        [Tooltip("One entry per ResearchData.branchID: soil, helper, plant, animals, equipment, " +
                 "weather, meta. A branch with no entry simply renders without an icon.")]
        [SerializeField] private List<Entry> entries = new List<Entry>();

        // Built on first lookup; cleared by OnValidate so editing the asset takes effect live.
        private Dictionary<string, Sprite> lookup;

        /// <summary>
        /// Icon for a branch, or null when the branch is blank, absent from the table, or
        /// present with no sprite assigned. Callers treat null as "render text only".
        /// </summary>
        public Sprite Lookup(string branchID)
        {
            if (string.IsNullOrEmpty(branchID)) return null;

            if (lookup == null)
            {
                lookup = new Dictionary<string, Sprite>();
                if (entries != null)
                {
                    foreach (Entry e in entries)
                    {
                        if (e == null || string.IsNullOrEmpty(e.branchID)) continue;
                        lookup[e.branchID] = e.icon; // last duplicate wins
                    }
                }
            }

            return lookup.TryGetValue(branchID, out Sprite s) ? s : null;
        }

        private void OnValidate() => lookup = null;

        // ── Static resolution ────────────────────────

        private static ResearchBranchIcons cached;
        private static bool loadAttempted;

        /// <summary>The Resources-loaded table, or null if the asset is missing.</summary>
        public static ResearchBranchIcons Asset
        {
            get
            {
                if (!loadAttempted)
                {
                    loadAttempted = true;
                    cached = Resources.Load<ResearchBranchIcons>(ResourcePath);
                }
                return cached;
            }
        }

        /// <summary>Icon for a branch. Null-safe when the asset itself is missing.</summary>
        public static Sprite For(string branchID)
        {
            ResearchBranchIcons a = Asset;
            return a != null ? a.Lookup(branchID) : null;
        }

        /// <summary>
        /// Test hook: point the static resolver at an in-memory table. Passing null clears the
        /// override so the next call falls back to the Resources lookup.
        /// </summary>
        public static void SetAssetForTests(ResearchBranchIcons asset)
        {
            cached = asset;
            loadAttempted = asset != null;
        }

        /// <summary>Test hook: build a table in memory without an asset file.</summary>
        public static ResearchBranchIcons CreateForTests(params (string branchID, Sprite icon)[] rows)
        {
            var t = CreateInstance<ResearchBranchIcons>();
            t.entries = new List<Entry>();
            foreach (var r in rows)
                t.entries.Add(new Entry { branchID = r.branchID, icon = r.icon });
            return t;
        }
    }
}
