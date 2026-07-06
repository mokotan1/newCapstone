using NUnit.Framework;

[TestFixture]
public sealed class UIDialRotatorTests
{
    [TestCase(-18f, 0)]
    [TestCase(0f, 0)]
    [TestCase(18f, 0)]
    [TestCase(18.01f, 1)]
    [TestCase(36f, 1)]
    [TestCase(54f, 1)]
    [TestCase(54.01f, 2)]
    [TestCase(-18.01f, 9)]
    [TestCase(-36f, 9)]
    [TestCase(-53.99f, 9)]
    [TestCase(-54.01f, 8)]
    [TestCase(342f, 0)]
    public void ResolveDigitFromRotation_UsesCenteredThirtySixDegreeBands(float rotationDegrees, int expectedDigit)
    {
        Assert.AreEqual(expectedDigit, UIDialRotator.ResolveDigitFromRotation(rotationDegrees, 36f));
    }

    [TestCase(true, false, true)]
    [TestCase(true, true, false)]
    [TestCase(false, false, false)]
    public void ShouldBeginDrag_RequiresDragAreaAndRejectsIgnoredTargets(
        bool pointerInsideDragArea,
        bool pointerInsideIgnoredTarget,
        bool expected)
    {
        Assert.AreEqual(
            expected,
            UIDialRotator.ShouldBeginDrag(pointerInsideDragArea, pointerInsideIgnoredTarget));
    }

    [Test]
    public void ResolveCenterScreenPosition_UsesConfiguredCenterWhenAvailable()
    {
        var dialCenter = new UnityEngine.Vector2(10f, 20f);
        var configuredCenter = new UnityEngine.Vector2(30f, 40f);

        Assert.AreEqual(
            configuredCenter,
            UIDialRotator.ResolveCenterScreenPosition(dialCenter, configuredCenter, true));
    }

    [Test]
    public void ResolveCenterScreenPosition_FallsBackToDialCenterWithoutConfiguredCenter()
    {
        var dialCenter = new UnityEngine.Vector2(10f, 20f);
        var configuredCenter = new UnityEngine.Vector2(30f, 40f);

        Assert.AreEqual(
            dialCenter,
            UIDialRotator.ResolveCenterScreenPosition(dialCenter, configuredCenter, false));
    }

    [Test]
    public void ResolveRotationDeltaFromCircularDirectionDrag_ClockwiseDrag_DecreasesZRotation()
    {
        float delta = UIDialRotator.ResolveRotationDeltaFromCircularDirectionDrag(
            centerScreenPos: new UnityEngine.Vector2(0f, 0f),
            previousPointerScreenPos: new UnityEngine.Vector2(100f, 0f),
            currentPointerScreenPos: new UnityEngine.Vector2(0f, -100f),
            fixedDegreesPerSecond: 120f,
            sensitivity: 1f,
            deltaTime: 0.5f,
            centerDeadZoneRadius: 8f,
            minimumPointerAngleDelta: 0.1f);

        Assert.AreEqual(-60f, delta);
    }

    [Test]
    public void ResolveRotationDeltaFromCircularDirectionDrag_CounterClockwiseDrag_IncreasesZRotation()
    {
        float delta = UIDialRotator.ResolveRotationDeltaFromCircularDirectionDrag(
            centerScreenPos: new UnityEngine.Vector2(0f, 0f),
            previousPointerScreenPos: new UnityEngine.Vector2(100f, 0f),
            currentPointerScreenPos: new UnityEngine.Vector2(0f, 100f),
            fixedDegreesPerSecond: 120f,
            sensitivity: 1f,
            deltaTime: 0.5f,
            centerDeadZoneRadius: 8f,
            minimumPointerAngleDelta: 0.1f);

        Assert.AreEqual(60f, delta);
    }

    [Test]
    public void ResolveRotationDeltaFromCircularDirectionDrag_IgnoresPointerRadiusAndAngleMagnitude()
    {
        float smallArc = UIDialRotator.ResolveRotationDeltaFromCircularDirectionDrag(
            centerScreenPos: new UnityEngine.Vector2(0f, 0f),
            previousPointerScreenPos: new UnityEngine.Vector2(100f, 0f),
            currentPointerScreenPos: new UnityEngine.Vector2(99f, 10f),
            fixedDegreesPerSecond: 90f,
            sensitivity: 1f,
            deltaTime: 0.25f,
            centerDeadZoneRadius: 8f,
            minimumPointerAngleDelta: 0.1f);
        float largeArc = UIDialRotator.ResolveRotationDeltaFromCircularDirectionDrag(
            centerScreenPos: new UnityEngine.Vector2(0f, 0f),
            previousPointerScreenPos: new UnityEngine.Vector2(1000f, 0f),
            currentPointerScreenPos: new UnityEngine.Vector2(0f, 1000f),
            fixedDegreesPerSecond: 90f,
            sensitivity: 1f,
            deltaTime: 0.25f,
            centerDeadZoneRadius: 8f,
            minimumPointerAngleDelta: 0.1f);

        Assert.AreEqual(smallArc, largeArc);
        Assert.AreEqual(22.5f, smallArc);
    }

    [Test]
    public void ResolveRotationDeltaFromCircularDirectionDrag_IgnoresCenterDeadZone()
    {
        float delta = UIDialRotator.ResolveRotationDeltaFromCircularDirectionDrag(
            centerScreenPos: new UnityEngine.Vector2(0f, 0f),
            previousPointerScreenPos: new UnityEngine.Vector2(2f, 0f),
            currentPointerScreenPos: new UnityEngine.Vector2(0f, 2f),
            fixedDegreesPerSecond: 120f,
            sensitivity: 1f,
            deltaTime: 0.5f,
            centerDeadZoneRadius: 8f,
            minimumPointerAngleDelta: 0.1f);

        Assert.AreEqual(0f, delta);
    }

    [Test]
    public void ResolveRotationDeltaFromCircularDirectionDrag_IgnoresTinyAngleJitter()
    {
        float delta = UIDialRotator.ResolveRotationDeltaFromCircularDirectionDrag(
            centerScreenPos: new UnityEngine.Vector2(0f, 0f),
            previousPointerScreenPos: new UnityEngine.Vector2(100f, 0f),
            currentPointerScreenPos: new UnityEngine.Vector2(100f, 0.05f),
            fixedDegreesPerSecond: 120f,
            sensitivity: 1f,
            deltaTime: 0.5f,
            centerDeadZoneRadius: 8f,
            minimumPointerAngleDelta: 0.1f);

        Assert.AreEqual(0f, delta);
    }
}
