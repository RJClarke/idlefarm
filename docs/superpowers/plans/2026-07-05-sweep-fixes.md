# Sweep Fixes — Save Integrity, Boot Order, Time Robustness, Perf, Settings

Fixes from the 2026-07-05 architecture/performance sweep, approved by RJ. Audio import
settings and quest timezone anchoring are explicitly OUT of scope (user decision).

## Global Constraints

- Unity 6000.3, C#. Single gameplay scene (`Assets/Scenes/SampleScene.unity`) + splash scene.
- NEVER edit serialized Unity files (.unity, .prefab, .asset, .meta) — code only.
- New Input System only (`Keyboard.current` / `Mouse.current`), never legacy `Input.*`.
- Pure logic with no UnityEngine dependency goes in the `EconomyCore` assembly
  (`Assets/Scripts/EconomyCore/`, has its own asmdef + EditMode tests in `Assets/Tests/EditMode/`).
  MonoBehaviours stay in `Assets/Scripts/` (Assembly-CSharp). Check the EditMode asmdef
  references before assuming a test can see Assembly-CSharp types.
- TDD for pure logic: write the EditMode test first, watch it fail, then implement.
- Logging convention: `Debug.Log` only for important events (purchases, run start/end,
  save/load); keep LogWarning/LogError.
- The repo has MANY pre-existing uncommitted modifications unrelated to this plan.
  Stage ONLY files you created/changed (`git add <paths>`), NEVER `git add -A` / `git add .`.
- Commit per task on branch `feat/run-ender-economy`. End commit messages with:
  `Co-Authored-By: Claude Fable 5 <noreply@anthropic.com>`
- Do not run Unity or its MCP tools; the controller runs the Unity test suite after tasks land.

## Task 1: Save integrity — atomic writes, backup fallback, schema version

Files: `Assets/Scripts/SaveManager.cs`, `Assets/Scripts/GameData.cs`, new
`Assets/Scripts/EconomyCore/SaveFileIO.cs`, new EditMode test file.

1. Add `public int saveVersion = 1;` as the FIRST field of `GameData` (field initializer,
   so JsonUtility writes it). No migration switch yet — the field existing is the point.
2. New static class `SaveFileIO` in EconomyCore (System.IO only, no UnityEngine):
   - `WriteAtomic(string path, string contents)`: write to `path + ".tmp"`, then swap it in
     atomically, keeping the previous file at `path + ".bak"`. Use `File.Replace(tmp, path, bak)`
     when `path` exists; `File.Move(tmp, path)` when it doesn't. Clean up stray .tmp on entry.
   - `ReadWithFallback(string path, Func<string, bool> isValid, out string contents, out bool usedBackup)`:
     try `path`; if missing/empty/`isValid` returns false, try `path + ".bak"`; return false only
     if both fail. `isValid` lets the caller do the JSON sanity check without EconomyCore
     referencing JsonUtility.
3. SaveManager.SaveGame: replace `File.WriteAllText` with `SaveFileIO.WriteAtomic`.
4. SaveManager.LoadGame: use `ReadWithFallback` with an isValid that parses via
   `JsonUtility.FromJson<GameData>` inside try/catch and checks non-null. LogWarning when the
   backup was used. Return false only when both copies fail (new game).
5. Fix LoadGame structure: currently ALL manager application is nested inside
   `if (CurrencyManager.Instance != null)` yet the method returns true when it's null.
   Restructure: if CurrencyManager.Instance is null, LogError and return false. The per-manager
   null guards for the other managers stay as they are.
6. EditMode tests for SaveFileIO (temp dir via `Path.GetTempPath()`): atomic write creates
   file; second write creates .bak with prior contents; corrupt main + valid .bak falls back;
   both corrupt returns false; first-ever write (no existing file) works.

## Task 2: Deterministic load order + run-Money reset bug

Files: `Assets/Scripts/CurrencyManager.cs`, new EditMode test file (reflection-based, only if
the EditMode asmdef can see Assembly-CSharp — otherwise skip tests and say so).

Background: `CurrencyManager.Start()` currently calls `SaveManager.LoadGame()` (which restores
an active run's Money via `SetMoney`) and THEN unconditionally calls `ResetMoneyForNewRun()`,
clobbering the restored Money back to `startingMoney`. The offline gate only repairs Money when
the away-gap is ≥ 5 minutes, so any quick app restart during a run resets run Money — a bug and
a bankruptcy-escape exploit. Separately, load order across ~12 managers is whatever Start()
order Unity picks.

1. Add `[DefaultExecutionOrder(2000)]` to CurrencyManager with a comment explaining the
   contract: CurrencyManager.Start is the game's load trigger and must run AFTER every other
   manager's Awake/Start so save state is applied onto fully-initialized managers.
   (AutoSaveManager is 1500; 2000 keeps load after its subscription, which is fine — a
   debounced save after load is harmless.)
2. Reorder Start(): set new-game defaults FIRST (`ResetMoneyForNewRun()`, and keep the
   existing `SetCoins(startingCoins)` fallback for the no-save case), THEN call
   `SaveManager.LoadGame()` so loaded state (including resumed-run Money) lands last and is
   never clobbered. Preserve existing behavior for the no-save path.
3. Update the stale `// Always reset money for new run` comment to reflect the new order.

## Task 3: Forward-only clock + autosave max interval

Files: new `Assets/Scripts/EconomyCore/OfflineClock.cs` + EditMode tests,
`Assets/Scripts/AutoSaveManager.cs`, and the offline consumers listed below.

1. New pure static class `OfflineClock` in EconomyCore:
   - `double ForwardGapSeconds(long lastUtcTicks, long nowUtcTicks)` → elapsed seconds,
     clamped to ≥ 0 (device clock moved backwards ⇒ 0). Also return 0 when lastUtcTicks ≤ 0.
   - EditMode tests: normal gap, zero, negative (clock rollback), lastUtcTicks 0.
2. Apply forward-only semantics at every offline/catch-up consumer: when "now" is EARLIER
   than the stored timestamp, treat elapsed as 0 AND re-anchor the stored timestamp to now
   (so a rolled-back clock heals instead of freezing progress until it catches up).
   Audit these files for `DateTime.UtcNow`-based deltas and guard each:
   - `Assets/Scripts/OfflineProgressManager.cs` (TryShow/ForceShow gap)
   - `Assets/Scripts/ResearchManager.cs` (research tick + offline catch-up)
   - `Assets/Scripts/AnimalManager.cs` (egg timer, compost tick, offline compost)
   - `Assets/Scripts/QuestManager.cs` (ProcessDrops: lastQuestDropTime in the future ⇒ clamp to now)
   - `Assets/Scripts/RunManager.cs` (ResumeRun offline credit ⇒ negative offlineSecs = 0)
   - `Assets/Scripts/DailyRewardManager.cs` (a last-claim date in the future ⇒ clamp to today)
   - `Assets/Scripts/Woodcutting/` + `Assets/Scripts/Woodcutting/TreeNode.cs` (regrowth timers)
   Keep each guard minimal — one clamp/re-anchor per site, no refactors.
3. AutoSaveManager: the trailing debounce (every event pushes the deadline out) can starve
   saves during a busy run. Add `[SerializeField] float maxIntervalSecs = 30f` and track the
   last actual save time: in Update, if a save is pending and `now - lastSave >= maxIntervalSecs`,
   save immediately instead of waiting for a quiet window. First save of the session counts
   from subscription time.

## Task 4: Quest event hygiene + quest lookup dictionary

Files: `Assets/Scripts/QuestManager.cs`, `Assets/Scripts/ResearchManager.cs` (only if it has
the same lambda-subscription pattern).

1. QuestManager subscribes to ~4 events with lambdas and documents that it can't unsubscribe
   (`UnsubscribeFromEvents` at ~line 298 is an empty stub). Replace each lambda with either a
   named private method or a stored `Action` delegate field, implement UnsubscribeFromEvents
   for real, and call it from OnDestroy (mirror the symmetric pattern in
   `Assets/Scripts/AutoSaveManager.cs`).
2. `IncrementProgress` (~line 310) does `allQuests.Find(q => q.questID == quest.questID)` —
   a fresh closure + linear scan per active quest per gameplay event (fires on every harvest).
   Build a `Dictionary<string, QuestData>` once when `allQuests` is populated and use it here
   and in any other per-event `.Find` on allQuests.
3. Check ResearchManager for the same unmatched-lambda pattern (it has 1 `+=` and 0 `-=`);
   fix identically if it's a real event subscription, leave alone if not.
4. Existing quest EditMode tests must still pass (do not delete/weaken any).

## Task 5: FloatingTextManager pooling + RunUI timer throttle

Files: `Assets/Scripts/FloatingTextManager.cs`, `Assets/Scripts/RunUI.cs`.

1. FloatingTextManager creates a new GameObject + TextMeshProUGUI per reward popup and
   destroys it ~1.2s later — this is the most-fired visual in the game (every harvest).
   Pool it: a Queue of reusable label GameObjects (reasonable cap, e.g. 16; if all are busy
   reuse the oldest active one). On reuse: `LeanTween.cancel(go)`, reset localScale/alpha/
   text/position, SetActive(true). On animation complete: SetActive(false) and return to
   pool instead of Destroy. Keep ALL public static entry points and their signatures
   unchanged (ShowMoney, ShowMoneySpent, ShowCoins with delay, ShowMoneySpentAtScreen,
   ShowGems, ShowCompost, Show). Both label styles (reward + spend) share the pool.
2. RunUI.Update (~line 56) rebuilds the timer string every frame. Cache the last displayed
   whole second; only call GetFormattedRunDuration / assign `.text` when the displayed second
   (or run-active state) changes.

## Task 6: Settings — Frame Rate preference + Disable Currency Animations

Files: `Assets/Scripts/SettingsManager.cs`, `Assets/Scripts/UI/SettingsPopupUITK.cs`,
`Assets/Scripts/CurrencyUI.cs`.

1. SettingsManager (follow the existing property pattern exactly — EnsureLoaded, PlayerPrefs
   key constant, backing field, PlayerPrefs.Save on set):
   - `TargetFps` int, key `setting_target_fps`, default 60, allowed values 30/60/120 (clamp
     anything else to 60). Setter applies immediately via a private
     `ApplyFrameRate()`: `QualitySettings.vSyncCount = 0; Application.targetFrameRate = value;`.
   - Call `ApplyFrameRate()` from `EnsureLoaded()` too, and add a
     `[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]` static hook
     that calls `EnsureLoaded()` so the cap applies at boot without any scene object.
   - `CurrencyAnimations` bool, key `setting_currency_animations`, default true.
2. SettingsPopupUITK: rows are spawned in code via `SpawnToggleRow(parent, title, desc,
   initial, onChanged)` (~line 125+).
   - Add a "Currency Animations" toggle row (desc: "Pulse the currency counters on change")
     near the existing "Floating Numbers" row, wired to `SettingsManager.CurrencyAnimations`.
   - Add a "Frame Rate" row showing the current value ("30 FPS" / "60 FPS" / "120 FPS") that
     cycles 30 → 60 → 120 → 30 on tap, wired to `SettingsManager.TargetFps`. Reuse the visual
     style of existing rows (build a small SpawnCycleRow helper modeled on SpawnToggleRow; if
     the row UXML template is toggle-specific, build the row programmatically to match the
     existing look — same classes/margins). Do NOT edit .uxml/.uss files if avoidable; if a
     template tweak is genuinely required, STOP and report BLOCKED with what's needed.
   - Leave the "Low Power Mode" and "Show FPS" stub rows untouched.
3. CurrencyUI.AnimateText (~line 136): early-return (after ensuring scale is reset to one)
   when `!SettingsManager.CurrencyAnimations`.
