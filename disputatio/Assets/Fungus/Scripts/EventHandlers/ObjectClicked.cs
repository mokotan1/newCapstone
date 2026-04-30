// This code is part of the Fungus library (https://github.com/snozbot/fungus)
// It is released for free under the MIT open source license (https://github.com/snozbot/fungus/blob/master/LICENSE)

﻿using UnityEngine;
using System.Collections;

namespace Fungus 
{
    /// <summary>
    /// The block will execute when the user clicks or taps on the clickable object.
    /// </summary>
    [EventHandlerInfo("Sprite",
                      "Object Clicked",
                      "The block will execute when the user clicks or taps on the clickable object.")]
    [AddComponentMenu("")]
    public class ObjectClicked : EventHandler
    {   
        public class ObjectClickedEvent
        {
            public Clickable2D ClickableObject;
            public ObjectClickedEvent(Clickable2D clickableObject)
            {
                ClickableObject = clickableObject;
            }
        }

        [Tooltip("Object that the user can click or tap on")]
        [SerializeField] protected Clickable2D clickableObject;

        [Tooltip("Wait for a number of frames before executing the block.")]
        [SerializeField] protected int waitFrames = 1;

        protected EventDispatcher eventDispatcher;

        protected virtual void OnEnable()
        {
            eventDispatcher = FungusManager.Instance.EventDispatcher;

            eventDispatcher.AddListener<ObjectClickedEvent>(OnObjectClickedEvent);
        }

        protected virtual void OnDisable()
        {
            eventDispatcher.RemoveListener<ObjectClickedEvent>(OnObjectClickedEvent);

            eventDispatcher = null;
        }

        void OnObjectClickedEvent(ObjectClickedEvent evt)
        {
            OnObjectClicked(evt.ClickableObject);
        }

        /// <summary>
        /// Executing a block on the same frame that the object is clicked can cause
        /// input problems (e.g. auto completing Say Dialog text). A single frame delay 
        /// fixes the problem.
        /// </summary>
        protected virtual IEnumerator DoExecuteBlock(int numFrames)
        {
            if (numFrames == 0)
            {
                ExecuteBlock();
                yield break;
            }

            int count = Mathf.Max(waitFrames, 1);
            while (count > 0)
            {
                count--;
                yield return new WaitForEndOfFrame();
            }

            ExecuteBlock();
        }

        #region Public members

        /// <summary>
        /// 이 핸들러가 감시 중인 Clickable2D. InteractionLock 등에서 루트 블록 매칭용.
        /// </summary>
        public Clickable2D AssignedClickable => clickableObject;

        /// <summary>
        /// Called by the Clickable2D object when it is clicked.
        /// </summary>
        public virtual void OnObjectClicked(Clickable2D clicked)
        {
            if (!AssignedClickableMatches(this, clicked))
                return;

            InteractionLock.NotifyObjectClickedMatchedThisClick();
            StartCoroutine(DoExecuteBlock(waitFrames));
        }

        /// <summary>
        /// BlockSignals에서 루트 블록을 판별할 때 사용. 에디터 역직렬화로 <see cref="Block._EventHandler"/>가 베이스 타입처럼
        /// 다뤄지는 경우에도 같은 GameObject의 ObjectClicked 컴포넌트에서 찾습니다.
        /// </summary>
        public static bool HandlesClickableForBlock(Block block, Clickable2D source)
        {
            if (block == null || source == null)
            {
                return false;
            }

            if (block._EventHandler is ObjectClicked ocField && AssignedClickableMatches(ocField, source))
            {
                return true;
            }

            var handlers = block.GetComponents<ObjectClicked>();
            for (var i = 0; i < handlers.Length; i++)
            {
                var h = handlers[i];
                if (h != null && h.ParentBlock == block && AssignedClickableMatches(h, source))
                {
                    return true;
                }
            }

            return false;
        }

        static bool AssignedClickableMatches(ObjectClicked handler, Clickable2D candidate)
        {
            var assigned = handler.AssignedClickable;
            if (assigned == null)
            {
                return false;
            }

            return assigned == candidate || assigned.gameObject == candidate.gameObject;
        }

        public override string GetSummary()
        {
            if (clickableObject != null)
            {
                return clickableObject.name;
            }

            return "None";
        }

        #endregion
    }
}
