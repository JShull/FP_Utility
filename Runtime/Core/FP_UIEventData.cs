namespace FuzzPhyte.Utility
{
    using UnityEngine;
    using UnityEngine.EventSystems;
    public enum FP_UIEventType
    {
        PointerDown,
        PointerUp,
        Drag,
        PointerClick,
        // Add others if needed
    }

    public class FP_UIEventData<T> where T :class
    {
        public PointerEventData UnityPointerEventData { get; private set; }
        public T AdditionalData { get; private set; }
        public GameObject TargetObject { get; private set; }
        public GameObject SourceObject { get; private set; }
        public Vector3 WorldPosition { get; private set; }
        public FP_UIEventType EventType { get; private set; }

        public FP_UIEventData(PointerEventData pointerEventData, FP_UIEventType eventType, GameObject target, GameObject source, T additionalData = null)
        {
            TargetObject = target;
            EventType = eventType;
            UnityPointerEventData = pointerEventData;
            SourceObject = source;
            AdditionalData = additionalData;
            WorldPosition = pointerEventData.pointerCurrentRaycast.worldPosition;
        }
    }
}
