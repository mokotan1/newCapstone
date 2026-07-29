#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using Godlotto.QA.Evidence;
using UnityEngine;

namespace Godlotto.QA.Gateway
{
    /// <summary>
    /// <see cref="QaCommandGateway"/>의 프로세스 전역 접근점(Task 10). Unity CLI 도구
    /// (<c>Assets/Editor/QA/QaUnityCliTools.cs</c>)와 런타임 개발자 패널(<c>QaDeveloperPanel</c>)이
    /// 동일한 Editor 프로세스 안에서 동작할 때 정확히 같은 리스/프로필/증거 상태를 관찰하도록
    /// 하나의 인스턴스를 공유합니다. 순수 development player 빌드(Editor 어셈블리가 존재하지
    /// 않는 빌드)에서는 <see cref="InstallFactory"/>가 호출되지 않으므로,
    /// <see cref="GetOrCreate"/>가 <see cref="DevelopmentQaEvidenceRecorder"/> 기반 기본
    /// 인스턴스를 지연 생성합니다(<c>persistentDataPath</c> 저장) — 어느 경로든 게이트웨이
    /// 없이 패널이 죽는 일은 없습니다.
    /// </summary>
    public static class QaCommandGatewayHost
    {
        private static readonly object sync = new object();
        private static Func<QaCommandGateway> factory;
        private static QaCommandGateway instance;

        /// <summary>
        /// Editor 전용 초기화 코드가 <c>[InitializeOnLoad]</c> 시점에 호출하여, Editor 저장소
        /// (<c>docs/qa/runs</c>)를 사용하는 팩토리를 설치합니다. 이미 인스턴스가 생성된 뒤에
        /// 호출하면 다음 <see cref="ResetForTests"/> 이전까지는 기존 인스턴스가 계속 사용됩니다
        /// (동일 프로세스 안에서 게이트웨이를 몰래 교체하지 않음).
        /// </summary>
        public static void InstallFactory(Func<QaCommandGateway> gatewayFactory)
        {
            lock (sync)
            {
                factory = gatewayFactory;
            }
        }

        /// <summary>공유 인스턴스를 반환합니다. 없으면 설치된 팩토리(또는 기본값)로 지연 생성합니다.</summary>
        public static QaCommandGateway GetOrCreate()
        {
            lock (sync)
            {
                if (instance != null)
                {
                    return instance;
                }

                try
                {
                    instance = factory != null ? factory() : CreateDefault();
                }
                catch (Exception ex)
                {
                    Debug.LogWarning(
                        "[QaCommandGatewayHost] Installed factory threw " + ex.GetType().Name +
                        "; falling back to the default development gateway.");
                    instance = CreateDefault();
                }

                return instance;
            }
        }

        private static QaCommandGateway CreateDefault()
        {
            var recorder = DevelopmentQaEvidenceRecorder.CreateDefault();
            return new QaCommandGateway(recorder, () => recorder.RunDirectoryPath);
        }

#if UNITY_INCLUDE_TESTS
        /// <summary>테스트 전용: 공유 인스턴스와 설치된 팩토리를 모두 지웁니다.</summary>
        internal static void ResetForTests()
        {
            lock (sync)
            {
                try
                {
                    instance?.Dispose();
                }
                catch (Exception ex)
                {
                    Debug.LogWarning("[QaCommandGatewayHost] Dispose during ResetForTests threw: " + ex.GetType().Name);
                }

                instance = null;
                factory = null;
            }
        }

        /// <summary>테스트 전용: 페이크/스텁 게이트웨이를 직접 주입합니다.</summary>
        internal static void InstallInstanceForTests(QaCommandGateway gateway)
        {
            lock (sync)
            {
                instance = gateway;
            }
        }
#endif
    }
}
#endif
