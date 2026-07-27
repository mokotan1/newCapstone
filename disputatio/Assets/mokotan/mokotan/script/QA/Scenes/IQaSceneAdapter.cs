#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections.Generic;

namespace Godlotto.QA.Scenes
{
    /// <summary>
    /// <see cref="QaScenePresetResult"/>가 나타낼 수 있는 명시적 결과 코드.
    /// </summary>
    public enum QaScenePresetResultCode
    {
        Success,

        /// <summary>요청한 프리셋 ID가 이 어댑터에 존재하지 않습니다.</summary>
        UnknownPreset,

        /// <summary>프리셋은 알지만 적용 중 실패했습니다. 자세한 사유는 <see cref="QaScenePresetResult.Message"/>.</summary>
        Failed
    }

    /// <summary>
    /// <see cref="IQaSceneAdapter.ApplyPreset"/> 호출 한 건의 불변 결과.
    /// </summary>
    public sealed class QaScenePresetResult
    {
        public QaScenePresetResultCode Code { get; }

        public string Message { get; }

        private QaScenePresetResult(QaScenePresetResultCode code, string message)
        {
            Code = code;
            Message = message ?? string.Empty;
        }

        public bool IsSuccess
        {
            get { return Code == QaScenePresetResultCode.Success; }
        }

        public static QaScenePresetResult Success(string message = null)
        {
            return new QaScenePresetResult(QaScenePresetResultCode.Success, message);
        }

        public static QaScenePresetResult UnknownPreset(string presetId)
        {
            return new QaScenePresetResult(
                QaScenePresetResultCode.UnknownPreset,
                "Preset '" + presetId + "' is not registered by this scene adapter.");
        }

        public static QaScenePresetResult Failed(string message)
        {
            return new QaScenePresetResult(QaScenePresetResultCode.Failed, message);
        }
    }

    /// <summary>
    /// 하나의 지원 씬을 소유하는 QA 어댑터의 최소 계약(디자인 문서 §4.4). 구현체는 기존 도메인
    /// 컨트롤러·Fungus 블록·인벤토리 서비스·UI 컴포넌트를 그대로 호출하며, 게임플레이 규칙을
    /// 중복 구현하지 않습니다. 실제 상호작용 실행(클릭/드래그/키 입력)은 이 인터페이스의
    /// 책임이 아니며, <c>IQaInputDriver</c>(Task 7)와 <see cref="QaSceneRegistry"/>가 노출하는
    /// 안정적 대상 ID를 통해 별도로 조율됩니다. 이 태스크(5)에서는 씬 이름 소유, 안정적
    /// 대상 ID 목록, 최소/스텁 프리셋 적용, 스냅샷 캡처만 제공합니다.
    /// </summary>
    public interface IQaSceneAdapter
    {
        /// <summary>이 어댑터가 소유하는 씬 이름. <see cref="QaSceneRegistry"/> 등록 키입니다.</summary>
        string SceneName { get; }

        /// <summary>이 어댑터가 노출하는 안정적 대상 ID 전체 목록. 절대 null이 아닙니다.</summary>
        IReadOnlyCollection<QaTargetId> TargetIds { get; }

        /// <summary>이 어댑터가 적용할 수 있는 프리셋 이름 전체 목록. 절대 null이 아닙니다.</summary>
        IReadOnlyCollection<string> PresetIds { get; }

        /// <summary>
        /// 이름으로 지정한 프리셋을 적용합니다. 아직 구체 어댑터가 없는 이 태스크 시점에는
        /// 스텁/최소 구현으로 충분합니다(디자인 세부 사항은 Task 12에서 완성).
        /// </summary>
        QaScenePresetResult ApplyPreset(string presetId);

        /// <summary>현재 씬 상태의 얕은 진단 스냅샷을 캡처합니다.</summary>
        QaSceneSnapshot CaptureSnapshot();
    }
}
#endif
