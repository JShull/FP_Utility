namespace FuzzPhyte.Utility
{
    using System.Collections;
    using System.Collections.Generic;
    using UnityEngine;

    public class FP_MotionStackController : MonoBehaviour
    {
        [Header("Motion Sequence (Blocks run sequentially)")]
        [SerializeField]
        private List<FP_MotionBlock> motionBlocks = new List<FP_MotionBlock>();

        [SerializeField] private bool resetAllOnStart = false;
        [SerializeField] private bool resetAllOnComplete = false;
        [SerializeField] private bool setupAllOnStart = true;

        private Coroutine sequenceCoroutine;

        #region Public Accessors
        public void Start()
        {
            if (setupAllOnStart)
            {
                SetupAllStack();
            }
        }

        public void PlaySequence()
        {
            if (sequenceCoroutine != null)
            {
                StopCoroutine(sequenceCoroutine);
            }

            sequenceCoroutine = StartCoroutine(SequenceRoutine());
        }

        public void StopSequence()
        {
            if (sequenceCoroutine != null)
            {
                StopCoroutine(sequenceCoroutine);
            }

            EndAllMotions();
        }
        #endregion

        /// <summary>
        /// Main Routine to play the motion stack
        /// </summary>
        /// <returns></returns>
        protected virtual IEnumerator SequenceRoutine()
        {
            if (resetAllOnStart)
            {
                ResetAllMotions();
            }

            foreach (var block in motionBlocks)
            {
                yield return RunBlock(block);
            }

            if (resetAllOnComplete)
            {
                ResetAllMotions();
            }
               

            sequenceCoroutine = null;
        }
        protected virtual void SetupAllStack()
        {
            foreach (var block in motionBlocks)
            {
                foreach (var entry in block.Motions)
                {
                    if (entry.motion != null)
                    {
                        entry.motion.SetupMotion();
                    }    
                }
            }
        }

        private IEnumerator RunBlock(FP_MotionBlock block)
        {
            int finishedCount = 0;
            int total = block.Motions.Count;

            if (total == 0)
                yield break;

            Dictionary<FP_MotionBase, FP_MotionBase.MotionEventHandler> handlers =
                new Dictionary<FP_MotionBase, FP_MotionBase.MotionEventHandler>();

            foreach (var entry in block.Motions)
            {
                if (entry.motion == null)
                {
                    finishedCount++;
                    continue;
                }

                // Inject curve for this execution
                entry.motion.SetOverrideCurve(entry.overrideCurve,entry.overrideDuration,entry.overrideParameterData);

                FP_MotionBase.MotionEventHandler handler = () =>
                {
                    finishedCount++;
                };

                handlers.Add(entry.motion, handler);
                entry.motion.OnMotionEnded += handler;

                entry.motion.StartMotion();
            }

            yield return new WaitUntil(() => finishedCount >= total);

            // Cleanup
            foreach (var pair in handlers)
            {
                if (pair.Key != null)
                    pair.Key.OnMotionEnded -= pair.Value;
            }
        }

        private void ResetAllMotions()
        {
            foreach (var block in motionBlocks)
            {
                foreach (var entry in block.Motions)
                {
                    if (entry.motion != null)
                    {
                        entry.motion.ResetMotion();
                    }
                }
            }
        }

        private void EndAllMotions()
        {
            foreach (var block in motionBlocks)
            {
                foreach (var entry in block.Motions)
                {
                    if (entry.motion != null)
                    {
                        entry.motion.EndMotion();
                    }   
                }
            }
        }
        
    }
}
