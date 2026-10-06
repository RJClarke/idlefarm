using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Keeps the narrative copy in one place. LetterCatalog.asset (cast, letters, tips) is the live
/// source of truth; this tool
///   • Seed Missing Copy — appends any NarrativeDefaults entry whose id the asset lacks (never
///     overwrites text you've edited), then rewrites the reference doc.
///   • Write Copy Reference — renders the asset to docs/narrative/cast-and-copy.md: every
///     character with their voice notes, every letter in full, every tip, and when each fires.
/// </summary>
public static class NarrativeCopyTool
{
    private const string CatalogPath = "Assets/Resources/LetterCatalog.asset";
    private const string DocPath = "docs/narrative/cast-and-copy.md";

    [MenuItem("Farm Game/Narrative/Seed Missing Copy")]
    public static void SeedMissingCopy()
    {
        var catalog = AssetDatabase.LoadAssetAtPath<LetterCatalogSO>(CatalogPath);
        if (catalog == null) { Debug.LogError("[NarrativeCopy] No catalog at " + CatalogPath); return; }

        int added = 0;
        catalog.cast = Merge(catalog.cast, NarrativeDefaults.Cast, c => c.id, ref added);
        catalog.letters = Merge(catalog.letters, NarrativeDefaults.Letters, l => l.id, ref added);
        catalog.tips = Merge(catalog.tips, NarrativeDefaults.Tips, t => t.id, ref added);

        EditorUtility.SetDirty(catalog);
        AssetDatabase.SaveAssets();
        Debug.Log($"[NarrativeCopy] Seeded {added} missing entr{(added == 1 ? "y" : "ies")} into {CatalogPath}.");
        WriteCopyReference();
    }

    private static T[] Merge<T>(T[] existing, T[] defaults, Func<T, string> id, ref int added) where T : class
    {
        var list = (existing ?? new T[0]).Where(e => e != null).ToList();
        var have = new HashSet<string>(list.Select(id));
        foreach (T d in defaults)
            if (have.Add(id(d))) { list.Add(d); added++; }
        return list.ToArray();
    }

    [MenuItem("Farm Game/Narrative/Write Copy Reference")]
    public static void WriteCopyReference()
    {
        var catalog = AssetDatabase.LoadAssetAtPath<LetterCatalogSO>(CatalogPath);
        if (catalog == null) { Debug.LogError("[NarrativeCopy] No catalog at " + CatalogPath); return; }

        var letters = (catalog.letters ?? new LetterDef[0]).Where(l => l != null).ToList();
        var cast = (catalog.cast ?? new CastMember[0]).Where(c => c != null).ToList();
        var tips = (catalog.tips ?? new TipDef[0]).Where(t => t != null).ToList();

        var sb = new StringBuilder();
        sb.AppendLine("# Cast & Copy");
        sb.AppendLine();
        sb.AppendLine($"_Generated {DateTime.Now:yyyy-MM-dd HH:mm} from `{CatalogPath}` — don't edit this file by hand, it gets overwritten._");
        sb.AppendLine();
        sb.AppendLine("**To change copy:** select `LetterCatalog` in the Project window and edit it in the Inspector (Cast / Letters / Tips), " +
                      "then run **Farm Game > Narrative > Write Copy Reference** to refresh this page. Or just ask Claude.");
        sb.AppendLine();
        sb.AppendLine("`{farmName}` in a letter is replaced with the player's farm name. Emoji don't render on Android — keep copy to plain text.");
        sb.AppendLine();

        // ── Cast ──
        sb.AppendLine("## Cast");
        sb.AppendLine();
        sb.AppendLine("| Character | Who they are | Letters |");
        sb.AppendLine("|---|---|---|");
        foreach (var c in cast)
            sb.AppendLine($"| **{Cell(c.displayName)}** | {Cell(c.role)} | {letters.Count(l => l.senderName == c.displayName)} |");
        var uncast = letters.Select(l => l.senderName).Where(n => !string.IsNullOrEmpty(n) && cast.All(c => c.displayName != n)).Distinct().ToList();
        foreach (string n in uncast)
            sb.AppendLine($"| **{Cell(n)}** | _(not in the cast list — add them)_ | {letters.Count(l => l.senderName == n)} |");
        sb.AppendLine();

        // ── Letters at a glance ──
        sb.AppendLine("## All letters at a glance");
        sb.AppendLine();
        sb.AppendLine("| Letter | From | Arrives when | Gift | Button |");
        sb.AppendLine("|---|---|---|---|---|");
        foreach (var l in letters)
            sb.AppendLine($"| {Cell(l.subject)} | {Cell(l.senderName)} | {Cell(When(l))} | {Gift(l)} | {Button(l.ctaKind)} |");
        sb.AppendLine();

        // ── Per character ──
        sb.AppendLine("## Letters by character");
        foreach (var c in cast.Select(c => (name: c.displayName, member: c)).Concat(uncast.Select(n => (name: n, member: (CastMember)null))))
        {
            sb.AppendLine();
            sb.AppendLine($"### {c.name}");
            if (c.member != null)
            {
                sb.AppendLine();
                if (!string.IsNullOrWhiteSpace(c.member.role)) sb.AppendLine($"**Who:** {c.member.role}  ");
                if (!string.IsNullOrWhiteSpace(c.member.voice)) sb.AppendLine($"**Voice:** {c.member.voice}");
            }
            var theirs = letters.Where(l => l.senderName == c.name).ToList();
            if (theirs.Count == 0) { sb.AppendLine(); sb.AppendLine("_No letters yet._"); continue; }
            foreach (var l in theirs)
            {
                sb.AppendLine();
                sb.AppendLine($"#### \"{l.subject}\"  `{l.id}`");
                sb.AppendLine();
                sb.AppendLine($"- **Arrives:** {When(l)}");
                if (l.HasReward) sb.AppendLine($"- **Gift:** {Gift(l)}");
                if (l.ctaKind != CtaKind.None) sb.AppendLine($"- **Button:** {Button(l.ctaKind)}");
                sb.AppendLine();
                foreach (string line in (l.body ?? "").Replace("\r", "").Split('\n'))
                    sb.AppendLine(line.Length == 0 ? ">" : "> " + line);
            }
        }
        sb.AppendLine();

        // ── Tips ──
        sb.AppendLine("## Tutorial steps & how-to tips");
        sb.AppendLine();
        sb.AppendLine("Shown by the spotlight overlay, once each, only on farms named after onboarding shipped " +
                      "(Dev Tools > **Replay Onboarding** opts any save back in).");
        sb.AppendLine();
        sb.AppendLine("| Shows when | Text | id |");
        sb.AppendLine("|---|---|---|");
        foreach (var t in tips.Where(t => !t.id.StartsWith("almanac_")))
            sb.AppendLine($"| {Cell(t.when)} | {Cell(t.text)} | `{t.id}` |");
        sb.AppendLine();

        // ── Almanac ──
        sb.AppendLine("## Farmer's Almanac pages");
        sb.AppendLine();
        sb.AppendLine("The \"why pick this\" blurb at the top of each Almanac page. Stats and tags under it are generated from game data. " +
                      "Equipment and animals without an entry here use their own description.");
        sb.AppendLine();
        sb.AppendLine("| Page | Blurb | id |");
        sb.AppendLine("|---|---|---|");
        foreach (var t in tips.Where(t => t.id.StartsWith("almanac_")))
            sb.AppendLine($"| {Cell(t.when.Replace("Almanac: ", ""))} | {Cell(t.text)} | `{t.id}` |");
        sb.AppendLine();

        string full = Path.GetFullPath(DocPath);
        Directory.CreateDirectory(Path.GetDirectoryName(full));
        File.WriteAllText(full, sb.ToString());
        Debug.Log($"[NarrativeCopy] Wrote {DocPath} ({cast.Count} characters, {letters.Count} letters, {tips.Count} tips).");
    }

    private static string Cell(string s) => (s ?? "").Replace("\r", "").Replace("\n\n", " / ").Replace("\n", " ").Replace("|", "\\|");

    private static string Gift(LetterDef l)
    {
        if (!l.HasReward) return "—";
        var parts = new List<string>();
        if (l.HasGiftSeed) parts.Add($"{l.giftSeed} seed packet");
        if (l.HasCurrencyReward) parts.Add($"{l.rewardAmount} {l.rewardKind}");
        return string.Join(" + ", parts);
    }

    private static string Button(CtaKind k) => k switch
    {
        CtaKind.None => "—",
        CtaKind.OpenEquipment => "Go to Equipment",
        CtaKind.OpenResearch => "Go to Research",
        CtaKind.OpenShop => "Go to Shop",
        CtaKind.OpenFarmUpgrades => "See Farm Upgrades",
        CtaKind.OpenCarpenter => "Visit Harry's Shop",
        CtaKind.OpenBarn => "Open the Barn",
        CtaKind.OpenAnimals => "See Animals",
        CtaKind.OpenFieldPicker => "Choose Seeds",
        CtaKind.OpenMarket => "Go to Market",
        CtaKind.OpenTownRequests => "See Requests",
        CtaKind.OpenPlantsShop => "Visit Hazel's Stall",
        _ => k.ToString(),
    };

    private static string When(LetterDef l)
    {
        var parts = new List<string>();
        if (!string.IsNullOrEmpty(l.triggerFeatureFlag)) parts.Add($"research unlocks `{l.triggerFeatureFlag}`");
        if (!string.IsNullOrEmpty(l.triggerAnimalId)) parts.Add($"the `{l.triggerAnimalId}` animal unlocks");
        if (!string.IsNullOrEmpty(l.triggerEvent))
            foreach (string token in l.triggerEvent.Split('|')) parts.Add(EventText(token.Trim()));
        string when = parts.Count == 0 ? "sent directly by code (e.g. right after naming the farm)" : string.Join(", or ", parts);
        return l.newPlayersOnly ? when + " _(new farms only)_" : when;
    }

    private static string EventText(string e)
    {
        if (e.StartsWith("run_ended:")) return $"run #{e.Substring(10)} ends";
        if (e.StartsWith("run_survived:")) return $"a run lasts {e.Substring(13)}";
        if (e.StartsWith("upgrade:zone_unlock_")) return $"Field {e.Substring(20)} is bought";
        if (e.StartsWith("upgrade:")) return $"upgrade `{e.Substring(8)}` is bought";
        if (e.StartsWith("built:")) return e.Substring(6).Replace("building_", "").Replace("_built", "") + " is built";
        return e switch
        {
            "tree_felled" => "the first tree is chopped down",
            "axe_bought" => "the axe is bought",
            "pole_bought" => "the fishing pole is bought",
            "fish_caught" => "the first fish is caught",
            "animal_unlocked" => "the first animal is unlocked",
            "town_request_done" => "the first Town Request is delivered",
            "seed_bought" => "a seed packet is bought at Hazel's stall",
            "seed_bought:regrow" => "the first regrowing crop is bought",
            "packet_bought" => "an extra seed packet is bought",
            _ => $"`{e}`",
        };
    }
}
