using Fungus;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 화분 확대 패널 배경: Fungus <c>GetBottle</c>이 아직 false이면 <see cref="spriteWhenBottleVisible"/>(화분 확대),
/// 병을 획득해 true가 되면 <see cref="spriteWhenBottleHidden"/>(Bottle_Off)로 맞춥니다.
/// </summary>
[DisallowMultipleComponent]
public class PotPanelBottleBackgroundSync : MonoBehaviour
{
    [SerializeField] private Image potPanelImage;
    [Tooltip("비우면 FlowchartLocator(Variablemanager)를 사용합니다.")]
    [SerializeField] private Flowchart flowchartOverride;
    [SerializeField] private string fungusBooleanKey = "GetBottle";
    [Tooltip("Variablemanager에 키가 없을 때 Fungus GlobalVariables도 조회합니다.")]
    [SerializeField] private bool checkGlobalVariables = true;

    [Header("스프라이트")]
    [Tooltip("GetBottle이 false일 때(병 미획득) 배경")]
    [SerializeField] private Sprite spriteWhenBottleVisible;
    [Tooltip("GetBottle이 true일 때(병 획득 후) 배경")]
    [SerializeField] private Sprite spriteWhenBottleHidden;

    private bool _initialized;
    private bool _lastGetBottle;

    private void Awake()
    {
        if (potPanelImage == null)
            potPanelImage = GetComponent<Image>();
    }

    private void OnEnable()
    {
        _initialized = false;
    }

    private void LateUpdate()
    {
        if (potPanelImage == null)
            return;

        bool getBottle = IsGetBottleTrue();
        if (_initialized && getBottle == _lastGetBottle)
            return;

        _lastGetBottle = getBottle;
        _initialized = true;

        Sprite chosen = ChoosePotPanelSprite(getBottle, spriteWhenBottleVisible, spriteWhenBottleHidden);
        if (chosen != null)
            potPanelImage.sprite = chosen;
    }

    private bool IsGetBottleTrue()
    {
        Flowchart fc = FlowchartLocator.Resolve(flowchartOverride);
        if (fc != null && !string.IsNullOrEmpty(fungusBooleanKey) && fc.GetBooleanVariable(fungusBooleanKey))
            return true;

        if (checkGlobalVariables && !string.IsNullOrEmpty(fungusBooleanKey)
            && FlowchartLocator.GetFungusGlobalBoolean(fungusBooleanKey))
            return true;

        return false;
    }

    /// <summary>
    /// <paramref name="getBottle"/>이 false면 화분 확대, true면 Bottle_Off 스프라이트.
    /// </summary>
    public static Sprite ChoosePotPanelSprite(bool getBottle, Sprite whenGetBottleFalse, Sprite whenGetBottleTrue)
    {
        return getBottle ? whenGetBottleTrue : whenGetBottleFalse;
    }
}
