using NUnit.Framework;

[TestFixture]
public class TutorQuizBankRepositoryTests
{
    private const string ValidCsv =
        "question_id,question_ko\n" +
        "Q001,십자가에 달리신 분은?\n" +
        "Q002,다윗이 이긴 거인 이름은?\n" +
        "Q003,이스라엘을 애굽에서 이끈 인물은?\n";

    [Test]
    public void Parse_ValidCsv_ReturnsAllIdsAndNoErrors()
    {
        TutorQuizBankLoadResult result = TutorQuizBankRepository.Parse(ValidCsv, "test.csv");

        Assert.IsFalse(result.HasStructuralError);
        Assert.IsFalse(result.HasErrors);
        CollectionAssert.AreEqual(new[] { "Q001", "Q002", "Q003" }, result.ValidQuestionIds);
    }

    [Test]
    public void Parse_DuplicateQuestionId_ReportsErrorAndExcludesSecondOccurrence()
    {
        string csv =
            "question_id,question_ko\n" +
            "Q001,첫 번째 질문\n" +
            "Q001,중복된 질문\n" +
            "Q002,세 번째 질문\n";

        TutorQuizBankLoadResult result = TutorQuizBankRepository.Parse(csv, "bank.csv");

        Assert.IsFalse(result.HasStructuralError);
        Assert.IsTrue(result.HasErrors);
        StringAssert.Contains("duplicate", result.Errors[0]);
        StringAssert.Contains("Q001", result.Errors[0]);
        StringAssert.Contains("bank.csv", result.Errors[0]);
        CollectionAssert.AreEqual(new[] { "Q001", "Q002" }, result.ValidQuestionIds);
    }

    [Test]
    public void Parse_MissingQuestionIdColumn_IsStructuralError()
    {
        string csv = "id,question_ko\nQ001,질문\n";

        TutorQuizBankLoadResult result = TutorQuizBankRepository.Parse(csv, "bank.csv");

        Assert.IsTrue(result.HasStructuralError);
        Assert.IsTrue(result.HasErrors);
        Assert.AreEqual(0, result.ValidQuestionIds.Count);
        StringAssert.Contains("bank.csv", result.Errors[0]);
    }

    [Test]
    public void Parse_MissingQuestionTextColumn_IsStructuralError()
    {
        string csv = "question_id,question_en\nQ001,Who?\n";

        TutorQuizBankLoadResult result = TutorQuizBankRepository.Parse(csv, "bank.csv");

        Assert.IsTrue(result.HasStructuralError);
        Assert.AreEqual(0, result.ValidQuestionIds.Count);
    }

    [Test]
    public void Parse_EmptyCsv_IsStructuralError()
    {
        TutorQuizBankLoadResult result = TutorQuizBankRepository.Parse("", "bank.csv");

        Assert.IsTrue(result.HasStructuralError);
        Assert.IsTrue(result.HasErrors);
    }

    [Test]
    public void Parse_NullCsv_IsStructuralError()
    {
        TutorQuizBankLoadResult result = TutorQuizBankRepository.Parse(null, "bank.csv");

        Assert.IsTrue(result.HasStructuralError);
    }

    [Test]
    public void Parse_RowMissingQuestionId_ReportsErrorAndSkipsRow()
    {
        string csv =
            "question_id,question_ko\n" +
            "Q001,질문 1\n" +
            ",질문 없는 ID\n" +
            "Q002,질문 2\n";

        TutorQuizBankLoadResult result = TutorQuizBankRepository.Parse(csv, "bank.csv");

        Assert.IsFalse(result.HasStructuralError);
        Assert.IsTrue(result.HasErrors);
        StringAssert.Contains("missing", result.Errors[0]);
        CollectionAssert.AreEqual(new[] { "Q001", "Q002" }, result.ValidQuestionIds);
    }

    [Test]
    public void Parse_RowMissingQuestionText_ReportsErrorAndSkipsRow()
    {
        string csv =
            "question_id,question_ko\n" +
            "Q001,질문 1\n" +
            "Q002,\n" +
            "Q003,질문 3\n";

        TutorQuizBankLoadResult result = TutorQuizBankRepository.Parse(csv, "bank.csv");

        Assert.IsFalse(result.HasStructuralError);
        Assert.IsTrue(result.HasErrors);
        StringAssert.Contains("Q002", result.Errors[0]);
        CollectionAssert.AreEqual(new[] { "Q001", "Q003" }, result.ValidQuestionIds);
    }

    [Test]
    public void Parse_BlankLines_AreSkippedSilently()
    {
        string csv =
            "question_id,question_ko\n" +
            "Q001,질문 1\n" +
            "\n" +
            "Q002,질문 2\n";

        TutorQuizBankLoadResult result = TutorQuizBankRepository.Parse(csv, "bank.csv");

        Assert.IsFalse(result.HasErrors);
        CollectionAssert.AreEqual(new[] { "Q001", "Q002" }, result.ValidQuestionIds);
    }

    [Test]
    public void Parse_QuotedFieldWithEmbeddedComma_ParsesCorrectly()
    {
        string csv =
            "question_id,question_ko,tags\n" +
            "Q001,\"질문, 쉼표 포함\",\"a,b\"\n";

        TutorQuizBankLoadResult result = TutorQuizBankRepository.Parse(csv, "bank.csv");

        Assert.IsFalse(result.HasErrors);
        CollectionAssert.AreEqual(new[] { "Q001" }, result.ValidQuestionIds);
    }

    [Test]
    public void LoadFromTextAsset_NullAsset_IsStructuralError()
    {
        TutorQuizBankLoadResult result = TutorQuizBankRepository.LoadFromTextAsset(null, "custom-label");

        Assert.IsTrue(result.HasStructuralError);
        StringAssert.Contains("custom-label", result.Errors[0]);
    }

    [Test]
    public void LoadFromResources_MissingResource_IsStructuralError()
    {
        TutorQuizBankLoadResult result = TutorQuizBankRepository.LoadFromResources("NoSuchTutorQuizBank__Test");

        Assert.IsTrue(result.HasStructuralError);
    }

    [Test]
    public void LoadFromResources_DefaultBank_HasAtLeastFiveValidQuestions()
    {
        TutorQuizBankLoadResult result = TutorQuizBankRepository.LoadFromResources();

        Assert.IsFalse(result.HasStructuralError);
        Assert.GreaterOrEqual(result.ValidQuestionIds.Count, 5);
    }
}
