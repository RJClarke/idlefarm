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
