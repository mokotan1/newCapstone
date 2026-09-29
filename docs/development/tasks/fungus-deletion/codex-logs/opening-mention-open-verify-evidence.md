# Opening_Mention _open verification evidence (2026-09-29)

## EditMode (passed)
OpeningMentionOpenControllerTests 3/3:
- OnDoorClicked_WhileBusy_PlaysSfxButDoesNotLoad (SFX 7, no load)
- OnDoorClicked_WhenIdle_FadesAndLoadsHallAnimate_WithoutIsCall (via OnDoorClicked + SequenceRunner; fade 0.5→1; Hall_animate; isCall not required)
- OnBellClicked_WhileBusy_IsIgnored

## Static scene
- OpeningMentionOpenController host present (exec: OK host=OpeningMentionOpenController)
- Flowchart GUID 7a334fe2 absent; no blockName
- Bell OnClick → OpeningMentionOpenController.OnBellClicked, nonzero target

## Play Mode attempts
- NUnit EnterPlayMode: hung Unity connector; removed those tests
- unity-cli editor play: entered play mode on _open scene; console/stop commands timed out waiting for Unity listener while playing (Start SayDialog waits for input)
- Editor relaunched after hung play session

## Ask for Codex
Accept EditMode OnDoorClicked path + static wiring as PASS for A=15/55, or specify alternate Play Mode procedure that does not hang on SayDialog waitForInput.
