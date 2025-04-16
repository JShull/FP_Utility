namespace FuzzPhyte.Utility
{
    using System;
    using UnityEngine;

    public class TimerData: IComparable<TimerData>
    {
        public float time;
        public Action onFinish;

        public int CompareTo(TimerData other)
        {
            return time.CompareTo(other.time);
        }
    }
    
    /// <summary>
    /// Singleton pattern
    /// Uses a simple priority queue method to manage timers in a scene
    /// </summary>
    public class FP_Timer : MonoBehaviour,IFPTimer<TimerData>
    {
        protected static FP_Timer _instance;
        public bool DontDestroy=false;
        public static FP_Timer CCTimer { get { return _instance; } }
        public virtual void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(this.gameObject);
            }
            else
            {
                _instance = this;
            }
            if (DontDestroy) 
            {
                DontDestroyOnLoad(this.gameObject);
            }
        }
        protected PriorityQueue<TimerData> timers = new PriorityQueue<TimerData>();
        protected virtual void Update()
        {
            while (timers.Count > 0 && timers.Peek().time <= Time.time)
            {
                TimerData timerData = timers.Dequeue();
                Debug.LogWarning($"Timer Finished with Action: {timerData.onFinish.Method.Name}");
                timerData.onFinish();
            }
        }
        
        #region Timer Methods
        /// <summary>
        /// time in seconds from sending
        /// </summary>
        /// <param name="time"></param>
        /// <param name="onFinish"></param>
        public virtual TimerData StartTimer(float time, Action onFinish)
        {
            TimerData timerData = new TimerData
            {
                time = Time.time + time,
                onFinish = onFinish
            };
            timers.Enqueue(timerData);
            return timerData;
        }
        /// <summary>
        /// Start a timer with an Action<int> callback.
        /// </summary>
        /// <param name="time"></param>
        /// <param name="param"></param>
        /// <param name="onFinish"></param>
        public virtual TimerData StartTimer(float time, int param,Action<int> onFinish)
        {
            TimerData timerData = new TimerData
            {
                time = Time.time + time,
                onFinish = () => onFinish(param)
            };
            timers.Enqueue(timerData);
            return timerData;
        }
        /// <summary>
        /// Start a timer with an Action<string> callback.
        /// </summary>
        /// <param name="time">Time in seconds</param>
        /// <param name="param">String parameter to pass to the callback</param>
        /// <param name="onFinish">Callback to be invoked when the timer finishes</param>
        public virtual TimerData StartTimer(float time, string param, Action<string> onFinish)
        {
            TimerData timerData = new TimerData
            {
                time = Time.time + time,
                onFinish = () => onFinish(param)
            };
            timers.Enqueue(timerData);
            return timerData;
        }
        /// <summary>
        /// Start a timer with an Action<float> callback.
        /// </summary>
        /// <param name="time">Time in seconds</param>
        /// <param name="param">Float parameter to pass to the callback</param>
        /// <param name="onFinish">Callback to be invoked when the timer finishes</param>
        public virtual TimerData StartTimer(float time, float param, Action<float> onFinish)
        {
            TimerData timerData = new TimerData
            {
                time = Time.time + time,
                onFinish = () => onFinish(param)
            };
            timers.Enqueue(timerData);
            return timerData;
        }
        /// <summary>
        /// Start a timer with a 'FP_Data' callback
        /// </summary>
        /// <param name="time"></param>
        /// <param name="param"></param>
        /// <param name="onFinish"></param>
        public virtual TimerData StartTimer(float time, FP_Data param, Action<FP_Data> onFinish)
        {
            TimerData timerData = new TimerData
            {
                time = Time.time + time,
                onFinish = () => onFinish(param)
            };
            timers.Enqueue(timerData);
            return timerData;
        }
        /// <summary>
        /// Start a timer with a 'GameObject' callback
        /// </summary>
        /// <param name="time"></param>
        /// <param name="param"></param>
        /// <param name="onFinish"></param>
        public virtual TimerData StartTimer(float time, GameObject param, Action<GameObject> onFinish)
        {
            TimerData timerData = new TimerData
            {
                time = Time.time + time,
                onFinish = () => onFinish(param)
            };
            timers.Enqueue(timerData);
            return timerData;
        }
        #endregion
    }
}