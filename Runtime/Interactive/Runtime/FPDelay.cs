namespace FuzzPhyte.Utility.Interactive
{
    using UnityEngine;
    using System.Collections;
    using UnityEngine.Events;
    public class FPDelay : MonoBehaviour
    {
        public bool DelayAfterAwake;
        public float DelayAwakeSeconds = 1f;
        public UnityEvent AwakeEventToInvoke;
        [Space]
        public bool DelayAfterStart;
        public float DelayStartSeconds = 1f;
        public UnityEvent StartEventToInvoke;
        [Space]
        public bool DelayEventAction;
        public float DelayEventActionSeconds = 1f;
        public UnityEvent DelayEventActions;
        [Space]
        protected WaitForSecondsRealtime delayWait;
        protected WaitForSecondsRealtime delayStart;
        protected WaitForSecondsRealtime delayBeforeEvent;

        protected void Awake()
        {
            if (DelayAfterAwake)
            {
                delayWait = new WaitForSecondsRealtime(DelayAwakeSeconds);
                StartCoroutine(DelayAwakeEventLaunch());
            }
        }
        protected void Start()
        {
            if (DelayAfterStart) {
                delayStart = new WaitForSecondsRealtime(DelayStartSeconds);
                StartCoroutine(DelayStartEventLaunch());
            }
            if (DelayEventAction)
            {
                delayBeforeEvent = new WaitForSecondsRealtime(DelayEventActionSeconds);
            }
        }
        /// <summary>
        /// Public Method to Call
        /// </summary>
        public void ActivateTimerEvent()
        {
            if (!DelayEventAction)
            {
                return;
            }
            StartCoroutine(InternalDelayEventAction());
        }
        IEnumerator InternalDelayEventAction()
        {
            yield return delayBeforeEvent;
            DelayEventActions?.Invoke();
        }
        IEnumerator DelayAwakeEventLaunch()
        {
            yield return delayWait;
            AwakeEventToInvoke.Invoke();
        }
        IEnumerator DelayStartEventLaunch()
        {
            yield return delayStart;
            StartEventToInvoke.Invoke();
        }

    }
}
