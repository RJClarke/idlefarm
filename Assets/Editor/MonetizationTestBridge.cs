using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// File-triggered play-mode driver for the Free Gift / Store (companion to UIDriveBridge, editor-only).
///   Temp/monet.request  body one command per line:
///     status            -> state dump (always appended after any command)
///     ready             -> FreeGiftManager.DevMakeReady
///     reset             -> FreeGiftManager.DevResetGift
///     resetpass         -> StoreManager.DevResetPass
///     claim             -> FreeGiftManager.RequestClaim (what the HUD button does)
///     nofill on|off     -> FakeAdService.SimulateNoFill
///     pause             -> OnApplicationPause(true) on the chest reveal (simulates backgrounding)
///     buy &lt;productId&gt;     -> StoreManager.Purchase
///     restore           -> StoreManager.Restore
///     store             -> StorePopupUITK.Open
/// Results go to Temp/monet_result.txt.
/// </summary>
[InitializeOnLoad]
public static class MonetizationTestBridge
{
    private const string RequestPath = "Temp/monet.request";
    private const string ResultPath = "Temp/monet_result.txt";
    private static double nextPoll;

    static MonetizationTestBridge() { EditorApplication.update += Poll; }

    private static void Poll()
    {
        if (EditorApplication.timeSinceStartup < nextPoll) return;
        nextPoll = EditorApplication.timeSinceStartup + 0.5;
        if (!File.Exists(RequestPath)) return;
        string body = File.ReadAllText(RequestPath);
        File.Delete(RequestPath);
        var sb = new StringBuilder();
        if (!EditorApplication.isPlaying) { File.WriteAllText(ResultPath, "ERR: not in play mode"); return; }
        foreach (string raw in body.Split('\n'))
        {
            string line = raw.Trim();
            if (line.Length == 0) continue;
            try { sb.AppendLine(Run(line)); }
            catch (System.Exception e) { sb.AppendLine($"ERR {line}: {e.Message}"); }
        }
        sb.Append(Status());
        File.WriteAllText(ResultPath, sb.ToString());
    }

    private static string Run(string line)
    {
        string[] parts = line.Split(new[] { ' ' }, 2);
        string cmd = parts[0].ToLowerInvariant();
        string arg = parts.Length > 1 ? parts[1].Trim() : "";
        switch (cmd)
        {
            case "status": return "ok status";
            case "ready": FreeGiftManager.Instance?.DevMakeReady(); return "ok ready";
            case "reset": FreeGiftManager.Instance?.DevResetGift(); return "ok reset";
            case "resetpass": StoreManager.Instance?.DevResetPass(); return "ok resetpass";
            case "claim": FreeGiftManager.Instance?.RequestClaim(); return "ok claim";
            case "nofill": FakeAdService.SimulateNoFill = arg == "on"; return "ok nofill " + FakeAdService.SimulateNoFill;
            case "pause":
                var chest = Object.FindFirstObjectByType<ChestRevealUITK>();
                if (chest == null) return "ERR no chest";
                chest.SendMessage("OnApplicationPause", true);
                return "ok pause";
            case "buy": StoreManager.Instance?.Purchase(arg); return "ok buy " + arg;
            case "restore": StoreManager.Instance?.Restore(); return "ok restore";
            case "store": StorePopupUITK.Open(); return "ok store";
            case "barn": BarnPopupUITK.Instance?.Open(); return "ok barn";
            case "gems": CurrencyManager.Instance?.AddGems(int.Parse(arg)); return "ok gems +" + arg;
            case "skin": return "skin " + arg + " -> " + (SkinManager.Instance != null ? SkinManager.Instance.TryBuy(arg).ToString() : "no manager");
            case "equip": return "equip " + arg + " -> " + (SkinManager.Instance != null && SkinManager.Instance.Equip(arg));
            case "grant": SkinManager.Instance?.Grant(new[] { arg }); return "ok grant " + arg;
            case "resetskins": SkinManager.Instance?.DevResetSkins(); return "ok resetskins";
            case "tab": StorePopupUITK.Open((StoreTab)System.Enum.Parse(typeof(StoreTab), arg, true)); return "ok tab " + arg;
            case "finish":
            {
                int slot = int.Parse(arg);
                var rm = ResearchManager.Instance;
                int cost = rm != null ? rm.GetFinishGemCost(slot) : -1;
                return $"finish {slot} cost={cost} -> " + (rm != null && rm.TryFinishWithGems(slot));
            }
            case "assign":
            {
                string[] a = arg.Split(' ');
                var rm = ResearchManager.Instance;
                return "assign -> " + (rm != null && rm.TryAssignResearch(int.Parse(a[0]), a[1]));
            }
            case "mult": return "harvest x" + StoreManager.HarvestCoinMultiplier;
            case "slots":
            {
                var rm = ResearchManager.Instance; var b = new StringBuilder();
                for (int i = 0; rm != null && i < ResearchManager.SlotCount; i++)
                {
                    var st = rm.GetSlot(i);
                    b.Append($"slot{i}: {(st == null || st.IsIdle ? "idle" : st.activeResearchID + " L" + st.currentLevel)} left={rm.GetSecondsRemaining(i):F0}s cost={rm.GetFinishGemCost(i)} | ");
                }
                return b.ToString();
            }
            default: return "ERR unknown " + cmd;
        }
    }

    private static string Status()
    {
        var m = FreeGiftManager.Instance;
        var s = StoreManager.Instance;
        var c = CurrencyManager.Instance;
        var sb = new StringBuilder("--- status\n");
        sb.AppendLine($"gems={c?.Gems} coins={c?.Coins}");
        if (m != null)
        {
            var d = new GameData();
            m.CaptureTo(d);
            sb.AppendLine($"gift status={m.Status} unlocked={m.IsUnlocked} secs={m.SecondsUntilReady:F0} needsAd={m.NextClaimNeedsAd} " +
                          $"reward={m.GemsPerClaim}g+{m.CoinReward}c claimsToday={d.giftClaimsToday} date={d.giftClaimsTodayDate} " +
                          $"lifetimeAd={d.giftLifetimeAdClaims} pitchShown={d.passPitchShown}");
        }
        else sb.AppendLine("gift manager MISSING");
        sb.AppendLine(s != null ? $"store pass={s.HasFarmersPass} busy={s.Busy} blessing={s.IsProductOwned(StoreDefaults.HarvestBlessingId)} golden={s.IsProductOwned(SkinDefaults.GoldenSet)} starter={s.IsProductOwned(StoreDefaults.StarterId)}" : "store manager MISSING");
        var sk = SkinManager.Instance;
        if (sk != null)
        {
            var e = new StringBuilder("skins equipped:");
            foreach (string t in SkinDefaults.Targets) { var d = sk.EquippedDef(t); e.Append($" {t}={(d != null ? d.id : "classic")}"); }
            sb.AppendLine(e.ToString());
        }
        sb.AppendLine($"chestShowing={ChestRevealUITK.IsShowing} storeOpen={StorePopupUITK.IsOpen} tutorial={TutorialManager.ActiveId}");
        var btn = FreeGiftButton.Instance;
        sb.AppendLine(btn != null ? $"hud shown={btn.IsShown} active={btn.gameObject.activeInHierarchy}" : "hud button MISSING");
        return sb.ToString();
    }
}
