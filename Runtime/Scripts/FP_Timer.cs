using System;
using UnityEngine;

namespace FuzzPhyte.Utility
{
    /// <summary>
    /// Singleton pattern
    /// Uses a simple priority queue method to manage timers in a scene
    /// </summary>
    public class FP_Timer : MonoBehaviour
    {
        private static FP_Timer _instance;
        public static FP_Timer CCTimer { get { return _instance; } }

        public void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(this.gameObject);
            }
            else
            {
                _instance = this;
            }
        }
        private class TimerData : IComparable<TimerData>
        {
            public float time;
            public Action onFinish;

            public int CompareTo(TimerData other)
            {
                return time.CompareTo(other.time);
            }
        }

        private PriorityQueue<TimerData> timers = new PriorityQueue<TimerData>();

        private void Update()
        {
            while (timers.Count > 0 && timers.Peek().time <= Time.time)
            {
                TimerData timerData = timers.Dequeue();
                Debug.LogWarning($"Timer Finished with Action: {timerData.onFinish.Method.Name}");
                timerData.onFinish();
            }
        }
        /// <summary>
        /// time in seconds from sending
        /// </summary>
        /// <param name="time"></param>
        /// <param name="onFinish"></param>
        public void StartTimer(float time, Action onFinish)
        {
            TimerData timerData = new TimerData();
            timerData.time = Time.time + time;
            timerData.onFinish = onFinish;
            timers.Enqueue(timerData);
        }
    }

}