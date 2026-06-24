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
