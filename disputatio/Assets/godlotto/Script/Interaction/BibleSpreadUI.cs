using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>인스펙터에서 앵커·오프셋으로 UI 위치를 조정할 때 사용합니다 (부모 RectTransform 기준).</summary>
[System.Serializable]
public struct BibleUIRect
{
    [Tooltip("왼쪽 아래 기준 앵커 (0~1)")]
    public Vector2 anchorMin;
    [Tooltip("오른쪽 위 기준 앵커 (0~1)")]
    public Vector2 anchorMax;
    [Tooltip("픽셀 여백 (왼쪽·아래)")]
    public Vector2 offsetMin;
    [Tooltip("픽셀 여백 (오른쪽·위, 보통 음수)")]
    public Vector2 offsetMax;

    public void Apply(RectTransform rt)
    {
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.offsetMin = offsetMin;
        rt.offsetMax = offsetMax;
        rt.pivot = new Vector2(0.5f, 0.5f);
    }
}

/// <summary>텍스트 한 덩어리 — 문구·폰트 크기·색·정렬·영역을 인스펙터에서 설정합니다.</summary>
[System.Serializable]
public class BibleTextBlock
{
    [TextArea(2, 12)]
    [Tooltip("표시할 문장 (줄바꿈 가능)")]
    public string text;
    [Tooltip("글자 크기")]
    public float fontSize = 22f;
    public Color color = Color.black;
    public TextAlignmentOptions alignment = TextAlignmentOptions.Center;
    [Tooltip("부모 안에서 차지할 영역")]
    public BibleUIRect rect;
    [Tooltip("줄 간격 (TMP)")]
    public float lineSpacing;
}

/// <summary>헤더 가운데 작은 사각 아이콘 영역</summary>
[System.Serializable]
public class BibleHeaderIcon
{
    public BibleUIRect rect;
    public Color color = new Color(0.3f, 0.3f, 0.3f, 0.35f);
}

/// <summary>연산 버튼 한 칸 (부모는 OperatorColumn)</summary>
[System.Serializable]
public class BibleOperatorButton
{
    public string label = "+";
    [Tooltip("버튼 배경색")]
    public Color buttonBackground = new Color(0.92f, 0.92f, 0.94f, 1f);
    [Tooltip("글자 크기")]
    public float labelFontSize = 28f;
    public Color labelColor = new Color(0.1f, 0.1f, 0.1f);
    [Tooltip("OperatorColumn 안에서의 영역 (0~1 앵커)")]
    public BibleUIRect rect;
}

/// <summary>
/// StudyRoom BiblePanel — 단일 스프레드 UI. 위치·폰트는 인스펙터의 BibleTextBlock / BibleUIRect 로 조정합니다.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(RectTransform))]
public class BibleSpreadUI : MonoBehaviour
{
    private const string RootName = "SpreadRoot";
    private const int kLayoutVersion = 4;

    [Header("배경 스프라이트 (비어 있으면 Resources/BibleSpreadReference)")]
    [SerializeField] private Sprite spreadBackgroundOverride;
    [Tooltip("배경 이미지가 차지할 영역 (보통 전체)")]
    [SerializeField] private BibleUIRect spreadBackgroundRect = new BibleUIRect
    {
        anchorMin = Vector2.zero,
        anchorMax = Vector2.one,
        offsetMin = Vector2.zero,
        offsetMax = Vector2.zero
    };

    [Header("전역 폰트 (비어 있으면 TMP 기본 / Fallback)")]
    [SerializeField] private TMP_FontAsset fontOverride;

    [Header("뒤로가기 (BiblePanel 자식 Backspace)")]
    [SerializeField] private Color backButtonColor = new Color(0.15f, 0.45f, 0.95f, 1f);

    [Header("영역 — 패널 (부모: SpreadRoot)")]
    [SerializeField] private BibleUIRect headerBarPanel = new BibleUIRect
    {
        anchorMin = new Vector2(0f, 0.82f),
        anchorMax = new Vector2(1f, 1f),
        offsetMin = new Vector2(32f, 8f),
        offsetMax = new Vector2(-32f, -8f)
    };
    [SerializeField] private BibleUIRect contentAreaPanel = new BibleUIRect
    {
        anchorMin = new Vector2(0f, 0f),
        anchorMax = new Vector2(1f, 0.82f),
        offsetMin = new Vector2(32f, 24f),
        offsetMax = new Vector2(-32f, -16f)
    };

    [Header("영역 — 좌·우 페이지 (부모: ContentArea)")]
    [SerializeField] private BibleUIRect leftPagePanel = new BibleUIRect
    {
        anchorMin = new Vector2(0.02f, 0.06f),
        anchorMax = new Vector2(0.48f, 0.94f),
        offsetMin = Vector2.zero,
        offsetMax = Vector2.zero
    };
    [SerializeField] private BibleUIRect rightPagePanel = new BibleUIRect
    {
        anchorMin = new Vector2(0.52f, 0.06f),
        anchorMax = new Vector2(0.98f, 0.94f),
        offsetMin = Vector2.zero,
        offsetMax = Vector2.zero
    };

    [Header("텍스트 — 헤더 (부모: HeaderBar)")]
    [SerializeField] private BibleTextBlock headerLeft = new BibleTextBlock
    {
        text = "···",
        fontSize = 20f,
        color = new Color(0.2f, 0.2f, 0.2f),
        alignment = TextAlignmentOptions.Left,
        rect = new BibleUIRect
        {
            anchorMin = new Vector2(0.02f, 0f),
            anchorMax = new Vector2(0.31f, 1f),
            offsetMin = Vector2.zero,
            offsetMax = Vector2.zero
        }
    };
    [SerializeField] private BibleTextBlock headerCenter = new BibleTextBlock
    {
        text = "···",
        fontSize = 20f,
        color = new Color(0.2f, 0.2f, 0.2f),
        alignment = TextAlignmentOptions.Center,
        rect = new BibleUIRect
        {
            anchorMin = new Vector2(0.34f, 0f),
            anchorMax = new Vector2(0.66f, 1f),
            offsetMin = Vector2.zero,
            offsetMax = Vector2.zero
        }
    };
    [SerializeField] private BibleTextBlock headerRight = new BibleTextBlock
    {
        text = "···",
        fontSize = 20f,
        color = new Color(0.2f, 0.2f, 0.2f),
        alignment = TextAlignmentOptions.Right,
        rect = new BibleUIRect
        {
            anchorMin = new Vector2(0.69f, 0f),
            anchorMax = new Vector2(0.98f, 1f),
            offsetMin = Vector2.zero,
            offsetMax = Vector2.zero
        }
    };
    [SerializeField] private BibleHeaderIcon headerIcon = new BibleHeaderIcon
    {
        rect = new BibleUIRect
        {
            anchorMin = new Vector2(0.48f, 0.15f),
            anchorMax = new Vector2(0.52f, 0.85f),
            offsetMin = Vector2.zero,
            offsetMax = Vector2.zero
        }
    };

    [Header("텍스트 — 왼쪽 장 (부모: LeftPage)")]
    [SerializeField] private BibleTextBlock bodyLeft = new BibleTextBlock
    {
        text = "예수께서 이르시되\n\n나는 부활이요 생명이니\n\n나를 믿는 자는 죽어도 살겠고\n\n무릇 살아서 나를 믿는 자는\n\n영원히 죽지 아니하리니\n\n이것을 네가 믿느냐",
        fontSize = 22f,
        color = new Color(0.1f, 0.1f, 0.1f),
        alignment = TextAlignmentOptions.Center,
        rect = new BibleUIRect
        {
            anchorMin = new Vector2(0.08f, 0.22f),
            anchorMax = new Vector2(0.92f, 0.88f),
            offsetMin = Vector2.zero,
            offsetMax = Vector2.zero
        },
        lineSpacing = 0f
    };
    [SerializeField] private BibleTextBlock citation = new BibleTextBlock
    {
        text = "(John 11:25-26)",
        fontSize = 18f,
        color = new Color(0.25f, 0.25f, 0.25f),
        alignment = TextAlignmentOptions.Center,
        rect = new BibleUIRect
        {
            anchorMin = new Vector2(0.12f, 0.05f),
            anchorMax = new Vector2(0.88f, 0.18f),
            offsetMin = Vector2.zero,
            offsetMax = Vector2.zero
        }
    };

    [Header("텍스트 — 오른쪽 장 (부모: RightPage)")]
    [SerializeField] private BibleTextBlock titleRight = new BibleTextBlock
    {
        text = "해석본",
        fontSize = 26f,
        color = new Color(0.45f, 0.22f, 0.16f),
        alignment = TextAlignmentOptions.Center,
        rect = new BibleUIRect
        {
            anchorMin = new Vector2(0.08f, 0.76f),
            anchorMax = new Vector2(0.70f, 0.94f),
            offsetMin = Vector2.zero,
            offsetMax = Vector2.zero
        }
    };
    [SerializeField] private BibleTextBlock hintsRight = new BibleTextBlock
    {
        text = "나사로: 4일\n\n마리아: 300 데나리온\n\n마르타: 1가지 일",
        fontSize = 21f,
        color = Color.black,
        alignment = TextAlignmentOptions.Center,
        rect = new BibleUIRect
        {
            anchorMin = new Vector2(0.08f, 0.16f),
            anchorMax = new Vector2(0.68f, 0.70f),
            offsetMin = Vector2.zero,
            offsetMax = Vector2.zero
        }
    };

    [Header("영역 — 연산 버튼 열 (부모: RightPage)")]
    [SerializeField] private BibleUIRect operatorColumnPanel = new BibleUIRect
    {
        anchorMin = new Vector2(0.72f, 0.12f),
        anchorMax = new Vector2(0.98f, 0.76f),
        offsetMin = Vector2.zero,
        offsetMax = new Vector2(-6f, 0f)
    };

    [Header("연산 버튼 4개 (부모: OperatorColumn)")]
    [SerializeField] private BibleOperatorButton[] operatorButtons = new BibleOperatorButton[]
    {
        new BibleOperatorButton
        {
            label = "+",
            rect = new BibleUIRect
            {
                anchorMin = new Vector2(0f, 0.76f),
                anchorMax = new Vector2(1f, 0.99f),
                offsetMin = Vector2.zero,
                offsetMax = Vector2.zero
            }
        },
        new BibleOperatorButton
        {
            label = "+",
            rect = new BibleUIRect
            {
                anchorMin = new Vector2(0f, 0.51f),
                anchorMax = new Vector2(1f, 0.74f),
                offsetMin = Vector2.zero,
                offsetMax = Vector2.zero
            }
        },
        new BibleOperatorButton
        {
            label = "—",
            rect = new BibleUIRect
            {
                anchorMin = new Vector2(0f, 0.26f),
                anchorMax = new Vector2(1f, 0.49f),
                offsetMin = Vector2.zero,
                offsetMax = Vector2.zero
            }
        },
        new BibleOperatorButton
        {
            label = "+",
            rect = new BibleUIRect
            {
                anchorMin = new Vector2(0f, 0.01f),
                anchorMax = new Vector2(1f, 0.24f),
                offsetMin = Vector2.zero,
                offsetMax = Vector2.zero
            }
        }
    };

    [Header("디버그")]
    [Tooltip("체크 시 플레이 중 SpreadRoot를 지우고 인스펙터 값으로 다시 만듭니다 (테스트용)")]
    [SerializeField] private bool forceRebuildNextPlay;

    private IEnumerator Start()
    {
        var existing = transform.Find(RootName);
        if (existing != null && !forceRebuildNextPlay)
        {
            var m = existing.GetComponent<BibleSpreadLayoutMarker>();
            if (m != null && m.LayoutVersion >= kLayoutVersion)
                yield break;
            Destroy(existing.gameObject);
            yield return null;
        }
        else if (existing != null && forceRebuildNextPlay)
        {
            Destroy(existing.gameObject);
            yield return null;
        }

        if (transform.Find(RootName) == null)
            Build();

        if (forceRebuildNextPlay)
            forceRebuildNextPlay = false;
    }

#if UNITY_EDITOR
    [ContextMenu("BibleSpread/지금 레이아웃 재생성 (에디터)")]
    private void EditorRebuild()
    {
        var existing = transform.Find(RootName);
        if (existing != null)
            DestroyImmediate(existing.gameObject);
        Build();
    }
#endif

    private void Build()
    {
        var img = GetComponent<Image>();
        if (img != null)
            img.enabled = false;

        var root = new GameObject(RootName, typeof(RectTransform), typeof(CanvasRenderer), typeof(BibleSpreadLayoutMarker));
        root.GetComponent<BibleSpreadLayoutMarker>().LayoutVersion = kLayoutVersion;
        var rootRt = root.GetComponent<RectTransform>();
        rootRt.SetParent(transform, false);
        rootRt.anchorMin = Vector2.zero;
        rootRt.anchorMax = Vector2.one;
        rootRt.offsetMin = Vector2.zero;
        rootRt.offsetMax = Vector2.zero;

        var bgGo = new GameObject("SpreadBackground", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        var bgRt = bgGo.GetComponent<RectTransform>();
        bgRt.SetParent(rootRt, false);
        spreadBackgroundRect.Apply(bgRt);
        var bgImg = bgGo.GetComponent<Image>();
        bgImg.raycastTarget = false;
        bgImg.preserveAspect = true;
        bgImg.sprite = spreadBackgroundOverride ?? Resources.Load<Sprite>("BibleSpreadReference");

        var font = fontOverride ?? TMP_Settings.defaultFontAsset;
        if (font == null)
            font = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF - Fallback");

        var headerBar = CreatePanel(rootRt, "HeaderBar", headerBarPanel);
        ApplyTextBlock(headerBar, "HeaderLeft", headerLeft, font);
        ApplyTextBlock(headerBar, "HeaderCenter", headerCenter, font);
        ApplyTextBlock(headerBar, "HeaderRight", headerRight, font);
        CreateHeaderIcon(headerBar, "HeaderIcon", headerIcon);

        var content = CreatePanel(rootRt, "ContentArea", contentAreaPanel);

        var left = CreatePanel(content, "LeftPage", leftPagePanel);
        var right = CreatePanel(content, "RightPage", rightPagePanel);

        ApplyTextBlock(left, "Body", bodyLeft, font);
        ApplyTextBlock(left, "Citation", citation, font);

        ApplyTextBlock(right, "TitleRight", titleRight, font);
        ApplyTextBlock(right, "Hints", hintsRight, font);

        var opCol = CreatePanel(right, "OperatorColumn", operatorColumnPanel);
        if (operatorButtons != null)
        {
            for (var i = 0; i < operatorButtons.Length; i++)
                CreateOperatorButton(opCol, $"Op{i}", operatorButtons[i], font);
        }

        StyleBackButton();
    }

    private static void CreateHeaderIcon(RectTransform headerBar, string name, BibleHeaderIcon icon)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        var rt = go.GetComponent<RectTransform>();
        rt.SetParent(headerBar, false);
        icon.rect.Apply(rt);
        var image = go.GetComponent<Image>();
        image.color = icon.color;
        image.raycastTarget = false;
    }

    private void CreateOperatorButton(RectTransform column, string name, BibleOperatorButton def, TMP_FontAsset font)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        var rt = go.GetComponent<RectTransform>();
        rt.SetParent(column, false);
        def.rect.Apply(rt);

        var image = go.GetComponent<Image>();
        image.color = def.buttonBackground;
        image.raycastTarget = true;

        var btn = go.GetComponent<Button>();
        var captured = def.label;
        btn.onClick.AddListener(() => GameLog.Log($"[BibleSpreadUI] {name} ({captured})"));

        var textGo = new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        var tr = textGo.GetComponent<RectTransform>();
        tr.SetParent(rt, false);
        tr.anchorMin = Vector2.zero;
        tr.anchorMax = Vector2.one;
        tr.offsetMin = Vector2.zero;
        tr.offsetMax = Vector2.zero;
        var tmp = textGo.GetComponent<TextMeshProUGUI>();
        tmp.text = def.label;
        tmp.fontSize = def.labelFontSize;
        tmp.color = def.labelColor;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.raycastTarget = false;
        tmp.textWrappingMode = TextWrappingModes.NoWrap;
        if (font != null)
        {
            tmp.font = font;
            tmp.fontSharedMaterial = font.material;
        }
    }

    private void StyleBackButton()
    {
        var panel = transform.parent;
        if (panel == null)
            return;
        var back = panel.Find("Backspace");
        if (back == null)
            return;
        var image = back.GetComponent<Image>();
        if (image != null)
            image.color = backButtonColor;
    }

    private static RectTransform CreatePanel(RectTransform parent, string name, BibleUIRect r)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer));
        var rt = go.GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        r.Apply(rt);
        return rt;
    }

    private static void ApplyTextBlock(RectTransform parent, string name, BibleTextBlock block, TMP_FontAsset font)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        var rt = go.GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        block.rect.Apply(rt);

        var tmp = go.GetComponent<TextMeshProUGUI>();
        tmp.text = block.text;
        tmp.fontSize = block.fontSize;
        tmp.color = block.color;
        tmp.alignment = block.alignment;
        tmp.raycastTarget = false;
        tmp.textWrappingMode = TextWrappingModes.Normal;
        tmp.overflowMode = TextOverflowModes.Overflow;
        if (block.lineSpacing > 0f)
            tmp.lineSpacing = block.lineSpacing;
        if (font != null)
        {
            tmp.font = font;
            tmp.fontSharedMaterial = font.material;
        }
    }
}
