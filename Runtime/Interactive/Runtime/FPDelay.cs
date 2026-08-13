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

        protected WaitForSecondsRealtime delayWait;
        protected WaitForSecondsRealtime delayStart;

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
