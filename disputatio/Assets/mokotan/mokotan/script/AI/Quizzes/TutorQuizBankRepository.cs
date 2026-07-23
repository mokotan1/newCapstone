using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

/// <summary>
/// Parses the tutor quiz bank CSV (mirrors <c>backend_ai/data/tutor_quiz/quiz_bank.csv</c> question
/// IDs into <c>Assets/Resources/TutorQuizBank.csv</c>). Owns only what the client needs to pick a
/// session: valid, unique <c>question_id</c> values. Grading/answer text stays server-side
/// (<c>/tutor/grade</c>); the client never needs acceptable answers locally.
/// </summary>
internal static class TutorQuizBankRepository
{
    public const string QuestionIdColumn = "question_id";
    public const string QuestionTextColumn = "question_ko";
    public const string DefaultResourceName = "TutorQuizBank";

    /// <summary>
    /// Parses <paramref name="csvText"/> (RFC4180-lite: quoted fields, embedded commas/newlines).
    /// Never throws — structural or per-row problems are reported in
    /// <see cref="TutorQuizBankLoadResult.Errors"/> with file/row/question_id context.
    /// </summary>
    public static TutorQuizBankLoadResult Parse(string csvText, string sourceLabel = DefaultResourceName)
    {
        string label = string.IsNullOrWhiteSpace(sourceLabel) ? DefaultResourceName : sourceLabel;
        var errors = new List<string>();
        var validIds = new List<string>();

        if (string.IsNullOrWhiteSpace(csvText))
        {
            errors.Add($"{label}: CSV is empty.");
            return new TutorQuizBankLoadResult(validIds, errors, hasStructuralError: true);
        }

        List<List<string>> rows = ParseCsvRows(csvText);
        if (rows.Count == 0)
        {
            errors.Add($"{label}: CSV has no rows.");
            return new TutorQuizBankLoadResult(validIds, errors, hasStructuralError: true);
        }

        List<string> header = rows[0];
        int idIdx = IndexOfHeader(header, QuestionIdColumn);
        int textIdx = IndexOfHeader(header, QuestionTextColumn);
        if (idIdx < 0 || textIdx < 0)
        {
            errors.Add(
                $"{label}: missing required column(s) " +
                $"({(idIdx < 0 ? QuestionIdColumn : "")}{(idIdx < 0 && textIdx < 0 ? ", " : "")}" +
                $"{(textIdx < 0 ? QuestionTextColumn : "")}).");
            return new TutorQuizBankLoadResult(validIds, errors, hasStructuralError: true);
        }

        var seenIds = new HashSet<string>(StringComparer.Ordinal);
        for (int r = 1; r < rows.Count; r++)
        {
            List<string> row = rows[r];
            if (row.Count == 0 || IsBlankRow(row))
                continue;

            string id = GetCell(row, idIdx).Trim();
            if (id.Length == 0)
            {
                errors.Add($"{label} row {r + 1}: missing {QuestionIdColumn}.");
                continue;
            }

            if (!seenIds.Add(id))
            {
                errors.Add($"{label} row {r + 1}: duplicate {QuestionIdColumn} '{id}'.");
                continue;
            }

            string text = GetCell(row, textIdx).Trim();
            if (text.Length == 0)
            {
                errors.Add($"{label} row {r + 1}: question_id '{id}' missing {QuestionTextColumn}.");
                continue;
            }

            validIds.Add(id);
        }

        return new TutorQuizBankLoadResult(validIds, errors, hasStructuralError: false);
    }

    /// <summary>Loads <c>Resources/{resourceName}</c> (default <see cref="DefaultResourceName"/>).</summary>
    public static TutorQuizBankLoadResult LoadFromResources(string resourceName = DefaultResourceName)
    {
        string name = string.IsNullOrWhiteSpace(resourceName) ? DefaultResourceName : resourceName;
        TextAsset asset = Resources.Load<TextAsset>(name);
        if (asset == null)
        {
            return new TutorQuizBankLoadResult(
                new List<string>(),
                new List<string> { $"Resources/{name}: TextAsset not found." },
                hasStructuralError: true);
        }

        return Parse(asset.text, name);
    }

    public static TutorQuizBankLoadResult LoadFromTextAsset(TextAsset asset, string sourceLabel = null)
    {
        if (asset == null)
        {
            return new TutorQuizBankLoadResult(
                new List<string>(),
                new List<string> { $"{sourceLabel ?? DefaultResourceName}: TextAsset is null." },
                hasStructuralError: true);
        }

        return Parse(asset.text, string.IsNullOrWhiteSpace(sourceLabel) ? asset.name : sourceLabel);
    }

    // ------------------------------------------------------------------
    //  RFC4180-lite CSV parsing (quoted fields, embedded commas/newlines/escaped quotes)
    // ------------------------------------------------------------------

    private static List<List<string>> ParseCsvRows(string text)
    {
        var rows = new List<List<string>>();
        var row = new List<string>();
        var cell = new StringBuilder();
        bool inQuotes = false;

        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];
            if (inQuotes)
            {
                if (c == '"')
                {
                    if (i + 1 < text.Length && text[i + 1] == '"')
                    {
                        cell.Append('"');
                        i++;
                    }
                    else
                    {
                        inQuotes = false;
                    }
                }
                else
                {
                    cell.Append(c);
                }
                continue;
            }

            if (c == '"')
            {
                inQuotes = true;
            }
            else if (c == ',')
            {
                row.Add(cell.ToString());
                cell.Length = 0;
            }
            else if (c == '\n' || c == '\r')
            {
                if (c == '\r' && i + 1 < text.Length && text[i + 1] == '\n')
                    i++;
                row.Add(cell.ToString());
                cell.Length = 0;
                if (row.Count > 1 || (row.Count == 1 && row[0].Length > 0) || rows.Count > 0)
                    rows.Add(row);
                row = new List<string>();
            }
            else
            {
                cell.Append(c);
            }
        }

        row.Add(cell.ToString());
        if (row.Count > 1 || (row.Count == 1 && row[0].Length > 0))
            rows.Add(row);

        return rows;
    }

    private static bool IsBlankRow(List<string> row)
    {
        for (int i = 0; i < row.Count; i++)
        {
            if (!string.IsNullOrWhiteSpace(row[i]))
                return false;
        }
        return true;
    }

    private static int IndexOfHeader(List<string> header, string name)
    {
        for (int i = 0; i < header.Count; i++)
        {
            if (string.Equals(header[i].Trim(), name, StringComparison.OrdinalIgnoreCase))
                return i;
        }
        return -1;
    }

    private static string GetCell(List<string> row, int idx)
    {
        if (idx < 0 || idx >= row.Count)
            return "";
        return row[idx] ?? "";
    }
}

/// <summary>Outcome of parsing a tutor quiz bank CSV.</summary>
internal readonly struct TutorQuizBankLoadResult
{
    public TutorQuizBankLoadResult(
        IReadOnlyList<string> validQuestionIds,
        IReadOnlyList<string> errors,
        bool hasStructuralError)
    {
        ValidQuestionIds = validQuestionIds ?? Array.Empty<string>();
        Errors = errors ?? Array.Empty<string>();
        HasStructuralError = hasStructuralError;
    }

    /// <summary>Unique, non-empty question IDs with non-empty question text, in file order.</summary>
    public IReadOnlyList<string> ValidQuestionIds { get; }

    /// <summary>Human-readable problems (file/row/question_id context). Empty when the CSV is clean.</summary>
    public IReadOnlyList<string> Errors { get; }

    /// <summary>True when a required column is missing or the CSV could not be read at all.</summary>
    public bool HasStructuralError { get; }

    public bool HasErrors => Errors.Count > 0;
}
