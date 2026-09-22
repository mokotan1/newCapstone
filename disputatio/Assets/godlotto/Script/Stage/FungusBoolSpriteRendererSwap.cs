using Fungus;
using UnityEngine;

/// <summary>
/// Fungus bool(예: UsedStudyKey)이 true가 되면 <see cref="SpriteRenderer"/>의 스프라이트를 교체합니다.
/// 씬 재진입 시 이미 true인 경우와, 같은 씬에서 열쇠 사용 직후 모두 처리합니다.
/// </summary>
public class FungusBoolSpriteRendererSwap : MonoBehaviour
{
    [SerializeField] private SpriteRenderer spriteRenderer;
    [Tooltip("비우면 FlowchartLocator(Variablemanager)를 사용합니다.")]
    [SerializeField] private Flowchart flowchartOverride;
    [SerializeField] private string fungusBooleanKey = FungusVariableKeys.UsedStudyKey;
    [SerializeField] private Sprite spriteWhenTrue;
    [Tooltip("Variablemanager에 키가 없을 때 Fungus GlobalVariables도 조회합니다.")]
    [SerializeField] private bool checkGlobalVariables = true;

    [Header("스프라이트 교체 시 월드 크기 유지")]
    [Tooltip("켜면 교체 전·후 스프라이트가 같은 월드 크기(너비·높이)를 갖도록 Transform 스케일을 조정합니다.")]
    [SerializeField] private bool preserveWorldSizeOnSwap;
    [Tooltip("(0,0)이면 Awake 시점의 닫힌 문 스프라이트 bounds를 기준으로 합니다. 값이 있으면 해당 크기(월드 단위)에 맞춥니다.")]
    [SerializeField] private Vector2 referenceWorldSizeOverride;

    [Header("스프라이트 교체 시 색 (인스펙터에서 조정)")]
    [Tooltip("켜면 Fungus 조건이 참이 되어 스프라이트가 교체될 때 아래 색을 SpriteRenderer에 적용합니다. 끄면 씬에 설정한 원래 색을 유지합니다.")]
    [SerializeField] private bool tintWhenSwappedSpriteApplied;
    [Tooltip("스프라이트가 교체될 때 적용할 색입니다. RGB를 모두 같은 값으로 두면 흑백 톤만 조절할 수 있습니다. (예: 0.55, 0.55, 0.55)")]
    [SerializeField] private Color colorWhenSwapped = new Color(0.55f, 0.55f, 0.55f, 1f);
    [Tooltip("켜면 위 색의 알파 대신, 이 오브젝트가 처음 로드됐을 때의 알파를 유지합니다.")]
    [SerializeField] private bool keepInitialAlphaWhenTinting = true;

    private bool _unlockedApplied;
    private Vector3 _baselineWorldSize;
    private Vector3 _initialLocalScale;
    private Color _initialColor;

    private void Awake()
    {
        if (spriteRenderer == null)
            spriteRenderer = GetComponent<SpriteRenderer>();

        if (spriteRenderer != null)
            _initialColor = spriteRenderer.color;

        _initialLocalScale = transform.localScale;

        if (preserveWorldSizeOnSwap && spriteRenderer != null)
        {
            _baselineWorldSize = referenceWorldSizeOverride.sqrMagnitude > 0.0001f
                ? new Vector3(referenceWorldSizeOverride.x, referenceWorldSizeOverride.y, 1f)
                : spriteRenderer.bounds.size;
        }
    }

    private void Start()
    {
        if (spriteRenderer == null)
            return;

        if (preserveWorldSizeOnSwap)
        {
            if (IsBoolTrue() && spriteWhenTrue != null)
            {
                spriteRenderer.sprite = spriteWhenTrue;
                ApplySwappedSpriteTintIfConfigured();
                _unlockedApplied = true;
            }

            if (spriteRenderer.sprite != null)
                ApplyScaleToMatchBaseline(spriteRenderer.sprite);
        }
        else
        {
            ApplyUnlockSpriteIfNeeded();
        }

        enabled = !_unlockedApplied && spriteWhenTrue != null;
    }

    private void Update()
    {
        if (_unlockedApplied)
        {
            enabled = false;
            return;
        }

        ApplyUnlockSpriteIfNeeded();
        if (_unlockedApplied)
            enabled = false;
    }

    /// <summary>UnityEvent 등에서 즉시 반영할 때 호출합니다.</summary>
    public void RefreshFromFungus()
    {
        ApplyUnlockSpriteIfNeeded();
    }

    private void ApplyUnlockSpriteIfNeeded()
    {
        if (spriteWhenTrue == null || spriteRenderer == null)
            return;

        if (!IsBoolTrue())
            return;

        spriteRenderer.sprite = spriteWhenTrue;
        if (preserveWorldSizeOnSwap)
            ApplyScaleToMatchBaseline(spriteWhenTrue);
        ApplySwappedSpriteTintIfConfigured();
        _unlockedApplied = true;
    }

    private void ApplyScaleToMatchBaseline(Sprite s)
    {
        if (s == null)
            return;

        Vector3 b = s.bounds.size;
        if (b.x < 1e-6f || b.y < 1e-6f)
            return;

        transform.localScale = new Vector3(
            _initialLocalScale.x * (_baselineWorldSize.x / b.x),
            _initialLocalScale.y * (_baselineWorldSize.y / b.y),
            _initialLocalScale.z);
    }

    private void ApplySwappedSpriteTintIfConfigured()
    {
        if (!tintWhenSwappedSpriteApplied || spriteRenderer == null)
            return;

        Color c = colorWhenSwapped;
        if (keepInitialAlphaWhenTinting)
            c.a = _initialColor.a;
        spriteRenderer.color = c;
    }

    private bool IsBoolTrue()
    {
        Flowchart fc = FlowchartLocator.Resolve(flowchartOverride);
        if (fc != null && fc.GetBooleanVariable(fungusBooleanKey))
            return true;

        if (checkGlobalVariables && FlowchartLocator.GetFungusGlobalBoolean(fungusBooleanKey))
            return true;

        return false;
    }
}
