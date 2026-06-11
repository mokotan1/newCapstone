using NUnit.Framework;
using UnityEngine;

public class TitleStylePayloadTests
{
    [TearDown]
    public void TearDown()
    {
        TitleStyleService.ResetCacheForTests();
    }

    [Test]
    public void FromJson_ExamplePayload_MatchesBackendContract()
    {
        const string json = @"{
            ""text"": ""DISPUTATIO"",
            ""language"": ""en"",
            ""fontKey"": ""cinzel"",
            ""color"": ""#c11414"",
            ""darkColor"": ""#4a0101"",
            ""brightColor"": ""#ff2a2a"",
            ""dripIntensity"": 0.75,
            ""poolEnabled"": true,
            ""seed"": 1138
        }";

        var payload = TitleStylePayload.FromJson(json);

        Assert.AreEqual("DISPUTATIO", payload.Text);
        Assert.AreEqual("en", payload.Language);
        Assert.AreEqual("cinzel", payload.FontKey);
        Assert.AreEqual("#c11414", payload.ColorHex);
        Assert.AreEqual("#4a0101", payload.DarkColorHex);
        Assert.AreEqual("#ff2a2a", payload.BrightColorHex);
        Assert.AreEqual(0.75f, payload.DripIntensity, 0.0001f);
        Assert.IsTrue(payload.PoolEnabled);
        Assert.IsTrue(payload.HasSeed);
        Assert.AreEqual(1138, payload.Seed);
    }

    [Test]
    public void ClampDripIntensity_ClampsOutOfRangeValues()
    {
        Assert.AreEqual(0f, TitleStylePayload.ClampDripIntensity(-0.5f), 0.0001f);
        Assert.AreEqual(1f, TitleStylePayload.ClampDripIntensity(1.5f), 0.0001f);
        Assert.AreEqual(0.25f, TitleStylePayload.ClampDripIntensity(0.25f), 0.0001f);
    }

    [Test]
    public void FromJson_MissingOptionalFields_UsesSafeDefaults()
    {
        const string json = @"{
            ""text"": ""TEST"",
            ""language"": ""ko"",
            ""fontKey"": ""nanum""
        }";

        var payload = TitleStylePayload.FromJson(json);

        Assert.AreEqual("TEST", payload.Text);
        Assert.AreEqual(TitleStylePayload.DefaultColorHex, payload.ColorHex);
        Assert.AreEqual(TitleStylePayload.DefaultDarkColorHex, payload.DarkColorHex);
        Assert.AreEqual(TitleStylePayload.DefaultBrightColorHex, payload.BrightColorHex);
        Assert.AreEqual(TitleStylePayload.DefaultDripIntensity, payload.DripIntensity, 0.0001f);
        Assert.IsTrue(payload.PoolEnabled);
        Assert.IsFalse(payload.HasSeed);
        Assert.AreEqual(0, payload.Seed);
    }

    [Test]
    public void FromJson_MissingRequiredFields_FallsBackSafely()
    {
        var payload = TitleStylePayload.FromJson("{}");

        Assert.AreEqual(TitleStylePayload.DefaultText, payload.Text);
        Assert.AreEqual(TitleStylePayload.DefaultLanguage, payload.Language);
        Assert.AreEqual(TitleStylePayload.DefaultFontKey, payload.FontKey);
    }

    [Test]
    public void ParseHexColor_InvalidHex_ReturnsFallback()
    {
        var fallback = Color.magenta;
        var parsed = TitleStylePayload.ParseHexColor("not-a-color", fallback);

        Assert.AreEqual(fallback, parsed);
    }

    [Test]
    public void CreateDefault_MatchesPrototypeDefaults()
    {
        var payload = TitleStylePayload.CreateDefault();

        Assert.AreEqual(TitleStylePayload.DefaultText, payload.Text);
        Assert.AreEqual(TitleStylePayload.DefaultDripIntensity, payload.DripIntensity, 0.0001f);
        Assert.IsTrue(payload.PoolEnabled);
        Assert.IsFalse(payload.HasSeed);
    }
}
