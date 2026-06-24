# Early-Game Onboarding + Inbox Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a one-shot narrative-beat foundation, a first-run farm-naming flow, and a built-to-last Inbox where townspeople send letters (with rewards/CTAs) triggered by game events.

**Architecture:** Three layers. (0) A pure `NarrativeLedger` fire-once flag set + `InboxModel` letter-state machine in the `IdleFarm.EconomyCore` assembly (unit-tested). (1) MonoBehaviour managers (`NarrativeManager`, `InboxManager`, `NarrativeDirector`) that wrap the pure logic, persist via `SaveManager`/`GameData`, and dispatch beats. (2) UITK consumers: `FarmNamePopup`, `InboxPopupUITK`, an envelope top-bar button, and a Settings rename row. Letter *content* is authored data (`LetterCatalogSO`); trigger *wiring* is code (`NarrativeDirector`) — the hybrid model.

**Tech Stack:** Unity 6000.3, C#, UI Toolkit (UITK), NUnit EditMode tests, JsonUtility save (`GameData`).

## Global Constraints

- Unit-testable logic MUST live in `Assets/Scripts/EconomyCore/` (the only assembly the test project references: `IdleFarm.EditModeTests` → `IdleFarm.EconomyCore`). MonoBehaviours/popups live in `Assets/Scripts/` (Assembly-CSharp) and are NOT unit-tested.
- Persistence goes through `GameData` (JsonUtility, no Dictionary — use flat arrays) and is wired in `SaveManager.SaveGame()` / `LoadGame()`, following the `seenContentIds` / `NewContentTracker` precedent (manager exposes `LoadState(...)` + `GetForSave()`).
- Farm name rules: **required, 3–30 characters** (trimmed), prefilled with a random playful suggestion, re-rollable via a dice button.
- One-shot flag id conventions: onboarding = `"onboarding_named"`; per-letter = `"letter:" + letterId`.
- Token convention: letter/onboarding body text may contain `{farmName}`, resolved at display time.
- Reward currencies a letter may grant: Coins, Gems, Compost (via `CurrencyManager.AddCoins/AddGems/AddCompost`).
- Do NOT edit `.unity`, `.prefab`, `.asset`, `.meta` files by hand — use Unity MCP tools for scene/asset/SO work.
- Coach-mark / tutorial overlays are OUT OF SCOPE; reserve `CtaKind`/payload room but build nothing for them.

---

## File Structure

**Created (EconomyCore — pure, tested):**
- `Assets/Scripts/EconomyCore/NarrativeLedger.cs` — fire-once flag set.
- `Assets/Scripts/EconomyCore/InboxModel.cs` — `InboxEntry` + letter-state machine.
- `Assets/Scripts/EconomyCore/LetterContent.cs` — `RewardKind`, `CtaKind`, `LetterDef`.
- `Assets/Scripts/EconomyCore/LetterCatalogSO.cs` — authored letter catalog + lookup.
- `Assets/Scripts/EconomyCore/FarmName.cs` — validation + playful-name suggestions.
- `Assets/Scripts/EconomyCore/NarrativeText.cs` — `{farmName}` token resolver.

**Created (Assembly-CSharp — managers/UI):**
- `Assets/Scripts/Narrative/NarrativeManager.cs`
- `Assets/Scripts/Narrative/InboxManager.cs`
- `Assets/Scripts/Narrative/NarrativeDirector.cs`
- `Assets/Scripts/UI/FarmNamePopupUITK.cs`
- `Assets/Scripts/UI/InboxPopupUITK.cs`
- `Assets/Scripts/UI/InboxButton.cs`
- UXML/USS under `Assets/UI/FarmNamePopupUITK/` and `Assets/UI/InboxPopupUITK/`

**Created (tests):**
- `Assets/Tests/EditMode/NarrativeLedgerTests.cs`
- `Assets/Tests/EditMode/InboxModelTests.cs`
- `Assets/Tests/EditMode/LetterCatalogTests.cs`
- `Assets/Tests/EditMode/FarmNameTests.cs`
- `Assets/Tests/EditMode/NarrativeTextTests.cs`

**Modified:**
- `Assets/Scripts/GameData.cs` — add `farmName`, `firedNarrativeFlags`, `inboxLetters`.
- `Assets/Scripts/SaveManager.cs` — save/load the new fields.
- `Assets/Scripts/UI/SettingsPopupUITK.cs` — replace the "Display Name" stub with a real farm-name rename row.

---

## Task 1: NarrativeLedger (fire-once flag set)

**Files:**
- Create: `Assets/Scripts/EconomyCore/NarrativeLedger.cs`
- Test: `Assets/Tests/EditMode/NarrativeLedgerTests.cs`

**Interfaces:**
- Produces: `class NarrativeLedger` with `bool HasFired(string id)`, `bool MarkFired(string id)` (returns true iff newly added), `void Load(IEnumerable<string> ids)`, `string[] ToArray()`.

- [ ] **Step 1: Write the failing tests**

```csharp
// Assets/Tests/EditMode/NarrativeLedgerTests.cs
using NUnit.Framework;

public class NarrativeLedgerTests
{
    [Test]
    public void MarkFired_FirstTime_ReturnsTrue_ThenFalse()
    {
        var ledger = new NarrativeLedger();
        Assert.IsTrue(ledger.MarkFired("onboarding_named"));
        Assert.IsFalse(ledger.MarkFired("onboarding_named"));
    }

    [Test]
    public void HasFired_TracksMarkedFlags()
    {
        var ledger = new NarrativeLedger();
        Assert.IsFalse(ledger.HasFired("letter:welcome"));
        ledger.MarkFired("letter:welcome");
        Assert.IsTrue(ledger.HasFired("letter:welcome"));
    }

    [Test]
    public void NullOrEmpty_IsIgnored_NeverFires()
    {
        var ledger = new NarrativeLedger();
        Assert.IsFalse(ledger.MarkFired(null));
        Assert.IsFalse(ledger.MarkFired(""));
        Assert.IsFalse(ledger.HasFired(null));
    }

    [Test]
    public void LoadAndToArray_RoundTrips_Deduped()
    {
        var ledger = new NarrativeLedger();
        ledger.Load(new[] { "a", "b", "a", null, "" });
        var arr = ledger.ToArray();
        Assert.AreEqual(2, arr.Length);
        Assert.Contains("a", arr);
        Assert.Contains("b", arr);
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: open Unity Test Runner (EditMode) or `Window > General > Test Runner`; run class `NarrativeLedgerTests`.
Expected: FAIL — `NarrativeLedger` does not exist (compile error).

- [ ] **Step 3: Implement `NarrativeLedger`**

```csharp
// Assets/Scripts/EconomyCore/NarrativeLedger.cs
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Pure fire-once flag set. Records "this narrative beat has happened" so it never
/// fires again. Persisted as a flat string[] in GameData; wrapped by NarrativeManager.
/// </summary>
public class NarrativeLedger
{
    private readonly HashSet<string> fired = new HashSet<string>();

    public bool HasFired(string id) => !string.IsNullOrEmpty(id) && fired.Contains(id);

    /// <summary>Returns true iff this id was not already present (i.e. fires now).</summary>
    public bool MarkFired(string id)
    {
        if (string.IsNullOrEmpty(id)) return false;
        return fired.Add(id);
    }

    public void Load(IEnumerable<string> ids)
    {
        fired.Clear();
        if (ids == null) return;
        foreach (var id in ids)
            if (!string.IsNullOrEmpty(id)) fired.Add(id);
    }

    public string[] ToArray() => fired.ToArray();
}
```

- [ ] **Step 4: Run the tests to verify they pass**

Run: Test Runner → `NarrativeLedgerTests`. Expected: 4 PASS.

- [ ] **Step 5: Commit**

```bash
git add Assets/Scripts/EconomyCore/NarrativeLedger.cs Assets/Tests/EditMode/NarrativeLedgerTests.cs
git commit -m "feat(narrative): NarrativeLedger fire-once flag set + tests"
```

---

## Task 2: InboxModel (letter-state machine)

**Files:**
- Create: `Assets/Scripts/EconomyCore/InboxModel.cs`
- Test: `Assets/Tests/EditMode/InboxModelTests.cs`

**Interfaces:**
- Produces:
  - `[Serializable] class InboxEntry { string letterId; long receivedUtcTicks; bool read; bool claimed; }`
  - `class InboxModel` with: `void Deliver(string letterId, long nowTicks)`, `int UnreadCount()`, `bool MarkRead(string letterId)` (true iff it flipped an unread entry), `bool Claim(string letterId)` (true iff newly claimed — caller grants reward then), `IReadOnlyList<InboxEntry> Entries` (newest first), `void Load(InboxEntry[] entries)`, `InboxEntry[] ToArray()`.
- Note: entries are keyed by `letterId`; letters are fire-once upstream so duplicates are not expected, and `MarkRead`/`Claim` act on the first matching entry.

- [ ] **Step 1: Write the failing tests**

```csharp
// Assets/Tests/EditMode/InboxModelTests.cs
using NUnit.Framework;

public class InboxModelTests
{
    [Test]
    public void Deliver_AppendsEntry_NewestFirst()
    {
        var m = new InboxModel();
        m.Deliver("welcome", 100);
        m.Deliver("scarecrow", 200);
        Assert.AreEqual(2, m.Entries.Count);
        Assert.AreEqual("scarecrow", m.Entries[0].letterId); // newest first
        Assert.AreEqual("welcome", m.Entries[1].letterId);
    }

    [Test]
    public void UnreadCount_CountsOnlyUnread()
    {
        var m = new InboxModel();
        m.Deliver("a", 1);
        m.Deliver("b", 2);
        Assert.AreEqual(2, m.UnreadCount());
        m.MarkRead("a");
        Assert.AreEqual(1, m.UnreadCount());
    }

    [Test]
    public void MarkRead_OnlyFlipsOnce()
    {
        var m = new InboxModel();
        m.Deliver("a", 1);
        Assert.IsTrue(m.MarkRead("a"));
        Assert.IsFalse(m.MarkRead("a"));   // already read
        Assert.IsFalse(m.MarkRead("missing"));
    }

    [Test]
    public void Claim_ReturnsTrueOnlyOnFirstClaim()
    {
        var m = new InboxModel();
        m.Deliver("a", 1);
        Assert.IsTrue(m.Claim("a"));
        Assert.IsFalse(m.Claim("a"));      // already claimed
        Assert.IsFalse(m.Claim("missing"));
    }

    [Test]
    public void LoadAndToArray_RoundTrips()
    {
        var m = new InboxModel();
        m.Deliver("a", 10);
        m.MarkRead("a");
        var saved = m.ToArray();

        var m2 = new InboxModel();
        m2.Load(saved);
        Assert.AreEqual(1, m2.Entries.Count);
        Assert.AreEqual(0, m2.UnreadCount());
        Assert.AreEqual("a", m2.Entries[0].letterId);
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: Test Runner → `InboxModelTests`. Expected: FAIL — `InboxModel`/`InboxEntry` not defined.

- [ ] **Step 3: Implement `InboxModel` + `InboxEntry`**

```csharp
// Assets/Scripts/EconomyCore/InboxModel.cs
using System;
using System.Collections.Generic;

/// <summary>Persisted per-letter state. Content (text/sender/reward) is looked up by
/// letterId from the authored LetterCatalogSO — only state lives here.</summary>
[Serializable]
public class InboxEntry
{
    public string letterId;
    public long receivedUtcTicks;
    public bool read;
    public bool claimed;
}

/// <summary>Pure inbox state machine: deliver letters, track read/claimed, expose
/// newest-first ordering. Wrapped by InboxManager (which persists + grants rewards).</summary>
public class InboxModel
{
    private readonly List<InboxEntry> entries = new List<InboxEntry>();

    /// <summary>Newest delivered letter first.</summary>
    public IReadOnlyList<InboxEntry> Entries
    {
        get
        {
            var copy = new List<InboxEntry>(entries);
            copy.Reverse();
            return copy;
        }
    }

    public void Deliver(string letterId, long nowTicks)
    {
        if (string.IsNullOrEmpty(letterId)) return;
        entries.Add(new InboxEntry
        {
            letterId = letterId,
            receivedUtcTicks = nowTicks,
            read = false,
            claimed = false
        });
    }

    public int UnreadCount()
    {
        int n = 0;
        foreach (var e in entries) if (!e.read) n++;
        return n;
    }

    public bool MarkRead(string letterId)
    {
        var e = Find(letterId);
        if (e == null || e.read) return false;
        e.read = true;
        return true;
    }

    public bool Claim(string letterId)
    {
        var e = Find(letterId);
        if (e == null || e.claimed) return false;
        e.claimed = true;
        return true;
    }

    public void Load(InboxEntry[] saved)
    {
        entries.Clear();
        if (saved == null) return;
        foreach (var e in saved)
            if (e != null && !string.IsNullOrEmpty(e.letterId)) entries.Add(e);
    }

    public InboxEntry[] ToArray() => entries.ToArray();

    private InboxEntry Find(string letterId)
    {
        if (string.IsNullOrEmpty(letterId)) return null;
        foreach (var e in entries) if (e.letterId == letterId) return e;
        return null;
    }
}
```

- [ ] **Step 4: Run the tests to verify they pass**

Run: Test Runner → `InboxModelTests`. Expected: 5 PASS.

- [ ] **Step 5: Commit**

```bash
git add Assets/Scripts/EconomyCore/InboxModel.cs Assets/Tests/EditMode/InboxModelTests.cs
git commit -m "feat(inbox): InboxModel letter-state machine + tests"
```

---

## Task 3: Letter content model + catalog

**Files:**
- Create: `Assets/Scripts/EconomyCore/LetterContent.cs`
- Create: `Assets/Scripts/EconomyCore/LetterCatalogSO.cs`
- Test: `Assets/Tests/EditMode/LetterCatalogTests.cs`

**Interfaces:**
- Produces:
  - `enum RewardKind { None, Coins, Gems, Compost }`
  - `enum CtaKind { None, OpenEquipment, OpenResearch, OpenShop }`
  - `[Serializable] class LetterDef { string id; string triggerFeatureFlag; string triggerAnimalId; string senderName; Sprite senderPortrait; string subject; [TextArea] string body; RewardKind rewardKind; int rewardAmount; CtaKind ctaKind; string ctaArg; }`
  - `class LetterCatalogSO : ScriptableObject { LetterDef[] letters; LetterDef Get(string id); IEnumerable<LetterDef> ByFeatureFlag(string flag); IEnumerable<LetterDef> ByAnimalId(string id); }`

- [ ] **Step 1: Write the failing tests**

```csharp
// Assets/Tests/EditMode/LetterCatalogTests.cs
using NUnit.Framework;
using UnityEngine;
using System.Linq;

public class LetterCatalogTests
{
    private LetterCatalogSO MakeCatalog()
    {
        var cat = ScriptableObject.CreateInstance<LetterCatalogSO>();
        cat.letters = new[]
        {
            new LetterDef { id = "welcome", subject = "Welcome" },
            new LetterDef { id = "scarecrow", triggerFeatureFlag = "scarecrow", subject = "Build it" },
            new LetterDef { id = "cow", triggerAnimalId = "cow", subject = "Moo" },
        };
        return cat;
    }

    [Test]
    public void Get_ReturnsMatchingDef_OrNull()
    {
        var cat = MakeCatalog();
        Assert.AreEqual("Welcome", cat.Get("welcome").subject);
        Assert.IsNull(cat.Get("nope"));
        Assert.IsNull(cat.Get(null));
        Object.DestroyImmediate(cat);
    }

    [Test]
    public void ByFeatureFlag_FiltersByTrigger()
    {
        var cat = MakeCatalog();
        var hits = cat.ByFeatureFlag("scarecrow").ToList();
        Assert.AreEqual(1, hits.Count);
        Assert.AreEqual("scarecrow", hits[0].id);
        Assert.IsEmpty(cat.ByFeatureFlag("welcome")); // welcome has no trigger flag
        Object.DestroyImmediate(cat);
    }

    [Test]
    public void ByAnimalId_FiltersByTrigger()
    {
        var cat = MakeCatalog();
        var hits = cat.ByAnimalId("cow").ToList();
        Assert.AreEqual(1, hits.Count);
        Assert.AreEqual("cow", hits[0].id);
        Object.DestroyImmediate(cat);
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: Test Runner → `LetterCatalogTests`. Expected: FAIL — types not defined.

- [ ] **Step 3: Implement content model + catalog**

```csharp
// Assets/Scripts/EconomyCore/LetterContent.cs
using System;
using UnityEngine;

public enum RewardKind { None, Coins, Gems, Compost }

/// <summary>What the letter's call-to-action button navigates to. Reserve room for
/// future targets (coach-marks are out of scope for now).</summary>
public enum CtaKind { None, OpenEquipment, OpenResearch, OpenShop }

/// <summary>Authored content for one letter. State (read/claimed) lives in InboxEntry,
/// not here. Trigger fields are optional: a letter with neither trigger is delivered
/// imperatively (e.g. the welcome letter on first-run naming).</summary>
[Serializable]
public class LetterDef
{
    public string id;

    [Header("Trigger (optional)")]
    public string triggerFeatureFlag; // matches ResearchManager.OnFeatureFlagUnlocked
    public string triggerAnimalId;    // matches AnimalManager.OnAnimalUnlocked

    [Header("Content")]
    public string senderName;
    public Sprite senderPortrait;
    public string subject;
    [TextArea(3, 8)] public string body; // may contain {farmName}

    [Header("Reward (optional)")]
    public RewardKind rewardKind = RewardKind.None;
    public int rewardAmount;

    [Header("Call to action (optional)")]
    public CtaKind ctaKind = CtaKind.None;
    public string ctaArg;
}
```

```csharp
// Assets/Scripts/EconomyCore/LetterCatalogSO.cs
using System.Collections.Generic;
using UnityEngine;

/// <summary>Single authored catalog of every letter. InboxManager resolves letterId →
/// LetterDef through this; NarrativeDirector finds trigger matches through this.</summary>
[CreateAssetMenu(fileName = "LetterCatalog", menuName = "IdleFarm/Letter Catalog")]
public class LetterCatalogSO : ScriptableObject
{
    public LetterDef[] letters;

    public LetterDef Get(string id)
    {
        if (string.IsNullOrEmpty(id) || letters == null) return null;
        foreach (var l in letters) if (l != null && l.id == id) return l;
        return null;
    }

    public IEnumerable<LetterDef> ByFeatureFlag(string flag)
    {
        if (string.IsNullOrEmpty(flag) || letters == null) yield break;
        foreach (var l in letters)
            if (l != null && l.triggerFeatureFlag == flag) yield return l;
    }

    public IEnumerable<LetterDef> ByAnimalId(string animalId)
    {
        if (string.IsNullOrEmpty(animalId) || letters == null) yield break;
        foreach (var l in letters)
            if (l != null && l.triggerAnimalId == animalId) yield return l;
    }
}
```

- [ ] **Step 4: Run the tests to verify they pass**

Run: Test Runner → `LetterCatalogTests`. Expected: 3 PASS.

- [ ] **Step 5: Commit**

```bash
git add Assets/Scripts/EconomyCore/LetterContent.cs Assets/Scripts/EconomyCore/LetterCatalogSO.cs Assets/Tests/EditMode/LetterCatalogTests.cs
git commit -m "feat(inbox): LetterDef content model + LetterCatalogSO + tests"
```

---

## Task 4: Farm-name validation + suggestions

**Files:**
- Create: `Assets/Scripts/EconomyCore/FarmName.cs`
- Test: `Assets/Tests/EditMode/FarmNameTests.cs`

**Interfaces:**
- Produces:
  - `static class FarmName` with `const int Min = 3`, `const int Max = 30`, `bool IsValid(string name)` (trim → length in [3,30]), `string Sanitize(string name)` (trim, clamp to 30).
  - `static class FarmNameSuggestions` with `string[] All`, `int Count`, `string At(int index)` (wraps via modulo), `string Random(System.Random rng)`.

- [ ] **Step 1: Write the failing tests**

```csharp
// Assets/Tests/EditMode/FarmNameTests.cs
using NUnit.Framework;
using System;

public class FarmNameTests
{
    [Test]
    public void IsValid_RejectsTooShort()
    {
        Assert.IsFalse(FarmName.IsValid("ab"));   // 2 chars
        Assert.IsFalse(FarmName.IsValid("  a "));  // trims to 1
        Assert.IsFalse(FarmName.IsValid(null));
    }

    [Test]
    public void IsValid_AcceptsBoundaries()
    {
        Assert.IsTrue(FarmName.IsValid("abc"));            // 3
        Assert.IsTrue(FarmName.IsValid(new string('x', 30))); // 30
    }

    [Test]
    public void IsValid_RejectsTooLong()
    {
        Assert.IsFalse(FarmName.IsValid(new string('x', 31)));
    }

    [Test]
    public void Sanitize_TrimsAndClamps()
    {
        Assert.AreEqual("Sunny Acres", FarmName.Sanitize("  Sunny Acres  "));
        Assert.AreEqual(30, FarmName.Sanitize(new string('y', 50)).Length);
    }

    [Test]
    public void Suggestions_AreAllValidNames()
    {
        Assert.Greater(FarmNameSuggestions.Count, 0);
        for (int i = 0; i < FarmNameSuggestions.Count; i++)
            Assert.IsTrue(FarmName.IsValid(FarmNameSuggestions.At(i)),
                $"Suggestion '{FarmNameSuggestions.At(i)}' must satisfy 3..30 chars");
    }

    [Test]
    public void Suggestions_At_WrapsModulo()
    {
        Assert.AreEqual(FarmNameSuggestions.At(0), FarmNameSuggestions.At(FarmNameSuggestions.Count));
    }

    [Test]
    public void Suggestions_Random_IsDeterministicForSeed()
    {
        var a = FarmNameSuggestions.Random(new Random(42));
        var b = FarmNameSuggestions.Random(new Random(42));
        Assert.AreEqual(a, b);
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: Test Runner → `FarmNameTests`. Expected: FAIL — types not defined.

- [ ] **Step 3: Implement validation + suggestions**

```csharp
// Assets/Scripts/EconomyCore/FarmName.cs
using System;

/// <summary>Farm-name rules: required, 3–30 characters (trimmed).</summary>
public static class FarmName
{
    public const int Min = 3;
    public const int Max = 30;

    public static bool IsValid(string name)
    {
        if (name == null) return false;
        int len = name.Trim().Length;
        return len >= Min && len <= Max;
    }

    public static string Sanitize(string name)
    {
        if (name == null) return "";
        string t = name.Trim();
        return t.Length > Max ? t.Substring(0, Max) : t;
    }
}

/// <summary>Curated playful farm names used to prefill the first-run field and the
/// dice re-roll. Every entry MUST satisfy FarmName.IsValid.</summary>
public static class FarmNameSuggestions
{
    public static readonly string[] All =
    {
        "Sunny Acres", "Maple Hollow", "Clover Creek", "Golden Meadow", "Whisker Farm",
        "Dewdrop Dell", "Pebble Patch", "Honey Hill", "Willow Brook", "Bramble Barn",
        "Cricket Field", "Marigold Ranch", "Pumpkin Patch", "Cozy Corner", "Fern Gully",
        "Berry Bend", "Thistle Down", "Robin Roost", "Dandelion Den", "Buttercup Bay",
    };

    public static int Count => All.Length;

    public static string At(int index)
    {
        int i = ((index % All.Length) + All.Length) % All.Length;
        return All[i];
    }

    public static string Random(Random rng)
    {
        if (rng == null) rng = new Random();
        return All[rng.Next(All.Length)];
    }
}
```

- [ ] **Step 4: Run the tests to verify they pass**

Run: Test Runner → `FarmNameTests`. Expected: 7 PASS.

- [ ] **Step 5: Commit**

```bash
git add Assets/Scripts/EconomyCore/FarmName.cs Assets/Tests/EditMode/FarmNameTests.cs
git commit -m "feat(onboarding): farm-name validation + playful suggestions + tests"
```

---

## Task 5: NarrativeText token resolution

**Files:**
- Create: `Assets/Scripts/EconomyCore/NarrativeText.cs`
- Test: `Assets/Tests/EditMode/NarrativeTextTests.cs`

**Interfaces:**
- Produces: `static class NarrativeText` with `string Resolve(string body, string farmName)`.

- [ ] **Step 1: Write the failing tests**

```csharp
// Assets/Tests/EditMode/NarrativeTextTests.cs
using NUnit.Framework;

public class NarrativeTextTests
{
    [Test]
    public void Resolve_ReplacesFarmNameToken()
    {
        Assert.AreEqual("Dear Sunny Acres,",
            NarrativeText.Resolve("Dear {farmName},", "Sunny Acres"));
    }

    [Test]
    public void Resolve_ReplacesAllOccurrences()
    {
        Assert.AreEqual("A A",
            NarrativeText.Resolve("{farmName} {farmName}", "A"));
    }

    [Test]
    public void Resolve_LeavesUnknownTokensUntouched()
    {
        Assert.AreEqual("Hi {playerName}",
            NarrativeText.Resolve("Hi {playerName}", "Acres"));
    }

    [Test]
    public void Resolve_HandlesNulls()
    {
        Assert.AreEqual("", NarrativeText.Resolve(null, "x"));
        Assert.AreEqual("Dear ,", NarrativeText.Resolve("Dear {farmName},", null));
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: Test Runner → `NarrativeTextTests`. Expected: FAIL — type not defined.

- [ ] **Step 3: Implement `NarrativeText`**

```csharp
// Assets/Scripts/EconomyCore/NarrativeText.cs
/// <summary>Resolves narrative text tokens. Currently just {farmName}; structured so
/// adding tokens is a one-line change.</summary>
public static class NarrativeText
{
    public static string Resolve(string body, string farmName)
    {
        if (string.IsNullOrEmpty(body)) return "";
        return body.Replace("{farmName}", farmName ?? "");
    }
}
```

- [ ] **Step 4: Run the tests to verify they pass**

Run: Test Runner → `NarrativeTextTests`. Expected: 4 PASS.

- [ ] **Step 5: Commit**

```bash
git add Assets/Scripts/EconomyCore/NarrativeText.cs Assets/Tests/EditMode/NarrativeTextTests.cs
git commit -m "feat(narrative): {farmName} token resolver + tests"
```

---

## Task 6: NarrativeManager + GameData/SaveManager wiring (farm name + flags)

**Files:**
- Create: `Assets/Scripts/Narrative/NarrativeManager.cs`
- Modify: `Assets/Scripts/GameData.cs` (add `farmName`, `firedNarrativeFlags`)
- Modify: `Assets/Scripts/SaveManager.cs` (save/load both)

**Interfaces:**
- Consumes: `NarrativeLedger` (Task 1).
- Produces: `NarrativeManager` singleton with `static NarrativeManager Instance`, `bool HasFired(string id)`, `bool MarkFired(string id)` (persists on new fire), `string FarmName { get; }`, `void SetFarmName(string name)` (sanitizes, saves, fires `OnFarmNameChanged`), `event Action OnFarmNameChanged`, `void LoadState(string farmName, string[] firedFlags)`, `string GetFarmNameForSave()`, `string[] GetFiredFlagsForSave()`.

- [ ] **Step 1: Add GameData fields**

In `Assets/Scripts/GameData.cs`, add fields after `seenContentIds` (line 46):

```csharp
    // Narrative one-shot ledger (NarrativeManager) + player's farm/account name.
    public string farmName;
    public string[] firedNarrativeFlags;
```

In the default constructor (after `seenContentIds = new string[0];`, line 86), add:

```csharp
        farmName = "";
        firedNarrativeFlags = new string[0];
```

- [ ] **Step 2: Implement `NarrativeManager`**

```csharp
// Assets/Scripts/Narrative/NarrativeManager.cs
using System;
using UnityEngine;

/// <summary>One-shot narrative ledger + the player's farm/account name. Everything that
/// must happen "only the first time" checks HasFired/MarkFired here. Persisted via
/// SaveManager/GameData following the NewContentTracker LoadState/GetForSave pattern.</summary>
[DefaultExecutionOrder(1100)]
public class NarrativeManager : MonoBehaviour
{
    public static NarrativeManager Instance { get; private set; }

    private readonly NarrativeLedger ledger = new NarrativeLedger();
    private string farmName = "";

    public event Action OnFarmNameChanged;

    public string FarmName => farmName;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void OnDestroy() { if (Instance == this) Instance = null; }

    public bool HasFired(string id) => ledger.HasFired(id);

    public bool MarkFired(string id)
    {
        bool isNew = ledger.MarkFired(id);
        if (isNew) SaveManager.Instance?.SaveGame();
        return isNew;
    }

    public void SetFarmName(string name)
    {
        string clean = FarmName.Sanitize(name); // static class FarmName from Task 4
        if (clean == farmName) return;
        farmName = clean;
        SaveManager.Instance?.SaveGame();
        OnFarmNameChanged?.Invoke();
    }

    // ── Persistence (called by SaveManager) ──
    public void LoadState(string savedFarmName, string[] firedFlags)
    {
        farmName = savedFarmName ?? "";
        ledger.Load(firedFlags);
        OnFarmNameChanged?.Invoke();
    }

    public string GetFarmNameForSave() => farmName;
    public string[] GetFiredFlagsForSave() => ledger.ToArray();
}
```

- [ ] **Step 3: Wire SaveManager.SaveGame()**

In `Assets/Scripts/SaveManager.cs`, after the `data.seenContentIds = ...` block (around line 113), add:

```csharp
        data.farmName = NarrativeManager.Instance != null
            ? NarrativeManager.Instance.GetFarmNameForSave()
            : "";
        data.firedNarrativeFlags = NarrativeManager.Instance != null
            ? NarrativeManager.Instance.GetFiredFlagsForSave()
            : new string[0];
```

- [ ] **Step 4: Wire SaveManager.LoadGame()**

In `LoadGame()`, after the `NewContentTracker.Instance.LoadState(...)` block (around line 228), add:

```csharp
                if (NarrativeManager.Instance != null)
                    NarrativeManager.Instance.LoadState(data.farmName, data.firedNarrativeFlags);
```

- [ ] **Step 5: Compile + scene object**

Use Unity MCP: after scripts compile clean (`read_console` shows no errors), create an empty GameObject named `NarrativeManager` in `SampleScene` and add the `NarrativeManager` component (mirror how `NewContentTracker` lives in the scene). Save the scene.

Run: `read_console` → Expected: no compile errors.

- [ ] **Step 6: Manual smoke**

In Play mode: call (via a temporary dev button or the console) `NarrativeManager.Instance.SetFarmName("Test Farm")`, stop play, reopen — `NarrativeManager.Instance.FarmName` should return `"Test Farm"` after load. Confirm `gamedata.json` contains `"farmName": "Test Farm"`.

- [ ] **Step 7: Commit**

```bash
git add Assets/Scripts/Narrative/NarrativeManager.cs Assets/Scripts/GameData.cs Assets/Scripts/SaveManager.cs Assets/Scenes/SampleScene.unity
git commit -m "feat(narrative): NarrativeManager + farmName/flag persistence"
```

---

## Task 7: InboxManager + inbox persistence + reward granting

**Files:**
- Create: `Assets/Scripts/Narrative/InboxManager.cs`
- Modify: `Assets/Scripts/GameData.cs` (add `inboxLetters`)
- Modify: `Assets/Scripts/SaveManager.cs` (save/load inbox)

**Interfaces:**
- Consumes: `InboxModel`/`InboxEntry` (Task 2), `LetterCatalogSO`/`LetterDef`/`RewardKind` (Task 3), `CurrencyManager` (existing).
- Produces: `InboxManager` singleton with `static InboxManager Instance`, `event Action OnInboxChanged`, `LetterCatalogSO Catalog { get; }`, `void Deliver(string letterId)` (appends if the letter exists in catalog, saves, raises event), `int UnreadCount()`, `IReadOnlyList<InboxEntry> Entries`, `LetterDef GetDef(string letterId)`, `void MarkRead(string letterId)` (saves + event if changed), `bool ClaimReward(string letterId)` (grants the letter's reward once via CurrencyManager; returns true iff granted), `void LoadState(InboxEntry[] entries)`, `InboxEntry[] GetForSave()`.

- [ ] **Step 1: Add GameData field**

In `Assets/Scripts/GameData.cs`, below the `firedNarrativeFlags` field (Task 6), add:

```csharp
    public InboxEntry[] inboxLetters;
```

In the default constructor, below `firedNarrativeFlags = new string[0];`, add:

```csharp
        inboxLetters = new InboxEntry[0];
```

- [ ] **Step 2: Implement `InboxManager`**

```csharp
// Assets/Scripts/Narrative/InboxManager.cs
using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Owns received letters. Public Deliver(id) lets any system drop a letter in.
/// Content is resolved from the authored LetterCatalogSO; state is the pure InboxModel,
/// persisted via SaveManager/GameData.</summary>
[DefaultExecutionOrder(1100)]
public class InboxManager : MonoBehaviour
{
    public static InboxManager Instance { get; private set; }

    [SerializeField] private LetterCatalogSO catalog;

    private readonly InboxModel model = new InboxModel();

    public event Action OnInboxChanged;

    public LetterCatalogSO Catalog => catalog;
    public IReadOnlyList<InboxEntry> Entries => model.Entries;
    public int UnreadCount() => model.UnreadCount();
    public LetterDef GetDef(string letterId) => catalog != null ? catalog.Get(letterId) : null;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void OnDestroy() { if (Instance == this) Instance = null; }

    /// <summary>Append a letter (must exist in the catalog) and notify listeners.</summary>
    public void Deliver(string letterId)
    {
        if (catalog == null || catalog.Get(letterId) == null)
        {
            Debug.LogWarning($"[InboxManager] Deliver: unknown letterId '{letterId}'.");
            return;
        }
        model.Deliver(letterId, DateTime.UtcNow.Ticks);
        SaveManager.Instance?.SaveGame();
        OnInboxChanged?.Invoke();
        Debug.Log($"[InboxManager] Delivered letter '{letterId}'. Unread: {model.UnreadCount()}");
    }

    public void MarkRead(string letterId)
    {
        if (model.MarkRead(letterId))
        {
            SaveManager.Instance?.SaveGame();
            OnInboxChanged?.Invoke();
        }
    }

    /// <summary>Grants the letter's reward exactly once. Returns true iff a reward was granted.</summary>
    public bool ClaimReward(string letterId)
    {
        var def = GetDef(letterId);
        if (def == null || def.rewardKind == RewardKind.None || def.rewardAmount <= 0) return false;
        if (!model.Claim(letterId)) return false; // already claimed

        switch (def.rewardKind)
        {
            case RewardKind.Coins:   CurrencyManager.Instance?.AddCoins(def.rewardAmount); break;
            case RewardKind.Gems:    CurrencyManager.Instance?.AddGems(def.rewardAmount); break;
            case RewardKind.Compost: CurrencyManager.Instance?.AddCompost(def.rewardAmount); break;
        }
        SaveManager.Instance?.SaveGame();
        OnInboxChanged?.Invoke();
        return true;
    }

    // ── Persistence (called by SaveManager) ──
    public void LoadState(InboxEntry[] entries)
    {
        model.Load(entries);
        OnInboxChanged?.Invoke();
    }

    public InboxEntry[] GetForSave() => model.ToArray();
}
```

- [ ] **Step 3: Wire SaveManager**

In `SaveManager.SaveGame()`, after the Task 6 narrative block, add:

```csharp
        data.inboxLetters = InboxManager.Instance != null
            ? InboxManager.Instance.GetForSave()
            : new InboxEntry[0];
```

In `SaveManager.LoadGame()`, after the Task 6 `NarrativeManager.Instance.LoadState(...)`, add:

```csharp
                if (InboxManager.Instance != null)
                    InboxManager.Instance.LoadState(data.inboxLetters);
```

- [ ] **Step 4: Author the LetterCatalog asset + scene object**

Use Unity MCP:
1. Create the catalog asset via `manage_scriptable_object` (or `Assets > Create > IdleFarm > Letter Catalog`) at `Assets/Resources/LetterCatalog.asset`.
2. Populate two starter letters:
   - `id="welcome"`, senderName="Mayor Bramble", subject="Welcome to {farmName}!", body="Dear {farmName},\n\nWord travels fast — a new farm in the valley! Check your mailbox often; folks 'round here love to write. Here's a little something to get you started.", rewardKind=Coins, rewardAmount=100, ctaKind=None.
   - `id="scarecrow_unlock"`, triggerFeatureFlag="scarecrow" (use the real scarecrow feature-flag id from EquipmentData/ResearchManager — verify in the project), senderName="Pippa the Tinker", subject="You can build a Scarecrow!", body="Heard you scared off your first crow the hard way. Stop by the shop — you can build a Scarecrow now!", rewardKind=None, ctaKind=OpenEquipment.
3. Create an empty GameObject `InboxManager` in `SampleScene`, add the `InboxManager` component, and assign the `LetterCatalog` asset to its `catalog` field. Save the scene.

> The exact `triggerFeatureFlag` value MUST match what `ResearchManager.OnFeatureFlagUnlocked` emits for the scarecrow. Grep `requiredFeatureFlag`/`unlocksFeatureID` on the scarecrow EquipmentData/ResearchData to confirm before saving the asset.

Run: `read_console` → Expected: no errors.

- [ ] **Step 5: Manual smoke**

Play mode: call `InboxManager.Instance.Deliver("welcome")`. Confirm `UnreadCount()==1`, a save is written, and `gamedata.json` `inboxLetters` has one entry. Call `ClaimReward("welcome")` → coins increase by 100, second call returns false.

- [ ] **Step 6: Commit**

```bash
git add Assets/Scripts/Narrative/InboxManager.cs Assets/Scripts/GameData.cs Assets/Scripts/SaveManager.cs Assets/Resources/LetterCatalog.asset Assets/Scenes/SampleScene.unity
git commit -m "feat(inbox): InboxManager deliver/claim + persistence + starter catalog"
```

---

## Task 8: NarrativeDirector (event → letter wiring)

**Files:**
- Create: `Assets/Scripts/Narrative/NarrativeDirector.cs`

**Interfaces:**
- Consumes: `NarrativeManager` (Task 6), `InboxManager` + catalog (Task 7), `ResearchManager.OnFeatureFlagUnlocked` (`event Action<string>`), `AnimalManager.OnAnimalUnlocked` (`event Action<string>`).
- Produces: `NarrativeDirector` MonoBehaviour (no public API; subscribes on enable, fires letters fire-once).

- [ ] **Step 1: Implement `NarrativeDirector`**

```csharp
// Assets/Scripts/Narrative/NarrativeDirector.cs
using UnityEngine;

/// <summary>Code-side wiring of the hybrid model: listens to existing game events and,
/// for any catalog letter whose trigger matches, delivers it exactly once (guarded by
/// the NarrativeManager ledger). Letter *content* is data; this is the *condition* logic.</summary>
[DefaultExecutionOrder(1200)] // after NarrativeManager/InboxManager (1100)
public class NarrativeDirector : MonoBehaviour
{
    private void OnEnable()
    {
        if (ResearchManager.Instance != null)
            ResearchManager.Instance.OnFeatureFlagUnlocked += OnFeatureFlagUnlocked;
        if (AnimalManager.Instance != null)
            AnimalManager.Instance.OnAnimalUnlocked += OnAnimalUnlocked;
    }

    private void OnDisable()
    {
        if (ResearchManager.Instance != null)
            ResearchManager.Instance.OnFeatureFlagUnlocked -= OnFeatureFlagUnlocked;
        if (AnimalManager.Instance != null)
            AnimalManager.Instance.OnAnimalUnlocked -= OnAnimalUnlocked;
    }

    private void OnFeatureFlagUnlocked(string featureId)
    {
        var catalog = InboxManager.Instance?.Catalog;
        if (catalog == null) return;
        foreach (var def in catalog.ByFeatureFlag(featureId)) TryFire(def);
    }

    private void OnAnimalUnlocked(string animalId)
    {
        var catalog = InboxManager.Instance?.Catalog;
        if (catalog == null) return;
        foreach (var def in catalog.ByAnimalId(animalId)) TryFire(def);
    }

    private void TryFire(LetterDef def)
    {
        if (def == null || string.IsNullOrEmpty(def.id)) return;
        if (NarrativeManager.Instance == null || InboxManager.Instance == null) return;

        string flag = "letter:" + def.id;
        if (NarrativeManager.Instance.HasFired(flag)) return;

        InboxManager.Instance.Deliver(def.id);
        NarrativeManager.Instance.MarkFired(flag);
    }
}
```

- [ ] **Step 2: Scene object + compile**

Unity MCP: add a `NarrativeDirector` component (on the `NarrativeManager` GameObject is fine). Save scene. `read_console` → Expected: no errors.

- [ ] **Step 3: Manual smoke**

Play mode: trigger the scarecrow feature flag (research it, or via a dev hook fire `ResearchManager.OnFeatureFlagUnlocked`). Confirm exactly one `scarecrow_unlock` letter arrives, and re-triggering the flag (relaunch + re-fire) delivers nothing more (`HasFired` blocks it).

- [ ] **Step 4: Commit**

```bash
git add Assets/Scripts/Narrative/NarrativeDirector.cs Assets/Scenes/SampleScene.unity
git commit -m "feat(narrative): NarrativeDirector fires catalog letters on game events"
```

---

## Task 9: FarmNamePopupUITK (first-run naming)

**Files:**
- Create: `Assets/Scripts/UI/FarmNamePopupUITK.cs`
- Create: `Assets/UI/FarmNamePopupUITK/FarmNamePopupUITK.uxml`
- Create: `Assets/UI/FarmNamePopupUITK/FarmNamePopupUITK.uss`

**Interfaces:**
- Consumes: `NarrativeManager` (SetFarmName, HasFired, MarkFired), `InboxManager` (Deliver welcome), `FarmName`/`FarmNameSuggestions` (Task 4).
- Produces: `FarmNamePopupUITK` singleton with `static FarmNamePopupUITK Instance`, `void Open(bool isFirstRun)`, `void Close()`, `bool IsOpen`.

- [ ] **Step 1: Create the UXML**

```xml
<!-- Assets/UI/FarmNamePopupUITK/FarmNamePopupUITK.uxml -->
<ui:UXML xmlns:ui="UnityEngine.UIElements">
    <ui:VisualElement name="popup-root" class="fnp-root" style="display: none;">
        <ui:VisualElement name="backdrop" class="fnp-backdrop" />
        <ui:VisualElement name="card" class="fnp-card">
            <ui:Label name="title" text="Your new farm's name:" class="fnp-title" />
            <ui:VisualElement class="fnp-field-row">
                <ui:TextField name="name-field" max-length="30" class="fnp-field" />
                <ui:Button name="dice-button" text="🎲" class="fnp-dice" />
            </ui:VisualElement>
            <ui:Label name="hint" text="(this can be changed later)" class="fnp-hint" />
            <ui:Label name="error" text="" class="fnp-error" />
            <ui:Button name="save-button" text="Save" class="fnp-save" />
        </ui:VisualElement>
    </ui:VisualElement>
</ui:UXML>
```

- [ ] **Step 2: Create the USS**

```css
/* Assets/UI/FarmNamePopupUITK/FarmNamePopupUITK.uss */
.fnp-root { position: absolute; top: 0; left: 0; right: 0; bottom: 0; }
.fnp-backdrop { position: absolute; top: 0; left: 0; right: 0; bottom: 0;
    background-color: rgba(0,0,0,0.6); }
.fnp-card { position: absolute; left: 60px; right: 60px; top: 600px;
    background-color: rgb(248,240,222); border-radius: 24px; padding: 28px;
    align-items: stretch; }
.fnp-title { font-size: 38px; -unity-font-style: bold; color: rgb(60,45,30);
    -unity-text-align: middle-center; margin-bottom: 18px; }
.fnp-field-row { flex-direction: row; align-items: center; }
.fnp-field { flex-grow: 1; font-size: 34px; height: 84px; }
.fnp-dice { width: 84px; height: 84px; font-size: 40px; margin-left: 12px;
    border-radius: 16px; }
.fnp-hint { font-size: 22px; color: rgb(120,105,90); -unity-text-align: middle-center;
    margin-top: 10px; }
.fnp-error { font-size: 22px; color: rgb(200,60,60); -unity-text-align: middle-center;
    margin-top: 6px; min-height: 26px; }
.fnp-save { font-size: 34px; height: 88px; margin-top: 18px; border-radius: 18px;
    background-color: rgb(120,180,90); color: white; -unity-font-style: bold; }
.fnp-save:disabled { background-color: rgb(180,180,170); opacity: 0.6; }
```

- [ ] **Step 3: Implement the controller**

```csharp
// Assets/Scripts/UI/FarmNamePopupUITK.cs
using System;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>First-run farm-naming modal. Required 3–30 char name, prefilled with a random
/// playful suggestion, re-rollable via the dice button. On save: stores the name, marks the
/// onboarding flag, and delivers the welcome letter. Reused by Settings for renaming.</summary>
[RequireComponent(typeof(UIDocument))]
public class FarmNamePopupUITK : MonoBehaviour
{
    public static FarmNamePopupUITK Instance { get; private set; }

    private UIDocument document;
    private VisualElement popupRoot;
    private TextField nameField;
    private Button diceButton;
    private Button saveButton;
    private Label errorLabel;

    private bool isOpen;
    private bool isFirstRun;
    private readonly System.Random rng = new System.Random();

    public bool IsOpen => isOpen;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        document = GetComponent<UIDocument>();
    }

    private void OnDestroy() { if (Instance == this) Instance = null; }

    private void Start()
    {
        CacheElements();
        WireCallbacks();
        // Auto-open on a fresh game: name not yet chosen.
        if (NarrativeManager.Instance != null && !NarrativeManager.Instance.HasFired("onboarding_named"))
            Open(isFirstRun: true);
    }

    private void CacheElements()
    {
        var root = document.rootVisualElement;
        root.pickingMode = PickingMode.Ignore;
        popupRoot   = root.Q<VisualElement>("popup-root");
        nameField   = root.Q<TextField>("name-field");
        diceButton  = root.Q<Button>("dice-button");
        saveButton  = root.Q<Button>("save-button");
        errorLabel  = root.Q<Label>("error");
    }

    private void WireCallbacks()
    {
        diceButton?.RegisterCallback<ClickEvent>(_ => Reroll());
        saveButton?.RegisterCallback<ClickEvent>(_ => OnSave());
        nameField?.RegisterValueChangedCallback(_ => Validate());
    }

    public void Open(bool isFirstRun)
    {
        this.isFirstRun = isFirstRun;
        isOpen = true;
        if (nameField != null)
        {
            string initial = isFirstRun || string.IsNullOrEmpty(NarrativeManager.Instance?.FarmName)
                ? FarmNameSuggestions.Random(rng)
                : NarrativeManager.Instance.FarmName;
            nameField.SetValueWithoutNotify(initial);
        }
        Validate();
        if (popupRoot != null) popupRoot.style.display = DisplayStyle.Flex;
    }

    public void Close()
    {
        isOpen = false;
        if (popupRoot != null) popupRoot.style.display = DisplayStyle.None;
    }

    private void Reroll()
    {
        nameField?.SetValueWithoutNotify(FarmNameSuggestions.Random(rng));
        Validate();
    }

    private void Validate()
    {
        bool valid = FarmName.IsValid(nameField?.value);
        if (saveButton != null) saveButton.SetEnabled(valid);
        if (errorLabel != null)
            errorLabel.text = valid ? "" : $"Name must be {FarmName.Min}–{FarmName.Max} characters.";
    }

    private void OnSave()
    {
        if (!FarmName.IsValid(nameField?.value)) return;
        NarrativeManager.Instance?.SetFarmName(nameField.value);

        if (isFirstRun && NarrativeManager.Instance != null
            && NarrativeManager.Instance.MarkFired("onboarding_named"))
        {
            InboxManager.Instance?.Deliver("welcome");
        }
        Close();
    }
}
```

- [ ] **Step 4: Scene wiring (Unity MCP)**

1. Create a GameObject `FarmNamePopupUITK` in `SampleScene` with a `UIDocument` component; set its Panel Settings to the project's shared PanelSettings (same asset the other popups use) and its Source Asset to `FarmNamePopupUITK.uxml`. Add a StyleSheet reference to `FarmNamePopupUITK.uss` on the UXML root (or via the UXML `<Style>` — match how the other popups attach USS).
2. Add the `FarmNamePopupUITK` component to the same GameObject.
3. Ensure its UIDocument sort order renders ABOVE gameplay HUD but is fine below nothing (it's a blocking first-run modal). Save scene.

`read_console` → Expected: no errors.

- [ ] **Step 5: Manual smoke**

Delete the save (`SaveManager.DeleteSave()` via Settings → Reset Save) and enter Play mode. Expected: the naming popup appears prefilled with a playful name; dice re-rolls; clearing to <3 chars disables Save and shows the error; Save stores the name, closes, and a `welcome` letter is now in the inbox (`InboxManager.Instance.UnreadCount()==1`). Relaunch → popup does NOT reappear.

- [ ] **Step 6: Commit**

```bash
git add Assets/Scripts/UI/FarmNamePopupUITK.cs Assets/UI/FarmNamePopupUITK Assets/Scenes/SampleScene.unity
git commit -m "feat(onboarding): first-run FarmNamePopupUITK (validate/dice/welcome letter)"
```

---

## Task 10: InboxPopupUITK (list + detail)

**Files:**
- Create: `Assets/Scripts/UI/InboxPopupUITK.cs`
- Create: `Assets/UI/InboxPopupUITK/InboxPopupUITK.uxml`
- Create: `Assets/UI/InboxPopupUITK/InboxPopupUITK.uss`

**Interfaces:**
- Consumes: `InboxManager` (Entries, GetDef, MarkRead, ClaimReward, OnInboxChanged), `NarrativeManager.FarmName`, `NarrativeText.Resolve`, `RewardKind`, `CtaKind`.
- Produces: `InboxPopupUITK` singleton with `static InboxPopupUITK Instance`, `void Open()`, `void Close()`, `bool IsOpen`.

- [ ] **Step 1: Create the UXML**

```xml
<!-- Assets/UI/InboxPopupUITK/InboxPopupUITK.uxml -->
<ui:UXML xmlns:ui="UnityEngine.UIElements">
    <ui:VisualElement name="popup-root" class="inbox-root" style="display: none;">
        <ui:VisualElement name="backdrop" class="inbox-backdrop" />
        <ui:VisualElement name="card" class="inbox-card">
            <ui:VisualElement class="inbox-header">
                <ui:Button name="back-button" text="‹" class="inbox-back" style="display:none;" />
                <ui:Label name="header-title" text="Mailbox" class="inbox-title" />
                <ui:Button name="close-button" text="✕" class="inbox-close" />
            </ui:VisualElement>

            <!-- List view -->
            <ui:ScrollView name="list-view" class="inbox-list" />

            <!-- Detail view -->
            <ui:VisualElement name="detail-view" class="inbox-detail" style="display:none;">
                <ui:VisualElement class="inbox-sender-row">
                    <ui:VisualElement name="portrait" class="inbox-portrait" />
                    <ui:Label name="sender-name" class="inbox-sender" />
                </ui:VisualElement>
                <ui:Label name="detail-subject" class="inbox-subject" />
                <ui:Label name="detail-body" class="inbox-body" />
                <ui:Button name="claim-button" text="Claim" class="inbox-claim" style="display:none;" />
                <ui:Button name="cta-button" text="Go" class="inbox-cta" style="display:none;" />
            </ui:VisualElement>
        </ui:VisualElement>
    </ui:VisualElement>
</ui:UXML>
```

- [ ] **Step 2: Create the USS**

```css
/* Assets/UI/InboxPopupUITK/InboxPopupUITK.uss */
.inbox-root { position: absolute; top: 0; left: 0; right: 0; bottom: 0; }
.inbox-backdrop { position: absolute; top:0; left:0; right:0; bottom:0;
    background-color: rgba(0,0,0,0.6); }
.inbox-card { position: absolute; left: 40px; right: 40px; top: 300px; bottom: 300px;
    background-color: rgb(248,240,222); border-radius: 24px; padding: 20px; }
.inbox-header { flex-direction: row; align-items: center; margin-bottom: 12px; }
.inbox-title { flex-grow: 1; font-size: 36px; -unity-font-style: bold;
    -unity-text-align: middle-center; color: rgb(60,45,30); }
.inbox-back, .inbox-close { width: 60px; height: 60px; font-size: 32px;
    background-color: rgba(0,0,0,0); color: rgb(60,45,30); }
.inbox-list { flex-grow: 1; }
.inbox-row { flex-direction: row; align-items: center; padding: 16px;
    background-color: rgb(255,250,238); border-radius: 14px; margin-bottom: 10px; }
.inbox-row-unread { border-left-width: 6px; border-left-color: rgb(220,80,80); }
.inbox-row-text { flex-grow: 1; }
.inbox-row-sender { font-size: 22px; color: rgb(120,100,80); }
.inbox-row-subject { font-size: 28px; color: rgb(50,40,30); -unity-font-style: bold; }
.inbox-row-dot { width: 18px; height: 18px; border-radius: 9px;
    background-color: rgb(220,80,80); }
.inbox-detail { flex-grow: 1; }
.inbox-sender-row { flex-direction: row; align-items: center; margin-bottom: 12px; }
.inbox-portrait { width: 96px; height: 96px; border-radius: 48px; margin-right: 14px;
    background-color: rgb(220,210,190); }
.inbox-sender { font-size: 30px; -unity-font-style: bold; color: rgb(60,45,30); }
.inbox-subject { font-size: 32px; -unity-font-style: bold; color: rgb(50,40,30);
    margin-bottom: 10px; }
.inbox-body { font-size: 26px; color: rgb(60,50,40); white-space: normal; }
.inbox-claim { font-size: 30px; height: 80px; margin-top: 18px; border-radius: 16px;
    background-color: rgb(230,180,60); color: white; -unity-font-style: bold; }
.inbox-claim:disabled { background-color: rgb(180,180,170); opacity: 0.6; }
.inbox-cta { font-size: 30px; height: 80px; margin-top: 12px; border-radius: 16px;
    background-color: rgb(110,160,210); color: white; -unity-font-style: bold; }
.inbox-empty { font-size: 26px; color: rgb(120,105,90); -unity-text-align: middle-center;
    margin-top: 40px; }
```

- [ ] **Step 3: Implement the controller**

```csharp
// Assets/Scripts/UI/InboxPopupUITK.cs
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>Mailbox UI: a list of received letters → a detail view per letter with an
/// optional Claim (reward) and CTA (navigation). Reads content from InboxManager's
/// catalog; resolves {farmName} tokens at display time.</summary>
[RequireComponent(typeof(UIDocument))]
public class InboxPopupUITK : MonoBehaviour
{
    public static InboxPopupUITK Instance { get; private set; }

    private UIDocument document;
    private VisualElement popupRoot, listView, detailView, portrait;
    private Button backButton, closeButton, claimButton, ctaButton;
    private Label headerTitle, senderName, detailSubject, detailBody;

    private bool isOpen;
    private string currentLetterId;
    public bool IsOpen => isOpen;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        document = GetComponent<UIDocument>();
    }

    private void OnDestroy()
    {
        if (InboxManager.Instance != null) InboxManager.Instance.OnInboxChanged -= OnInboxChanged;
        if (Instance == this) Instance = null;
    }

    private void OnEnable() { CacheElements(); WireCallbacks(); }

    private void Start()
    {
        if (InboxManager.Instance != null) InboxManager.Instance.OnInboxChanged += OnInboxChanged;
    }

    private void CacheElements()
    {
        var root = document.rootVisualElement;
        root.pickingMode = PickingMode.Ignore;
        popupRoot     = root.Q<VisualElement>("popup-root");
        listView      = root.Q<ScrollView>("list-view");
        detailView    = root.Q<VisualElement>("detail-view");
        portrait      = root.Q<VisualElement>("portrait");
        backButton    = root.Q<Button>("back-button");
        closeButton   = root.Q<Button>("close-button");
        claimButton   = root.Q<Button>("claim-button");
        ctaButton     = root.Q<Button>("cta-button");
        headerTitle   = root.Q<Label>("header-title");
        senderName    = root.Q<Label>("sender-name");
        detailSubject = root.Q<Label>("detail-subject");
        detailBody    = root.Q<Label>("detail-body");
    }

    private void WireCallbacks()
    {
        root_backdrop()?.RegisterCallback<ClickEvent>(_ => Close());
        closeButton?.RegisterCallback<ClickEvent>(_ => Close());
        backButton?.RegisterCallback<ClickEvent>(_ => ShowList());
        claimButton?.RegisterCallback<ClickEvent>(_ => OnClaim());
        ctaButton?.RegisterCallback<ClickEvent>(_ => OnCta());
    }

    private VisualElement root_backdrop() => document.rootVisualElement.Q<VisualElement>("backdrop");

    public void Open() { isOpen = true; if (popupRoot != null) popupRoot.style.display = DisplayStyle.Flex; ShowList(); }
    public void Close() { isOpen = false; if (popupRoot != null) popupRoot.style.display = DisplayStyle.None; }

    private void OnInboxChanged() { if (isOpen && detailView.style.display == DisplayStyle.None) ShowList(); }

    private void ShowList()
    {
        currentLetterId = null;
        if (backButton != null) backButton.style.display = DisplayStyle.None;
        if (headerTitle != null) headerTitle.text = "Mailbox";
        if (detailView != null) detailView.style.display = DisplayStyle.None;
        if (listView != null) listView.style.display = DisplayStyle.Flex;

        listView.Clear();
        var mgr = InboxManager.Instance;
        if (mgr == null || mgr.Entries.Count == 0)
        {
            var empty = new Label("No letters yet. Check back later!") { name = "empty" };
            empty.AddToClassList("inbox-empty");
            listView.Add(empty);
            return;
        }

        foreach (var entry in mgr.Entries)
        {
            var def = mgr.GetDef(entry.letterId);
            if (def == null) continue;

            var row = new VisualElement();
            row.AddToClassList("inbox-row");
            if (!entry.read) row.AddToClassList("inbox-row-unread");

            var textCol = new VisualElement(); textCol.AddToClassList("inbox-row-text");
            var s = new Label(def.senderName ?? ""); s.AddToClassList("inbox-row-sender");
            var subj = new Label(NarrativeText.Resolve(def.subject, NarrativeManager.Instance?.FarmName));
            subj.AddToClassList("inbox-row-subject");
            textCol.Add(s); textCol.Add(subj);
            row.Add(textCol);

            if (!entry.read)
            {
                var dot = new VisualElement(); dot.AddToClassList("inbox-row-dot");
                row.Add(dot);
            }

            string id = entry.letterId;
            row.RegisterCallback<ClickEvent>(_ => ShowDetail(id));
            listView.Add(row);
        }
    }

    private void ShowDetail(string letterId)
    {
        var mgr = InboxManager.Instance;
        var def = mgr?.GetDef(letterId);
        if (def == null) return;

        currentLetterId = letterId;
        mgr.MarkRead(letterId);

        if (backButton != null) backButton.style.display = DisplayStyle.Flex;
        if (headerTitle != null) headerTitle.text = "";
        if (listView != null) listView.style.display = DisplayStyle.None;
        if (detailView != null) detailView.style.display = DisplayStyle.Flex;

        string farm = NarrativeManager.Instance?.FarmName;
        if (senderName != null) senderName.text = def.senderName ?? "";
        if (detailSubject != null) detailSubject.text = NarrativeText.Resolve(def.subject, farm);
        if (detailBody != null) detailBody.text = NarrativeText.Resolve(def.body, farm);
        if (portrait != null)
            portrait.style.backgroundImage = def.senderPortrait != null
                ? new StyleBackground(def.senderPortrait) : new StyleBackground();

        bool hasReward = def.rewardKind != RewardKind.None && def.rewardAmount > 0;
        var entry = FindEntry(letterId);
        bool claimed = entry != null && entry.claimed;
        if (claimButton != null)
        {
            claimButton.style.display = hasReward ? DisplayStyle.Flex : DisplayStyle.None;
            claimButton.text = claimed ? "Claimed" : $"Claim {def.rewardAmount} {def.rewardKind}";
            claimButton.SetEnabled(hasReward && !claimed);
        }
        if (ctaButton != null)
        {
            ctaButton.style.display = def.ctaKind != CtaKind.None ? DisplayStyle.Flex : DisplayStyle.None;
            ctaButton.text = CtaLabel(def.ctaKind);
        }
    }

    private InboxEntry FindEntry(string letterId)
    {
        foreach (var e in InboxManager.Instance.Entries) if (e.letterId == letterId) return e;
        return null;
    }

    private void OnClaim()
    {
        if (currentLetterId == null) return;
        if (InboxManager.Instance != null && InboxManager.Instance.ClaimReward(currentLetterId))
            ShowDetail(currentLetterId); // refresh button → "Claimed"
    }

    private void OnCta()
    {
        var def = InboxManager.Instance?.GetDef(currentLetterId);
        if (def == null) return;
        switch (def.ctaKind)
        {
            case CtaKind.OpenEquipment: EquipmentPopupUITK.Instance?.Open(); Close(); break;
            case CtaKind.OpenResearch:  ResearchPopupUITK.Instance?.Open();  Close(); break;
            case CtaKind.OpenShop:      EquipmentPopupUITK.Instance?.Open(); Close(); break;
            default: Debug.Log($"[Inbox] CTA {def.ctaKind} not wired."); break;
        }
    }

    private static string CtaLabel(CtaKind kind)
    {
        switch (kind)
        {
            case CtaKind.OpenEquipment: return "Go to Equipment";
            case CtaKind.OpenResearch:  return "Go to Research";
            case CtaKind.OpenShop:      return "Go to Shop";
            default: return "Go";
        }
    }
}
```

> Before implementing `OnCta`, verify the real popup type names + `Open()` signatures (`EquipmentPopupUITK`, `ResearchPopupUITK`) with a grep. If a name differs, fix the switch to match; if a target popup doesn't exist, leave that branch as a `Debug.Log` stub. Do NOT invent a popup that isn't there.

- [ ] **Step 4: Scene wiring (Unity MCP)**

Create a GameObject `InboxPopupUITK` with a `UIDocument` (shared PanelSettings, Source Asset = `InboxPopupUITK.uxml`, USS attached), plus the `InboxPopupUITK` component. Save scene. `read_console` → Expected: no errors.

- [ ] **Step 5: Manual smoke**

With a `welcome` letter delivered: open the inbox via `InboxPopupUITK.Instance.Open()`. Expected: list shows the welcome row with an unread dot; tapping it opens the detail with the body's `{farmName}` resolved to the chosen name; Claim grants 100 coins and flips to "Claimed"; Back returns to the list and the unread dot is gone.

- [ ] **Step 6: Commit**

```bash
git add Assets/Scripts/UI/InboxPopupUITK.cs Assets/UI/InboxPopupUITK Assets/Scenes/SampleScene.unity
git commit -m "feat(inbox): InboxPopupUITK list + detail with claim/CTA"
```

---

## Task 11: Envelope top-bar button + unread dot

**Files:**
- Create: `Assets/Scripts/UI/InboxButton.cs`

**Interfaces:**
- Consumes: `InboxManager` (UnreadCount, OnInboxChanged), `InboxPopupUITK` (Open).
- Produces: `InboxButton` MonoBehaviour driving a uGUI Button + a notification-dot child (mirrors the daily-rewards basket button pattern — see `EggClaimButton`/`GemClaimButton` for the notification-dot precedent).

- [ ] **Step 1: Implement `InboxButton`**

```csharp
// Assets/Scripts/UI/InboxButton.cs
using UnityEngine;
using UnityEngine.UI;

/// <summary>Top-bar envelope button beside the daily-rewards basket. Opens the inbox and
/// shows an unread-count notification dot, refreshed on InboxManager.OnInboxChanged.</summary>
[RequireComponent(typeof(Button))]
public class InboxButton : MonoBehaviour
{
    [SerializeField] private GameObject notificationDot; // small dot child, toggled on unread
    [SerializeField] private Text unreadCountLabel;      // optional count inside the dot

    private Button button;

    private void Awake() { button = GetComponent<Button>(); }

    private void OnEnable()
    {
        button.onClick.AddListener(OnClick);
        if (InboxManager.Instance != null) InboxManager.Instance.OnInboxChanged += Refresh;
        Refresh();
    }

    private void OnDisable()
    {
        button.onClick.RemoveListener(OnClick);
        if (InboxManager.Instance != null) InboxManager.Instance.OnInboxChanged -= Refresh;
    }

    private void OnClick() { InboxPopupUITK.Instance?.Open(); }

    private void Refresh()
    {
        int unread = InboxManager.Instance != null ? InboxManager.Instance.UnreadCount() : 0;
        if (notificationDot != null) notificationDot.SetActive(unread > 0);
        if (unreadCountLabel != null) unreadCountLabel.text = unread > 9 ? "9+" : unread.ToString();
    }
}
```

- [ ] **Step 2: Scene wiring (Unity MCP)**

1. Inspect the existing top bar where the daily-rewards basket button lives (`find_gameobjects` for the basket/chest button used by `DailyRewardPopup`).
2. Duplicate that button's structure for an `InboxButton`: a uGUI `Button` with an envelope sprite, plus a small red dot child (reuse the notification-dot art used by `EggClaimButton`/the basket) with an optional count `Text`.
3. Add the `InboxButton` component; assign `notificationDot` and `unreadCountLabel`. Position it next to the basket. Save scene.

`read_console` → Expected: no errors.

- [ ] **Step 3: Manual smoke**

Fresh game → after naming, the welcome letter makes the envelope dot show "1". Tap the envelope → inbox opens. Read the letter → dot disappears. Relaunch → dot state persists (read letters stay read).

- [ ] **Step 4: Commit**

```bash
git add Assets/Scripts/UI/InboxButton.cs Assets/Scenes/SampleScene.unity
git commit -m "feat(inbox): top-bar envelope button with unread dot"
```

---

## Task 12: Settings rename row

**Files:**
- Modify: `Assets/Scripts/UI/SettingsPopupUITK.cs` (replace the "Display Name" stub in `BuildAccountSection`)

**Interfaces:**
- Consumes: `NarrativeManager` (FarmName), `FarmNamePopupUITK` (Open(isFirstRun:false)).

- [ ] **Step 1: Replace the Display Name stub**

In `Assets/Scripts/UI/SettingsPopupUITK.cs`, in `BuildAccountSection()`, replace these lines (161–163):

```csharp
        SpawnButtonRow(rows, "Display Name", "Currently: Player", "Edit",
            () => Debug.Log("[Settings] Display name editor stub"));
```

with:

```csharp
        string currentName = NarrativeManager.Instance != null && !string.IsNullOrEmpty(NarrativeManager.Instance.FarmName)
            ? NarrativeManager.Instance.FarmName
            : "Unnamed Farm";
        SpawnButtonRow(rows, "Farm Name", $"Currently: {currentName}", "Edit",
            () =>
            {
                Close();
                FarmNamePopupUITK.Instance?.Open(isFirstRun: false);
            });
```

- [ ] **Step 2: Compile + manual smoke**

`read_console` → Expected: no errors. In Play mode: open Settings → Account → "Farm Name" shows the current name; tapping Edit closes Settings and opens the naming popup prefilled with the current name; saving a new valid name updates it (reopen Settings to confirm the "Currently:" line changed). Editing does NOT deliver another welcome letter (not first-run).

- [ ] **Step 3: Commit**

```bash
git add Assets/Scripts/UI/SettingsPopupUITK.cs
git commit -m "feat(settings): farm-name rename row (reuses FarmNamePopup)"
```

---

## Final verification

- [ ] **Run the full EditMode suite** — Test Runner → all tests. Expected: every prior suite still green PLUS `NarrativeLedgerTests` (4), `InboxModelTests` (5), `LetterCatalogTests` (3), `FarmNameTests` (7), `NarrativeTextTests` (4) all PASS.
- [ ] **End-to-end smoke on a fresh save:** Reset Save → relaunch → name the farm (dice + validation) → welcome letter arrives (envelope dot) → open inbox, read + claim 100 coins → research the scarecrow → scarecrow letter arrives once → CTA opens Equipment → rename in Settings → relaunch → name persists, letters persist read/claimed, no popup re-appears, no duplicate letters.

---

## Self-Review Notes (addressed)

- **Spec coverage:** ledger (T1/T6), beats+dispatch (T3/T8 hybrid), first-run naming (T9) + Settings rename (T12), Inbox manager/UI/button (T7/T10/T11), reward+CTA+sender (T3/T7/T10), token resolution (T5/T10), persistence (T6/T7), gems reward (T7). Coach-marks explicitly deferred (CtaKind reserves room).
- **Type consistency:** `Deliver(letterId)` / `MarkRead` / `ClaimReward` / `GetDef` / `Entries` / `OnInboxChanged` used identically across T7, T10, T11. `HasFired`/`MarkFired`/`FarmName`/`SetFarmName` consistent across T6, T8, T9, T12. `RewardKind`/`CtaKind`/`LetterDef` fields consistent T3↔T7↔T10.
- **Known verify-before-code points flagged in-task:** real scarecrow feature-flag id (T7), real `EquipmentPopupUITK`/`ResearchPopupUITK` names + `Open()` (T10), basket button structure for the envelope (T11), shared PanelSettings asset (T9/T10).
