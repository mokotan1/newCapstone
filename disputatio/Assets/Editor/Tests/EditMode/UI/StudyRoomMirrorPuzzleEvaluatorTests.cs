using Godlotto.Interaction;
using NUnit.Framework;
using UnityEngine;

[TestFixture]
public class StudyRoomMirrorPuzzleEvaluatorTests
{
    const float Epsilon = 1e-3f;

    [Test]
    public void ComputeMirrorNormal_AtZeroDegrees_ReturnsBaseNormal()
    {
        Vector2 normal = StudyRoomMirrorPuzzleEvaluator.ComputeMirrorNormal(0f, Vector2.right);

        Assert.AreEqual(1f, normal.x, Epsilon);
        Assert.AreEqual(0f, normal.y, Epsilon);
    }

    [Test]
    public void ComputeMirrorNormal_At90Degrees_RotatesBaseNormalCounterClockwise()
    {
        Vector2 normal = StudyRoomMirrorPuzzleEvaluator.ComputeMirrorNormal(90f, Vector2.right);

        Assert.AreEqual(0f, normal.x, Epsilon);
        Assert.AreEqual(1f, normal.y, Epsilon);
    }

    [Test]
    public void ComputeReflectedDirection_StraightOnMirror_ReflectsBackToSource()
    {
        var input = new MirrorReflectionInput
        {
            mirrorPosition = Vector2.zero,
            mirrorAngleDegrees = 0f,
            lightSource = new Vector2(-100f, 0f),
            targetMarker = new Vector2(-100f, 0f),
            mirrorBaseNormal = Vector2.right
        };

        Vector2 reflected = StudyRoomMirrorPuzzleEvaluator.ComputeReflectedDirection(input);

        Assert.AreEqual(-1f, reflected.x, Epsilon);
        Assert.AreEqual(0f, reflected.y, Epsilon);
    }

    [Test]
    public void ReflectionAngleError_WhenReflectedHitsMarker_IsNearZero()
    {
        var input = new MirrorReflectionInput
        {
            mirrorPosition = Vector2.zero,
            mirrorAngleDegrees = 0f,
            lightSource = new Vector2(-100f, 0f),
            targetMarker = new Vector2(-100f, 0f),
            mirrorBaseNormal = Vector2.right
        };

        float error = StudyRoomMirrorPuzzleEvaluator.ReflectionAngleErrorDegrees(input);

        Assert.Less(error, 0.5f);
    }

    [Test]
    public void ReflectionAngleError_WhenReflectedMissesMarker_IsLarge()
    {
        var input = new MirrorReflectionInput
        {
            mirrorPosition = Vector2.zero,
            mirrorAngleDegrees = 0f,
            lightSource = new Vector2(-100f, 0f),
            targetMarker = new Vector2(0f, 100f),
            mirrorBaseNormal = Vector2.right
        };

        float error = StudyRoomMirrorPuzzleEvaluator.ReflectionAngleErrorDegrees(input);

        Assert.Greater(error, 80f);
    }

    [Test]
    public void IsReflectionSolution_WithinTolerance_ReturnsTrue()
    {
        var input = new MirrorReflectionInput
        {
            mirrorPosition = Vector2.zero,
            mirrorAngleDegrees = 0f,
            lightSource = new Vector2(-100f, 0f),
            targetMarker = new Vector2(-100f, 0f),
            mirrorBaseNormal = Vector2.right
        };

        Assert.IsTrue(StudyRoomMirrorPuzzleEvaluator.IsReflectionSolution(input, 12f));
    }

    [Test]
    public void IsReflectionSolution_OutsideTolerance_ReturnsFalse()
    {
        var input = new MirrorReflectionInput
        {
            mirrorPosition = Vector2.zero,
            mirrorAngleDegrees = 0f,
            lightSource = new Vector2(-100f, 0f),
            targetMarker = new Vector2(0f, 100f),
            mirrorBaseNormal = Vector2.right
        };

        Assert.IsFalse(StudyRoomMirrorPuzzleEvaluator.IsReflectionSolution(input, 12f));
    }

    [Test]
    public void IsFullSolution_WhenPositionAngleAndReflectionAllMatch_ReturnsTrue()
    {
        // 거울 (10,-10), 광원·표식이 같은 점 (10,90) 위 → 입사광 (0,-1), 원하는 반사 (0,1).
        // 이를 만족하려면 거울 법선이 수직이어야 하므로 각도 90°.
        var input = new MirrorReflectionInput
        {
            mirrorPosition = new Vector2(10f, -10f),
            mirrorAngleDegrees = 90f,
            lightSource = new Vector2(10f, 90f),
            targetMarker = new Vector2(10f, 90f),
            mirrorBaseNormal = Vector2.right
        };

        bool solved = StudyRoomMirrorPuzzleEvaluator.IsFullSolution(
            currentAnchoredPosition: new Vector2(10f, -10f),
            currentVisualAngleDegrees: 90f,
            targetAnchoredPosition: new Vector2(10f, -10f),
            targetVisualAngleDegrees: 90f,
            positionTolerance: 46f,
            angleToleranceDegrees: 10f,
            reflectionInput: input,
            reflectionToleranceDegrees: 12f);

        Assert.IsTrue(solved);
    }

    [Test]
    public void IsFullSolution_WhenAngleOff_ReturnsFalse()
    {
        var input = new MirrorReflectionInput
        {
            mirrorPosition = new Vector2(10f, -10f),
            mirrorAngleDegrees = 80f,
            lightSource = new Vector2(10f, 90f),
            targetMarker = new Vector2(10f, 90f),
            mirrorBaseNormal = Vector2.right
        };

        bool solved = StudyRoomMirrorPuzzleEvaluator.IsFullSolution(
            currentAnchoredPosition: new Vector2(10f, -10f),
            currentVisualAngleDegrees: 80f,
            targetAnchoredPosition: new Vector2(10f, -10f),
            targetVisualAngleDegrees: 35f,
            positionTolerance: 46f,
            angleToleranceDegrees: 10f,
            reflectionInput: input,
            reflectionToleranceDegrees: 12f);

        Assert.IsFalse(solved);
    }

    [Test]
    public void IsFullSolution_WhenReflectionMisses_ReturnsFalse()
    {
        var input = new MirrorReflectionInput
        {
            mirrorPosition = new Vector2(10f, -10f),
            mirrorAngleDegrees = 35f,
            lightSource = new Vector2(-100f, 0f),
            targetMarker = new Vector2(300f, 250f),
            mirrorBaseNormal = Vector2.right
        };

        bool solved = StudyRoomMirrorPuzzleEvaluator.IsFullSolution(
            currentAnchoredPosition: new Vector2(10f, -10f),
            currentVisualAngleDegrees: 35f,
            targetAnchoredPosition: new Vector2(10f, -10f),
            targetVisualAngleDegrees: 35f,
            positionTolerance: 46f,
            angleToleranceDegrees: 10f,
            reflectionInput: input,
            reflectionToleranceDegrees: 12f);

        Assert.IsFalse(solved);
    }

    [Test]
    public void SolveIntensity01_AtPerfectAlignment_IsNearOne()
    {
        var input = new MirrorReflectionInput
        {
            mirrorPosition = new Vector2(10f, -10f),
            mirrorAngleDegrees = 90f,
            lightSource = new Vector2(10f, 90f),
            targetMarker = new Vector2(10f, 90f),
            mirrorBaseNormal = Vector2.right
        };

        float intensity = StudyRoomMirrorPuzzleEvaluator.SolveIntensity01(
            currentAnchoredPosition: new Vector2(10f, -10f),
            targetAnchoredPosition: new Vector2(10f, -10f),
            positionFalloff: 240f,
            currentVisualAngleDegrees: 90f,
            targetVisualAngleDegrees: 90f,
            angleFalloff: 70f,
            reflectionInput: input,
            reflectionFalloffDegrees: 70f);

        Assert.GreaterOrEqual(intensity, 0.99f);
        Assert.LessOrEqual(intensity, 1f);
    }

    [Test]
    public void SolveIntensity01_WhenFarFromSolution_IsLow()
    {
        var input = new MirrorReflectionInput
        {
            mirrorPosition = new Vector2(200f, 200f),
            mirrorAngleDegrees = -80f,
            lightSource = new Vector2(-100f, 0f),
            targetMarker = new Vector2(10f, 90f),
            mirrorBaseNormal = Vector2.right
        };

        float intensity = StudyRoomMirrorPuzzleEvaluator.SolveIntensity01(
            currentAnchoredPosition: new Vector2(200f, 200f),
            targetAnchoredPosition: new Vector2(10f, -10f),
            positionFalloff: 240f,
            currentVisualAngleDegrees: -80f,
            targetVisualAngleDegrees: 35f,
            angleFalloff: 70f,
            reflectionInput: input,
            reflectionFalloffDegrees: 70f);

        Assert.GreaterOrEqual(intensity, 0f);
        Assert.Less(intensity, 0.25f);
    }
}
