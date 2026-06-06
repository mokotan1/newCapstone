namespace Godlotto.Constants
{
    /// <summary>
    /// 게임 UI·대사용 한글 TMP SDF 경로. 에디터 빌더·런타임 참조 공통.
    /// </summary>
    public static class GameFontPaths
    {
        public const string KoreanRegularSdf = "Assets/Font/NanumGothic SDF.asset";
        public const string KoreanBoldSdf = "Assets/Font/NanumGothic Bold SDF.asset";
        public const string KoreanLightSdf = "Assets/Font/NanumGothic Light SDF.asset";
        public const string KoreanExtraBoldSdf = "Assets/Font/NanumGothic ExtraBold SDF.asset";

        /// <summary>일괄 교체 대상(구 Jalnan Gothic SDF guid).</summary>
        public const string LegacyJalnanSdfGuid = "34d8dd68c71273049a43918fae18662a";

        /// <summary>일괄 교체 대상(구 LiberationSans SDF guid — 게임 TMP 기본).</summary>
        public const string LegacyLiberationSansSdfGuid = "8f586378b4e144a9851e7b34d9b748ee";
    }
}
