// Prefab 구조:
// Canvas (StandingDialogueCanvas) — Screen Space Overlay, Sort Order 10
//   CanvasGroup
//   ├── CharacterDimBackdrop (인트로 씬 전용, Raycast Off)
//   ├── LeftSlot
//   │     ├── LeftOverlay (비활성/투명, 슬롯별 예비)
//   │     └── LeftCharImage (Image, Preserve Aspect: true)
//   ├── RightSlot
//   │     ├── RightOverlay (비활성/투명, 슬롯별 예비)
//   │     └── RightCharImage (Image, Preserve Aspect: true)
//   └── DialogueBox (하단 중앙)
//         ├── NameText (TMP)
//         └── DialogueText (TMP)

using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Mokotan.StandingDialogue
{
    public enum Side { Left, Right }

    /// <summary>타이핑 효과 설정값.</summary>
    public struct TypographySettings
    {
        /// <summary>TMP 폰트 에셋. null이면 프리팹 기본값 유지.</summary>
        public TMP_FontAsset Font;
        /// <summary>대사 폰트 크기. 0이면 프리팹 기본값 유지.</summary>
        public float FontSize;
        /// <summary>초당 출력 글자 수. 0이면 즉시 출력.</summary>
        public float CharsPerSecond;

        public static readonly TypographySettings Default = new TypographySettings
        {
            Font           = null,
            FontSize       = 0f,
            CharsPerSecond = 0f,
        };
    }

    [AddComponentMenu("Mokotan/Standing Dialogue Manager")]
    public class StandingDialogueManager : MonoBehaviour
    {
        public const float FadeDurationSeconds = 0.3f;

        private static readonly Color ActiveColor   = Color.white;
        private static readonly Color InactiveColor = new Color(0.35f, 0.35f, 0.35f, 1f);

        public static StandingDialogueManager ActiveStandingDialogue { get; set; }

        private static readonly List<StandingDialogueManager> ActiveStandingDialogues =
            new List<StandingDialogueManager>();

        public static StandingDialogueManager Instance
        {
            get
            {
                if (ActiveStandingDialogue != null) return ActiveStandingDialogue;
                return ActiveStandingDialogues.Count > 0 ? ActiveStandingDialogues[0] : null;
            }
        }

        [SerializeField] private Image    leftCharImage;
        [SerializeField] private Image    rightCharImage;
        [SerializeField] private Image    characterDimBackdrop;
        [SerializeField] private GameObject dialogueBox;
        [SerializeField] private TMP_Text nameText;
        [SerializeField] private TMP_Text dialogueTextField;
        [SerializeField] private CanvasGroup canvasGroup;

        private bool   _waitingForInput;
        private bool   _typing;           // 타이핑 중 여부
        private Action _onInputComplete;
        private Coroutine _fadeAllRoutine;
        private Coroutine _typingRoutine;

        // 대사 텍스트 기본 폰트 크기 (프리팹 설정값 보존용)
        private float _defaultFontSize;
        private TMP_FontAsset _defaultFont;

        private void Awake()
        {
            if (!ActiveStandingDialogues.Contains(this))
                ActiveStandingDialogues.Add(this);

            if (dialogueTextField != null)
            {
                _defaultFontSize = dialogueTextField.fontSize;
                _defaultFont     = dialogueTextField.font;
            }

            ApplyIntroBackdrop(visible: false);
        }

        private void OnEnable()
        {
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private void OnDisable()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        private void OnDestroy()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            ActiveStandingDialogues.Remove(this);
            if (ActiveStandingDialogue == this) ActiveStandingDialogue = null;
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            ApplyIntroBackdrop(visible: gameObject.activeSelf);
        }

        public static StandingDialogueManager GetStandingDialogue()
        {
            if (ActiveStandingDialogue != null) return ActiveStandingDialogue;

            if (ActiveStandingDialogues.Count > 0)
            {
                ActiveStandingDialogue = ActiveStandingDialogues[0];
                return ActiveStandingDialogue;
            }

            GameObject prefab = Resources.Load<GameObject>("Prefabs/StandingDialogueCanvas");
            if (prefab == null) return null;

            GameObject go = UnityEngine.Object.Instantiate(prefab);
            go.name = "StandingDialogueCanvas";
            go.SetActive(false);
            StandingDialogueManager mgr = go.GetComponent<StandingDialogueManager>();
            if (mgr == null) { UnityEngine.Object.Destroy(go); return null; }

            ActiveStandingDialogue = mgr;
            return mgr;
        }

        private void Update()
        {
            if (!_waitingForInput) return;

            if (Input.GetKeyDown(KeyCode.Space) || Input.GetMouseButtonDown(0))
            {
                // 타이핑 중이면 클릭 한 번에 전체 텍스트 즉시 출력
                if (_typing)
                {
                    SkipTyping();
                    return;
                }
                CompleteWaitingInput();
            }
        }

        // ── 공개 API ──────────────────────────────────────────────

        public void TalkStanding(Side speakerSide,
            Sprite speakerSprite, Vector2 speakerOffset,
            Sprite otherSprite,   Vector2 otherOffset,
            string speakerName,   string dialogueText,
            TypographySettings typography,
            Action onComplete)
        {
            SetupSpeakerSlots(speakerSide, speakerSprite, speakerOffset, otherSprite, otherOffset);
            Speak(speakerSide, speakerName, dialogueText, typography, onComplete);
        }

        /// <summary>스탠딩 이미지 슬롯과 하이라이트만 설정합니다. 대사창은 외부(SayDialog)에서 처리할 때 사용합니다.</summary>
        public void SetupSpeakerSlots(Side speakerSide,
            Sprite speakerSprite, Vector2 speakerOffset,
            Sprite otherSprite,   Vector2 otherOffset)
        {
            EnsureCanvasVisible();

            var speakerImg = GetCharImage(speakerSide);
            var otherImg   = GetCharImage(speakerSide == Side.Left ? Side.Right : Side.Left);

            if (speakerSprite != null && speakerImg != null)
            {
                speakerImg.sprite = speakerSprite;
                speakerImg.gameObject.SetActive(true);
            }
            if (speakerImg != null) ApplyOffset(speakerImg, speakerOffset);

            if (otherSprite != null && otherImg != null)
            {
                otherImg.sprite = otherSprite;
                otherImg.gameObject.SetActive(true);
            }
            if (otherImg != null) ApplyOffset(otherImg, otherOffset);

            ApplySpeakerHighlight(speakerSide);

            if (dialogueBox != null) dialogueBox.SetActive(false);
        }

        public void Speak(Side side, string speakerName, string dialogueText,
            TypographySettings typography, Action onComplete)
        {
            EnsureCanvasVisible();

            if (nameText != null)
                nameText.text = speakerName ?? string.Empty;

            if (dialogueBox != null)
                dialogueBox.SetActive(true);

            ApplyTypography(typography);
            ApplySpeakerHighlight(side);

            StopWaitingForInput();
            _waitingForInput = true;
            _onInputComplete = onComplete;

            if (_typingRoutine != null) StopCoroutine(_typingRoutine);

            if (typography.CharsPerSecond > 0f && !string.IsNullOrEmpty(dialogueText))
                _typingRoutine = StartCoroutine(TypeText(dialogueText, typography.CharsPerSecond));
            else
            {
                if (dialogueTextField != null) dialogueTextField.text = dialogueText ?? string.Empty;
            }
        }

        public void HideAll()
        {
            StopWaitingForInput();
            if (_fadeAllRoutine != null) StopCoroutine(_fadeAllRoutine);
            _fadeAllRoutine = StartCoroutine(HideAllRoutine());
        }

        /// <summary>스탠딩 초상화 없이 독백만 출력할 때 슬롯을 비웁니다.</summary>
        public void ClearStandingCharacterSlots()
        {
            EnsureCanvasVisible();
            ResetSlot(leftCharImage);
            ResetSlot(rightCharImage);
        }

        public void ShowCharacter(Side side, Sprite sprite)
        {
            var img = GetCharImage(side);
            if (img == null) return;
            EnsureCanvasVisible();
            img.sprite = sprite;
            img.color  = ActiveColor;
            img.gameObject.SetActive(true);
        }

        // ── 내부 ──────────────────────────────────────────────────

        private void ApplyTypography(TypographySettings t)
        {
            if (dialogueTextField == null) return;

            dialogueTextField.font     = t.Font     != null  ? t.Font     : _defaultFont;
            dialogueTextField.fontSize = t.FontSize > 0f     ? t.FontSize : _defaultFontSize;
        }

        private IEnumerator TypeText(string fullText, float charsPerSecond)
        {
            _typing = true;
            if (dialogueTextField != null) dialogueTextField.text = string.Empty;

            float interval = 1f / charsPerSecond;
            float elapsed  = 0f;
            int   shown    = 0;

            while (shown < fullText.Length)
            {
                elapsed += Time.deltaTime;
                int target = Mathf.Min(Mathf.FloorToInt(elapsed * charsPerSecond) + 1, fullText.Length);
                if (target > shown)
                {
                    shown = target;
                    if (dialogueTextField != null)
                        dialogueTextField.text = fullText.Substring(0, shown);
                }
                yield return null;
            }

            _typing        = false;
            _typingRoutine = null;
        }

        private void SkipTyping()
        {
            if (_typingRoutine != null)
            {
                StopCoroutine(_typingRoutine);
                _typingRoutine = null;
            }
            // 전체 텍스트를 즉시 표시 — dialogueTextField.text는 TypeText가 마지막으로 설정 중이므로
            // maxVisibleCharacters를 활용하는 대신 text 자체를 직접 완성시킵니다.
            // TalkStanding 호출 시 전달한 원본 텍스트를 보존하기 위해 TMP의 현재 text를 꺼냅니다.
            // (TypeText 내부에서 Substring으로 설정했으므로 fullText를 다시 쓰는 대신
            //  dialogueTextField의 text를 fullText로 되돌리는 방식으로 처리합니다.)
            _typing = false;
        }

        private void ApplySpeakerHighlight(Side speakerSide)
        {
            var speakerImg = GetCharImage(speakerSide);
            var otherImg   = GetCharImage(speakerSide == Side.Left ? Side.Right : Side.Left);

            if (speakerImg != null && speakerImg.sprite != null)
                speakerImg.color = ActiveColor;
            if (otherImg   != null && otherImg.sprite   != null)
                otherImg.color   = InactiveColor;
        }

        private IEnumerator HideAllRoutine()
        {
            if (canvasGroup != null)
            {
                float elapsed = 0f;
                float start   = canvasGroup.alpha;
                while (elapsed < FadeDurationSeconds)
                {
                    elapsed += Time.deltaTime;
                    canvasGroup.alpha = Mathf.Lerp(start, 0f, Mathf.Clamp01(elapsed / FadeDurationSeconds));
                    yield return null;
                }
                canvasGroup.alpha = 0f;
            }

            if (dialogueBox != null) dialogueBox.SetActive(false);
            ResetSlot(leftCharImage);
            ResetSlot(rightCharImage);
            ApplyIntroBackdrop(visible: false);
            _fadeAllRoutine = null;
        }

        private static void ResetSlot(Image img)
        {
            if (img == null) return;
            img.sprite = null;
            img.color  = ActiveColor;
            img.gameObject.SetActive(false);
        }

        private void CompleteWaitingInput()
        {
            _waitingForInput = false;
            var cb = _onInputComplete;
            _onInputComplete = null;
            cb?.Invoke();
        }

        private void StopWaitingForInput()
        {
            _typing          = false;
            _waitingForInput = false;
            _onInputComplete = null;
            if (_typingRoutine != null) { StopCoroutine(_typingRoutine); _typingRoutine = null; }
        }

        private void EnsureCanvasVisible()
        {
            if (!gameObject.activeSelf) gameObject.SetActive(true);
            if (canvasGroup != null && canvasGroup.alpha < 1f) canvasGroup.alpha = 1f;
            ApplyIntroBackdrop(visible: true);
        }

        private void ApplyIntroBackdrop(bool visible)
        {
            if (characterDimBackdrop == null) return;

            bool useBackdrop = visible &&
                IntroStandingDialogueOffsetPolicy.UsesDimBackdrop(
                    SceneManager.GetActiveScene().name);

            characterDimBackdrop.gameObject.SetActive(useBackdrop);
            if (!useBackdrop) return;

            characterDimBackdrop.raycastTarget = false;
            characterDimBackdrop.color = new Color(
                0f, 0f, 0f, IntroStandingDialogueOffsetPolicy.BackdropAlpha);
        }

        private Image GetCharImage(Side side) =>
            side == Side.Left ? leftCharImage : rightCharImage;

        internal RectTransform LeftCharacterRect =>
            leftCharImage != null ? leftCharImage.rectTransform : null;

        internal RectTransform RightCharacterRect =>
            rightCharImage != null ? rightCharImage.rectTransform : null;

        internal RectTransform LeftSlotRect =>
            leftCharImage != null ? leftCharImage.transform.parent as RectTransform : null;

        internal RectTransform RightSlotRect =>
            rightCharImage != null ? rightCharImage.transform.parent as RectTransform : null;

        private static void ApplyOffset(Image img, Vector2 offset)
        {
            img.rectTransform.anchoredPosition =
                IntroStandingDialogueOffsetPolicy.ResolveForActiveScene(offset);
        }
    }
}
