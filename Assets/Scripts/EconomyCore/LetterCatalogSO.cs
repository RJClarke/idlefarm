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
