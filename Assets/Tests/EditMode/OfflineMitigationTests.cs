using NUnit.Framework;

public class OfflineMitigationTests
{
    [Test] public void Reduction_AbsentIsZero()  => Assert.AreEqual(0f, OfflineMitigation.Reduction(false, 0.5f, 1f), 1e-4f);
    [Test] public void Reduction_PresentBase()    => Assert.AreEqual(0.5f, OfflineMitigation.Reduction(true, 0.5f, 0f), 1e-4f);
    [Test] public void Reduction_EffectivenessScales() => Assert.AreEqual(0.6f, OfflineMitigation.Reduction(true, 0.5f, 0.2f), 1e-4f); // 0.5*1.2
    [Test] public void Reduction_ClampedToOne()    => Assert.AreEqual(1f, OfflineMitigation.Reduction(true, 0.9f, 1f), 1e-4f);
    [Test] public void Reduction_NegativeBonusClamped() => Assert.AreEqual(0.5f, OfflineMitigation.Reduction(true, 0.5f, -1f), 1e-4f);

    [Test] public void Stack_TwoSources_ComplementProduct() // 1-(1-.5)(1-.5)=.75
        => Assert.AreEqual(0.75f, OfflineMitigation.Stack(0.5f, 0.5f), 1e-4f);
    [Test] public void Stack_WithZero_Unchanged()  => Assert.AreEqual(0.4f, OfflineMitigation.Stack(0.4f, 0f), 1e-4f);
    [Test] public void Stack_NeverExceedsOne()     => Assert.AreEqual(1f, OfflineMitigation.Stack(1f, 0.5f), 1e-4f);
}
