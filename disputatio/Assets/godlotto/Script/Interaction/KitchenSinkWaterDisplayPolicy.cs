namespace Godlotto.Interaction
{
    /// <summary>
    /// Kitchen Sink_Pannel 물/수도꼭지 표시 레이아웃·정렬 상수.
    /// EditMode 씬 텍스트 검증과 런타임 정렬에 공유합니다.
    /// </summary>
    public static class KitchenSinkWaterDisplayPolicy
    {
        public const string SinkPanelName = "Sink_Pannel";
        public const string BackgroundChildName = "SinkBackground";
        public const string OverlayChildName = "SinkWaterOverlay";
        public const string FaucetClosedName = "Faucet";
        public const string FaucetOpenName = "FaucetOpen";
        public const string WaterRootName = "Water";

        /// <summary>Kitchen.unity Sink_Pannel Faucet 버튼 GameObject fileID (씬 YAML 검증용).</summary>
        public const string FaucetButtonSceneFileId = "614617303";

        /// <summary>Kitchen.unity Flowchart GameObject fileID — FaucetKeyReleaseController 호스트.</summary>
        public const string KitchenFlowchartSceneFileId = "290853875";

        /// <summary>Kitchen.unity Water 프리팹 인스턴스 루트 fileID (씬 YAML 검증용).</summary>
        public const string WaterRootSceneFileId = "581171985";

        public static readonly string[] SinkWaterDisplayFungusBlockNames =
        {
            KitchenSinkInteractionGate.FaucetBlockName,
            KitchenSinkInteractionGate.FilledBottleBlockName,
            KitchenSinkInteractionGate.BottleDraggedBlockName,
        };

        public static readonly string[] SinkWaterDisplayObjectNames =
        {
            WaterRootName,
            FaucetClosedName,
            FaucetOpenName,
        };

        /// <summary>루트 Kitchen Canvas(sorting order 2)보다 위에 물 FX를 그립니다.</summary>
        public const int OverlaySortingOrder = 12;
    }
}
