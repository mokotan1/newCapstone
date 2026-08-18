using System;
using System.IO;
using System.Text.RegularExpressions;
using Godlotto.QA.Developer;
using Godlotto.QA.EditorCli;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// Task 11 — Release-configuration compile gate.
/// EditMode cannot perform a true player Release compile, so this suite guards the
/// source-level <c>#if</c> wrappers that strip DeveloperQa entry points outside
/// <c>UNITY_EDITOR || DEVELOPMENT_BUILD</c> (CLI bridge: editor-only).
/// Removing those guards from the listed files fails these tests.
/// </summary>
[TestFixture]
public class DeveloperQaReleaseCompileGateTests
{
    private const string EditorOrDevelopmentBuild = "UNITY_EDITOR || DEVELOPMENT_BUILD";
    private const string EditorOnly = "UNITY_EDITOR";

    private static readonly Regex IfDirective =
        new Regex(@"^\s*#if\s+(.+?)\s*$", RegexOptions.Multiline | RegexOptions.CultureInvariant);

    private static readonly Regex EndifDirective =
        new Regex(@"^\s*#endif\b", RegexOptions.Multiline | RegexOptions.CultureInvariant);

    [Test]
    public void IDeveloperQaService_Source_IsWrappedInEditorOrDevelopmentBuildGuard()
    {
        AssertTypeWrappedInPreprocessorGuard(
            ResolveUnderAssets("mokotan/mokotan/script/QA/Developer/IDeveloperQaService.cs"),
            "interface IDeveloperQaService",
            EditorOrDevelopmentBuild);
    }

    [Test]
    public void DeveloperQaService_Source_IsWrappedInEditorOrDevelopmentBuildGuard()
    {
        AssertTypeWrappedInPreprocessorGuard(
            ResolveUnderAssets("mokotan/mokotan/script/QA/Developer/DeveloperQaService.cs"),
            "class DeveloperQaService",
            EditorOrDevelopmentBuild);
    }

    [Test]
    public void DeveloperQaCliBridge_Source_IsWrappedInEditorOnlyGuard()
    {
        AssertTypeWrappedInPreprocessorGuard(
            ResolveUnderAssets("Editor/QA/DeveloperQaCliBridge.cs"),
            "class DeveloperQaCliBridge",
            EditorOnly);
    }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    [Test]
    public void KeyDeveloperQaTypes_AreVisible_WhenEditorOrDevelopmentBuildDefined()
    {
        Assert.IsNotNull(typeof(IDeveloperQaService));
        Assert.IsNotNull(typeof(DeveloperQaService));
        Assert.IsTrue(typeof(IDeveloperQaService).IsInterface);
        Assert.IsTrue(typeof(IDeveloperQaService).IsAssignableFrom(typeof(DeveloperQaService)));
    }
#endif

#if UNITY_EDITOR
    [Test]
    public void DeveloperQaCliBridge_IsVisible_WhenUnityEditorDefined()
    {
        Assert.IsNotNull(typeof(DeveloperQaCliBridge));
        Assert.IsTrue(typeof(DeveloperQaCliBridge).IsAbstract && typeof(DeveloperQaCliBridge).IsSealed);
    }
#endif

    private static string ResolveUnderAssets(string relativeUnderAssets)
    {
        return Path.GetFullPath(Path.Combine(Application.dataPath, relativeUnderAssets));
    }

    private static void AssertTypeWrappedInPreprocessorGuard(
        string absolutePath,
        string typeMarker,
        string expectedCondition)
    {
        Assert.IsTrue(File.Exists(absolutePath), "Missing source file: " + absolutePath);

        string source = File.ReadAllText(absolutePath);
        int typeIndex = source.IndexOf(typeMarker, StringComparison.Ordinal);
        Assert.GreaterOrEqual(
            typeIndex,
            0,
            "Type marker '" + typeMarker + "' not found in " + absolutePath);

        string beforeType = source.Substring(0, typeIndex);
        MatchCollection ifMatches = IfDirective.Matches(beforeType);
        Assert.Greater(
            ifMatches.Count,
            0,
            "Expected a #if before '" + typeMarker + "' in " + absolutePath +
            ". DeveloperQa entry points must stay compile-gated for release builds.");

        string actualCondition = NormalizeCondition(ifMatches[ifMatches.Count - 1].Groups[1].Value);
        Assert.AreEqual(
            NormalizeCondition(expectedCondition),
            actualCondition,
            "'" + typeMarker + "' must be under #if " + expectedCondition +
            " (nearest preceding #if was: " + actualCondition + ")");

        string afterType = source.Substring(typeIndex);
        Assert.IsTrue(
            EndifDirective.IsMatch(afterType),
            "Expected a matching #endif after '" + typeMarker + "' in " + absolutePath);
    }

    private static string NormalizeCondition(string condition)
    {
        return Regex.Replace(condition.Trim(), @"\s+", " ");
    }
}
