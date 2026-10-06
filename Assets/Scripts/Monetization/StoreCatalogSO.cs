using UnityEngine;

/// <summary>The Store's products (spec §6). Resources/StoreCatalog; new products are data, not code.
/// Copy is edited here. Build Assets only appends missing ids, never overwrites.</summary>
public class StoreCatalogSO : ScriptableObject
{
    public StoreProductDef[] products = new StoreProductDef[0];

    private static StoreCatalogSO cached;
    public static StoreCatalogSO Instance
    {
        get
        {
            if (cached != null) return cached;
            cached = Resources.Load<StoreCatalogSO>("StoreCatalog");
            if (cached == null)
            {
                Debug.LogWarning("[Store] Resources/StoreCatalog missing; using StoreDefaults. Run Farm Game > Monetization > Build Assets.");
                cached = CreateInstance<StoreCatalogSO>();
                cached.products = StoreDefaults.Products;
            }
            return cached;
        }
    }

    public StoreProductDef Get(string id)
    {
        if (products == null || string.IsNullOrEmpty(id)) return null;
        foreach (StoreProductDef p in products) if (p != null && p.id == id) return p;
        return null;
    }
}
