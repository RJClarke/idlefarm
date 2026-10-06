#if UNITY_EDITOR
using System.Linq;
using UnityEngine;

/// <summary>
/// Editor-only balance bench, driven through PlayModeBridge's method caller:
/// MaxEverything, then Start|Crop, poll Status, Report, and finally RestoreSelection.
/// Plants one crop in every field so runs with different crops can be compared.
/// Use on a COPY of the save: it maxes progression and saves.
/// </summary>
public static class DevBench
{
    // The player's own field picks, kept in a file (one "zone=crop" per line) so a script reload
    // mid-bench can't lose them; RestoreSelection puts them back and deletes the file.
    private static string SelectionBackupPath => System.IO.Path.Combine(Application.dataPath, "..", "Temp", "devbench_selection.txt");

    /// <summary>All research, permanent upgrades (helpers, grid, fields, equipment tracks), farm
    /// upgrades and Barn skills at `percent` of their max (rounded up, so every field and one-level
    /// unlock is owned); every seed with a packet per field.</summary>
    public static string MaxEverything(float percent)
    {
        float f = Mathf.Clamp01(percent / 100f);
        int Lv(int max) => Mathf.CeilToInt(max * f);
        int upgrades = 0;
        if (ResearchManager.Instance != null) ResearchManager.Instance.DevSetAll(f);
        if (FarmSkillsManager.Instance != null) FarmSkillsManager.Instance.DevSetAll(f);
        if (UpgradeManager.Instance != null)
        {
            foreach (var u in Resources.FindObjectsOfTypeAll<UpgradeData>())
                if (u != null && !string.IsNullOrEmpty(u.upgradeID) && u.maxLevel > 0)
                { UpgradeManager.Instance.GrantPermanentLevel(u.upgradeID, Lv(u.maxLevel)); upgrades++; }
            foreach (var fu in Resources.LoadAll<FarmUpgradeData>("FarmUpgrades"))
            { UpgradeManager.Instance.GrantPermanentLevel(fu.upgradeID, Lv(fu.maxLevel)); upgrades++; }
            foreach (var c in Resources.FindObjectsOfTypeAll<CropData>())
                UpgradeManager.Instance.GrantPermanentLevel(SeedShopRules.OwnershipKey(c.cropName), SeedShopRules.MaxPackets);
        }
        if (SaveManager.Instance != null) SaveManager.Instance.SaveGame();
        // Grid size and helper count are built at load: exit and re-enter play before benching.
        return $"maxed: everything at {percent:0}% - research + skills + {upgrades} upgrade tracks; fields={CropOwnership.FieldsOwned()} (re-enter play to apply)";
    }

    /// <summary>Plant this crop in every field, start a run, and set the game speed (dev speeds allowed).</summary>
    public static string Start(string cropName, float speed)
    {
        var rm = RunManager.Instance;
        if (rm == null) return "no RunManager";
        if (rm.IsRunActive) rm.EndRun();

        CropData crop = Resources.FindObjectsOfTypeAll<CropData>().FirstOrDefault(c => c.cropName == cropName);
        if (crop == null) return "no crop " + cropName;

        var sel = SeedSelectionData.Load();
        if (!System.IO.File.Exists(SelectionBackupPath))
            System.IO.File.WriteAllLines(SelectionBackupPath, sel.zoneAssignments.Select(kv => kv.Key + "=" + kv.Value));
        sel.Clear();
        for (int zone = 1; zone <= CropOwnership.FieldsOwned(); zone++) sel.AssignCrop(zone, crop);
        sel.Save();

        rm.StartNewRun();
        GameSpeedControl.JumpToEnd(-1);
        while (GameSpeedControl.Multiplier < speed - 0.001f && GameSpeedControl.Step(1)) { }
        rm.RefreshGameSpeed();
        return $"started {cropName} x{CropOwnership.FieldsOwned()} fields at {GameSpeedControl.Label}, run={rm.IsRunActive}";
    }

    /// <summary>One line: is the run still going, and how much farm time has passed.</summary>
    public static string Status()
    {
        var rm = RunManager.Instance;
        return rm == null ? "no RunManager" : $"active={rm.IsRunActive} farm={rm.CurrentRunDuration:0}";
    }

    /// <summary>End the current run now (used when a run outlasts the bench's time cap).</summary>
    public static string Stop()
    {
        var rm = RunManager.Instance;
        if (rm != null && rm.IsRunActive) rm.EndRun();
        return Status();
    }

    /// <summary>The finished (or current) run's numbers as one CSV-ish line.</summary>
    public static string Report()
    {
        var rm = RunManager.Instance; var rs = RunStats.Instance;
        if (rm == null || rs == null) return "missing managers";
        // Once a run ends the live timer resets; the run's length is kept as LastRunSurvivedSeconds.
        float farm = rm.IsRunActive ? rm.CurrentRunDuration : rm.LastRunSurvivedSeconds;
        return $"farm={farm:0} bankrupt={rm.LastRunEndedBankrupt} active={rm.IsRunActive} " +
               $"harvested={rs.CropsHarvested} planted={rs.SeedsPlanted} money={rs.MoneyEarned} coins={rs.CoinsBanked} " +
               $"deer={rs.PlantsEatenByDeer} crows={rs.PlantsEatenByCrows} lightning={rs.PlantsStruckByLightning} " +
               $"dried={rs.PlantsDehydrated} rotted={rs.CropsDecayed} cash={(CurrencyManager.Instance != null ? CurrencyManager.Instance.Money : -1)}";
    }

    /// <summary>Put the player's own field picks back.</summary>
    public static string RestoreSelection()
    {
        if (!System.IO.File.Exists(SelectionBackupPath)) return "nothing to restore";
        var sel = new SeedSelectionData();
        foreach (string line in System.IO.File.ReadAllLines(SelectionBackupPath))
        {
            int eq = line.IndexOf('=');
            if (eq > 0 && int.TryParse(line.Substring(0, eq), out int zone)) sel.zoneAssignments[zone] = line.Substring(eq + 1);
        }
        sel.Save();
        System.IO.File.Delete(SelectionBackupPath);
        return "restored " + string.Join(", ", sel.zoneAssignments.Select(kv => kv.Key + "=" + kv.Value));
    }
}
#endif
