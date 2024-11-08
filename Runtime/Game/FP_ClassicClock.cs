namespace FuzzPhyte.Utility
{
    using System;
    using UnityEngine;

    /// <summary>
    /// Simple script to run a classic clock in Unity
    /// </summary>
    public class FP_ClassicClock : MonoBehaviour
    {
        public Transform HourHand;
        public Transform MinuteHand;
        public Transform SecondHand;
        public AudioSource ClockAudio;
        float _runTime = 0;


        private void Awake()
        {
            if (HourHand == null || MinuteHand == null || SecondHand == null)
            {
                Debug.LogError($"This is broken and I am throwing myself off a cliff!");
                Destroy(this);
            }
        }
        

        private void LateUpdate()
        {
            SetClockHands(DateTime.Now.Hour, DateTime.Now.Minute, DateTime.Now.Second);
            if (_runTime >= 1)
            {
                _runTime = 0;
                if (ClockAudio != null)
                {
                    ClockAudio.Play();
                }

            }
            _runTime += Time.deltaTime;
        }
        private void SetClockHands(int twentyFourHour, int minute, int second)
        {
            //method below adjusts the hour hand to be aligned to the current time
            //this is a bit of a hack, but it works

            if (twentyFourHour > 12)
            {
                twentyFourHour -= 12;
            }
            HourHand.localRotation = Quaternion.Euler(0, 0, (twentyFourHour * 30) + ((minute / 60f) * 15));
            MinuteHand.localRotation = Quaternion.Euler(0, 0, (minute * 6f) + ((second / 60f) * 6f));
            SecondHand.localRotation = Quaternion.Euler(0, 0, second * 6f);


        }
    }
}
