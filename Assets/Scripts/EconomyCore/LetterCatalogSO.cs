using System.Collections.Generic;
using UnityEngine;

/// <summary>Single authored catalog of every letter. InboxManager resolves letterId →
/// LetterDef through this; NarrativeDirector finds trigger matches through this.</summary>
[CreateAssetMenu(fileName = "LetterCatalog", menuName = "IdleFarm/Letter Catalog")]
public class LetterCatalogSO : ScriptableObject
{
    public LetterDef[] letters;
    public CastMember[] cast;
    public TipDef[] tips;

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

    /// <summary>Letters whose triggerEvent contains <paramref name="eventId"/> as a whole
    /// '|'-separated token.</summary>
    public IEnumerable<LetterDef> ByEvent(string eventId)
    {
        if (string.IsNullOrEmpty(eventId) || letters == null) yield break;
        foreach (var l in letters)
        {
            if (l == null || string.IsNullOrEmpty(l.triggerEvent)) continue;
            foreach (string token in l.triggerEvent.Split('|'))
                if (token.Trim() == eventId) { yield return l; break; }
        }
    }

    public TipDef GetTip(string id)
    {
        if (string.IsNullOrEmpty(id) || tips == null) return null;
        foreach (var t in tips) if (t != null && t.id == id) return t;
        return null;
    }

    public CastMember GetCast(string id)
    {
        if (string.IsNullOrEmpty(id) || cast == null) return null;
        foreach (var c in cast) if (c != null && c.id == id) return c;
        return null;
    }
}
