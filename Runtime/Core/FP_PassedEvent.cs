using UnityEngine;
using UnityEngine.Events;

namespace FuzzPhyte.Utility
{
    public class FP_PassedEvent : MonoBehaviour
    {
        public UnityEvent ThePassedEvent;

        public void PassMyEvent()
        {
            ThePassedEvent.Invoke();
        }
    }
}
