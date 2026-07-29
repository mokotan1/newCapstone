#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Runtime.CompilerServices;

// QaDriverCore.FaultInjectorForTests (and any other UNITY_INCLUDE_TESTS-only internal test hooks
// added later) must remain internal to production code, yet still be reachable from EditMode
// tests. Predefined assemblies (Assembly-CSharp / Assembly-CSharp-Editor) get this friendship for
// free from Unity; custom asmdefs like Godlotto.QA.Core do not, so it must be declared explicitly
// here now that Task 9 moved this assembly out of Assembly-CSharp.
[assembly: InternalsVisibleTo("Assembly-CSharp-Editor")]
#endif
