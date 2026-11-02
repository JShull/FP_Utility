// Copyright (c) 2026 John B. Shull. GNU GPLv3-or-later. See LICENSE.md.
namespace FuzzPhyte.Utility.Tests
{
    using System;
    using System.Collections.Generic;
    using UnityEngine;
    using UnityEngine.Events;
    public sealed class FPSceneReferenceTestOwner : MonoBehaviour
    {
        public UnityEngine.Object Target;
        public int ConfigurationValue;
        public BoxCollider ColliderTarget;
        public UnityEngine.Object m_ConnectedBody;
        [HideInInspector] public UnityEngine.Object HiddenTarget;
        public List<UnityEngine.Object> Items = new List<UnityEngine.Object>();
        public UnityEvent Callback = new UnityEvent();
        public UnityEvent<UnityEngine.Object> ObjectCallback = new UnityEvent<UnityEngine.Object>();
        [SerializeReference] public FPSceneReferenceTestNested Nested;
        public void Receive() { }
        public void ReceiveObject(UnityEngine.Object value) { }
    }
    [Serializable]
    public sealed class FPSceneReferenceTestNested { public UnityEngine.Object Target; }
}
