using Fungus;
using UnityEngine;

namespace Godlotto.Interaction
{
    /// <summary>
    /// Kitchen 싱크/병 퍼즐 진행 bool의 C# 소스. Fungus 글로벌 bool에 미러합니다.
    /// GetBottle은 냉장고·복도 체인 소유 — Kitchen에서는 읽기만 합니다.
    /// </summary>
    public class KitchenPuzzleState : MonoBehaviour
    {
        [SerializeField] Flowchart flowchart;

        public bool HasBottle { get; private set; }
        public bool BottleClicked { get; private set; }
        public bool FaucetClicked { get; private set; }
        public bool BottleDragged { get; private set; }

        void Awake()
        {
            HydrateFromFungus();
        }

        public void HydrateFromFungus()
        {
            Flowchart fc = FlowchartLocator.Resolve(flowchart);
            if (fc == null)
                return;

            HasBottle = ReadBool(fc, FungusVariableKeys.GetBottle);
            BottleClicked = ReadBool(fc, FungusVariableKeys.BottleClicked);
            FaucetClicked = ReadBool(fc, FungusVariableKeys.FaucetClicked);
            BottleDragged = ReadBool(fc, FungusVariableKeys.BottleDragged);
        }

        /// <summary>ExecuteBlock 직전에 Fungus If 분기가 C# 상태와 일치하도록 미러합니다.</summary>
        public void MirrorSinkFlagsToFlowchart(Flowchart targetFlowchart)
        {
            Flowchart fc = FlowchartLocator.Resolve(targetFlowchart ?? flowchart);
            if (fc == null)
                return;

            MirrorBool(fc, FungusVariableKeys.BottleClicked, BottleClicked);
            MirrorBool(fc, FungusVariableKeys.FaucetClicked, FaucetClicked);
            MirrorBool(fc, FungusVariableKeys.BottleDragged, BottleDragged);
        }

        /// <summary>상호작용 블록 종료 시 Kitchen 소유 플래그를 갱신합니다.</summary>
        public void ApplyBlockCompletion(string blockName)
        {
            if (string.IsNullOrWhiteSpace(blockName))
                return;

            switch (blockName)
            {
                case KitchenSinkInteractionGate.BottleClickedBlockName:
                    if (HasBottle)
                        SetBottleClicked(true);
                    break;
                case KitchenSinkInteractionGate.FaucetBlockName:
                    SetFaucetClicked(true);
                    break;
                case KitchenSinkInteractionGate.BottleDraggedBlockName:
                    GameLog.Log("[KitchenPuzzleState] Bottle_Dragged completion applied");
                    SetBottleDragged(true);
                    SetBottleClicked(true);
                    RemoveBottleFromInventoryIfPresent();
                    break;
            }
        }

        public void SetBottleClicked(bool value)
        {
            BottleClicked = value;
            MirrorBool(FlowchartLocator.Resolve(flowchart), FungusVariableKeys.BottleClicked, value);
        }

        public void SetFaucetClicked(bool value)
        {
            FaucetClicked = value;
            MirrorBool(FlowchartLocator.Resolve(flowchart), FungusVariableKeys.FaucetClicked, value);
        }

        public void SetBottleDragged(bool value)
        {
            BottleDragged = value;
            MirrorBool(FlowchartLocator.Resolve(flowchart), FungusVariableKeys.BottleDragged, value);
        }

        static bool ReadBool(Flowchart fc, string key)
        {
            if (fc != null && fc.GetBooleanVariable(key))
                return true;

            return FlowchartLocator.GetFungusGlobalBoolean(key);
        }

        static void MirrorBool(Flowchart fc, string key, bool value)
        {
            if (fc == null || string.IsNullOrEmpty(key))
                return;

            fc.SetBooleanVariable(key, value);
        }

        internal void SetFlowchartForTests(Flowchart target) => flowchart = target;

        static void RemoveBottleFromInventoryIfPresent()
        {
            if (InventoryManager.instance == null)
                return;

            Item bottle = InventorySlot.draggedItem;
            if (bottle == null)
            {
                foreach (Item item in InventoryManager.instance.Items)
                {
                    if (item != null && item.itemName == "Bottle")
                    {
                        bottle = item;
                        break;
                    }
                }
            }

            if (bottle != null)
                InventoryManager.instance.RemoveItem(bottle);
        }

        internal void SetSinkFlagsForTests(bool hasBottle, bool bottleClicked, bool faucetClicked, bool bottleDragged)
        {
            HasBottle = hasBottle;
            BottleClicked = bottleClicked;
            FaucetClicked = faucetClicked;
            BottleDragged = bottleDragged;
        }
    }
}
