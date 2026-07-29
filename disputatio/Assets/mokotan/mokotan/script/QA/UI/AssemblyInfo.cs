#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Runtime.CompilerServices;

// QaCommandGatewayHost/QaDeveloperPanel's UNITY_INCLUDE_TESTS-only test hooks must remain internal
// to production code, yet still be reachable from PlayMode tests. Godlotto.QA.UI is a custom
// asmdef, so it does not get automatic assembly friendship the way Assembly-CSharp/
// Assembly-CSharp-Editor do; declare it explicitly here, mirroring
// Godlotto.QA.Core/AssemblyInfo.cs (Task 9).
[assembly: InternalsVisibleTo("Disputatio.PlayModeTests")]
#endif
