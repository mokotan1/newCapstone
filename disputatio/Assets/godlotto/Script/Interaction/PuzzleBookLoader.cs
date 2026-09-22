using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

/// <summary>
/// MaidRoomPuzzleBook.txt 같은 마커 기반 TXT 파일을 파싱해
/// BookPanelController 가 소비할 수 있는 PageData 배열로 반환한다.
///
/// TXT 포맷:
///   # 주석 줄
///   [PAGE]          ← 페이지 시작
///   [LEFT]  내용    ← 왼쪽 페이지 (BookPanelController.recipe 에 대응)
///   [RIGHT] 내용    ← 오른쪽 페이지 (BookPanelController.memo  에 대응)
///   [/PAGE]         ← 페이지 종료
/// </summary>
public static class PuzzleBookLoader
{
    [Serializable]
    public class PageData
    {
        public string left;   // 왼쪽 페이지 본문 (recipe)
        public string right;  // 오른쪽 페이지 본문 (memo)
    }

    /// <summary>Resources 폴더 안의 TXT 파일을 로드해 파싱한다 (확장자 제외).</summary>
    public static PageData[] Load(string resourcePath)
    {
        var asset = Resources.Load<TextAsset>(resourcePath);
        if (asset == null)
        {
            Debug.LogWarning($"[PuzzleBookLoader] 파일을 찾을 수 없음: Resources/{resourcePath}");
            return Array.Empty<PageData>();
        }
        return Parse(asset.text);
    }

    /// <summary>TXT 문자열을 직접 파싱한다.</summary>
    public static PageData[] Parse(string text)
    {
        if (string.IsNullOrEmpty(text)) return Array.Empty<PageData>();

        var pages   = new List<PageData>();
        var leftBuf = new StringBuilder();
        var rightBuf = new StringBuilder();
        bool inPage  = false;
        bool inLeft  = false;
        bool inRight = false;

        foreach (var rawLine in text.Replace("\r\n", "\n").Split('\n'))
        {
            var line = rawLine.TrimEnd();

            if (line.StartsWith("#")) continue;   // 주석

            switch (line)
            {
                case "[PAGE]":
                    inPage  = true;
                    inLeft  = false;
                    inRight = false;
                    leftBuf.Clear();
                    rightBuf.Clear();
                    break;

                case "[/PAGE]":
                    if (inPage)
                        pages.Add(new PageData
                        {
                            left  = leftBuf.ToString().Trim(),
                            right = rightBuf.ToString().Trim()
                        });
                    inPage = inLeft = inRight = false;
                    break;

                case "[LEFT]":
                    if (inPage) { inLeft = true; inRight = false; }
                    break;

                case "[RIGHT]":
                    if (inPage) { inRight = true; inLeft = false; }
                    break;

                default:
                    if (!inPage) break;
                    if (inLeft)  leftBuf.Append(line).Append('\n');
                    if (inRight) rightBuf.Append(line).Append('\n');
                    break;
            }
        }

        return pages.ToArray();
    }
}
