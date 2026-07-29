using System;
using Godlotto.QA.Scenes;
using NUnit.Framework;

/// <summary>
/// <see cref="QaTargetId"/>의 정규화(소문자 dotted)와 거부 규칙(공백, 계층 구분자 <c>/</c> <c>\</c>,
/// 빈 값)을 검증합니다. QA Scenes 타입은 <c>UNITY_EDITOR || DEVELOPMENT_BUILD</c>에서만
/// 컴파일되며, 본 EditMode 테스트는 항상 에디터에서 실행되므로 해당 타입을 볼 수 있습니다.
/// </summary>
[TestFixture]
public class QaTargetIdTests
{
    // ---------------------------------------------------------------
    //  Valid input / normalization
    // ---------------------------------------------------------------

    [Test]
    public void TryCreate_LowercaseDottedId_Succeeds()
    {
        bool created = QaTargetId.TryCreate("kitchen.sink.faucet", out QaTargetId targetId, out string error);

        Assert.IsTrue(created);
        Assert.IsNull(error);
        Assert.AreEqual("kitchen.sink.faucet", targetId.Value);
        Assert.IsFalse(targetId.IsNone);
    }

    [Test]
    public void TryCreate_MixedCaseId_NormalizesToLowercase()
    {
        bool created = QaTargetId.TryCreate("Kitchen.Sink.Faucet", out QaTargetId targetId, out string error);

        Assert.IsTrue(created);
        Assert.AreEqual("kitchen.sink.faucet", targetId.Value);
    }

    [Test]
    public void Create_ValidId_ReturnsNormalizedTargetId()
    {
        QaTargetId targetId = QaTargetId.Create("MaidRoom.Food");

        Assert.AreEqual("maidroom.food", targetId.Value);
    }

    [Test]
    public void ToString_ReturnsNormalizedValue()
    {
        QaTargetId targetId = QaTargetId.Create("TutorRoom.Cheshire");

        Assert.AreEqual("tutorroom.cheshire", targetId.ToString());
    }

    // ---------------------------------------------------------------
    //  Equality
    // ---------------------------------------------------------------

    [Test]
    public void Equals_SameLogicalIdDifferentCase_AreEqualAfterNormalization()
    {
        QaTargetId lower = QaTargetId.Create("kitchen.sink.faucet");
        QaTargetId upper = QaTargetId.Create("KITCHEN.SINK.FAUCET");

        Assert.AreEqual(lower, upper);
        Assert.IsTrue(lower == upper);
        Assert.AreEqual(lower.GetHashCode(), upper.GetHashCode());
    }

    [Test]
    public void Equals_DifferentIds_AreNotEqual()
    {
        QaTargetId a = QaTargetId.Create("kitchen.sink.faucet");
        QaTargetId b = QaTargetId.Create("kitchen.maid-key");

        Assert.AreNotEqual(a, b);
        Assert.IsTrue(a != b);
    }

    [Test]
    public void None_IsDefaultAndIsNone()
    {
        QaTargetId none = QaTargetId.None;

        Assert.IsTrue(none.IsNone);
        Assert.AreEqual(string.Empty, none.Value);
    }

    // ---------------------------------------------------------------
    //  Rejection: blank / null
    // ---------------------------------------------------------------

    [Test]
    public void TryCreate_Null_Fails()
    {
        bool created = QaTargetId.TryCreate(null, out QaTargetId targetId, out string error);

        Assert.IsFalse(created);
        Assert.IsTrue(targetId.IsNone);
        Assert.IsNotEmpty(error);
    }

    [Test]
    public void TryCreate_Empty_Fails()
    {
        bool created = QaTargetId.TryCreate(string.Empty, out QaTargetId targetId, out string error);

        Assert.IsFalse(created);
        Assert.IsNotEmpty(error);
    }

    // ---------------------------------------------------------------
    //  Rejection: whitespace
    // ---------------------------------------------------------------

    [Test]
    public void TryCreate_InteriorWhitespace_Fails()
    {
        bool created = QaTargetId.TryCreate("kitchen. sink.faucet", out QaTargetId targetId, out string error);

        Assert.IsFalse(created);
        Assert.IsTrue(targetId.IsNone);
        StringAssert.Contains("whitespace", error);
    }

    [Test]
    public void TryCreate_LeadingWhitespace_Fails()
    {
        bool created = QaTargetId.TryCreate(" kitchen.sink.faucet", out QaTargetId targetId, out string error);

        Assert.IsFalse(created);
        Assert.IsNotEmpty(error);
    }

    [Test]
    public void TryCreate_TrailingWhitespace_Fails()
    {
        bool created = QaTargetId.TryCreate("kitchen.sink.faucet ", out QaTargetId targetId, out string error);

        Assert.IsFalse(created);
        Assert.IsNotEmpty(error);
    }

    // ---------------------------------------------------------------
    //  Rejection: hierarchy separators
    // ---------------------------------------------------------------

    [Test]
    public void TryCreate_ForwardSlash_Fails()
    {
        bool created = QaTargetId.TryCreate("Kitchen/Sink/Faucet", out QaTargetId targetId, out string error);

        Assert.IsFalse(created);
        Assert.IsTrue(targetId.IsNone);
        StringAssert.Contains("hierarchy separator", error);
    }

    [Test]
    public void TryCreate_BackSlash_Fails()
    {
        bool created = QaTargetId.TryCreate("Kitchen\\Sink\\Faucet", out QaTargetId targetId, out string error);

        Assert.IsFalse(created);
        StringAssert.Contains("hierarchy separator", error);
    }

    // ---------------------------------------------------------------
    //  Create() throwing overload
    // ---------------------------------------------------------------

    [Test]
    public void Create_InvalidId_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => QaTargetId.Create("bad/id"));
    }

    [Test]
    public void Create_Blank_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => QaTargetId.Create("   "));
    }
}
