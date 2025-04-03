namespace FuzzPhyte.Utility.FPSystem
{
    using System.Collections.Generic;
    using UnityEngine;
    using UnityEngine.EventSystems;

    [RequireComponent(typeof(RectTransform))]
    public class FP_UIInputListener<T> : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IDragHandler where T : class
    {
        [SerializeField]
        private bool isActive = true;

        public bool IsActive => isActive;
        public void ActivateListener() => isActive = true;
        public void DeactivateListener() => isActive = false;

        private readonly List<IFPUIEventListener<T>> listeners = new();

        protected T CurrentEngagedData;
        protected GameObject CurrentEngagedGameObject;
        public void RegisterListener(IFPUIEventListener<T> listener)
        {
            if (!listeners.Contains(listener))
                listeners.Add(listener);
        }

        public void UnregisterListener(IFPUIEventListener<T> listener)
        {
            if (listeners.Contains(listener))
                listeners.Remove(listener);
        }
        /// <summary>
        /// Set the current engaged data. This is used to pass data to the listeners.]
        /// E.g. a game manager might reference this and set this based on some UI actions
        /// </summary>
        /// <param name="engagedData">Active class</param>
        public virtual void SetCurrentEngagedData(T engagedData)
        {
            CurrentEngagedData = engagedData;
        }
        public virtual void SetCurrentEngagedGameObject(GameObject theEngagedObject)
        {
            CurrentEngagedGameObject = theEngagedObject;
        }
        /// <summary>
        /// Reset it as we need to
        /// </summary>
        public virtual void ResetCurrentEngagedData()
        {
            CurrentEngagedData = null;
        }
        public void OnPointerDown(PointerEventData eventData) => DispatchEvent(eventData,FP_UIEventType.PointerDown);
        public void OnPointerUp(PointerEventData eventData) => DispatchEvent(eventData,FP_UIEventType.PointerUp);
        public void OnDrag(PointerEventData eventData) => DispatchEvent(eventData,FP_UIEventType.Drag);
        //public void OnPointerClick(PointerEventData eventData) => DispatchEvent(eventData);

        protected virtual void DispatchEvent(PointerEventData unityEventData, FP_UIEventType theType)
        {
            
            if(!isActive)
            {
                return;
            }
            var fpEventData = new FP_UIEventData<T>(unityEventData, theType, CurrentEngagedGameObject,unityEventData.pointerPress, CurrentEngagedData);
            for (int i = 0; i < listeners.Count; i++) 
            {
                var listener = listeners[i];
                listener.OnUIEvent(fpEventData);
            }
        }
    }
}
