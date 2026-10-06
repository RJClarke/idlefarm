using UnityEngine;

/// <summary>Skins with their art (Resources/SkinCatalog). Built by Farm Game > Monetization > Build Skin Catalog,
/// which appends missing SkinDefaults ids and never overwrites edited prices/names.</summary>
public class SkinCatalogSO : ScriptableObject
{
    public SkinDef[] skins = new SkinDef[0];

    private static SkinCatalogSO cached;
    public static SkinCatalogSO Instance
    {
        get
        {
            if (cached != null) return cached;
            cached = Resources.Load<SkinCatalogSO>("SkinCatalog");
            if (cached == null)
            {
                Debug.LogWarning("[Skins] Resources/SkinCatalog missing; using SkinDefaults without art. Run Farm Game > Monetization > Build Skin Catalog.");
                cached = CreateInstance<SkinCatalogSO>();
                cached.skins = SkinDefaults.All;
            }
            return cached;
        }
    }

    public SkinDef Get(string id)
    {
        if (skins == null || string.IsNullOrEmpty(id)) return null;
        foreach (SkinDef s in skins) if (s != null && s.id == id) return s;
        return null;
    }
}
