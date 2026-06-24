using Fungus;

namespace Godlotto.Interaction
{
    /// <summary>거울 퍼즐 성공 시 Fungus / RoomInteractionController 라우팅.</summary>
    public static class StudyRoomMirrorPuzzleSuccessRouter
    {
        internal static System.Action<StudyRoomPuzzleController, string> InteractionHandlerForTests;
        internal static System.Action<Flowchart, string, bool> SetBoolHandlerForTests;
        internal static System.Func<Flowchart, string, bool> ExecuteBlockHandlerForTests;

        public static void ApplySuccess(
            StudyRoomPuzzleController roomController,
            Flowchart flowchart,
            string interactionId,
            string fungusBlockName,
            string solvedBoolVariableName,
            bool setSolvedBoolBeforeSuccess,
            bool preferInteractionController)
        {
            Flowchart fc = FlowchartLocator.Resolve(flowchart);

            if (setSolvedBoolBeforeSuccess && fc != null && !string.IsNullOrWhiteSpace(solvedBoolVariableName))
                SetFlowchartBool(fc, solvedBoolVariableName, true);

            if (preferInteractionController
                && roomController != null
                && !string.IsNullOrWhiteSpace(interactionId))
            {
                if (InteractionHandlerForTests != null)
                {
                    InteractionHandlerForTests(roomController, interactionId);
                    return;
                }

                roomController.OnInteraction(interactionId);
                return;
            }

            if (fc == null || string.IsNullOrWhiteSpace(fungusBlockName))
                return;

            if (ExecuteBlockHandlerForTests != null)
            {
                ExecuteBlockHandlerForTests(fc, fungusBlockName);
                return;
            }

            FungusDialogueBridge.ExecuteBlockSafely(fc, fungusBlockName);
        }

        static void SetFlowchartBool(Flowchart targetFlowchart, string key, bool value)
        {
            if (SetBoolHandlerForTests != null)
            {
                SetBoolHandlerForTests(targetFlowchart, key, value);
                return;
            }

            targetFlowchart.SetBooleanVariable(key, value);
        }

        internal static void ResetForTests()
        {
            InteractionHandlerForTests = null;
            SetBoolHandlerForTests = null;
            ExecuteBlockHandlerForTests = null;
        }
    }
}
