using System;
using UnityEngine;

namespace FuzzPhyte.Utility.Notification
{
    
    [Serializable]
    [CreateAssetMenu(fileName = "Overlay Notification", menuName = "FuzzPhyte/Utility/Notification/Overlay", order = 3)]
    public class FP_OverlayNotification : FP_Notification
    {
        [TextArea(2,4)]
        public string OverlayObjective;
        public bool OverlayStatus;
        [Tooltip("Useful for having other media content but not required")]
        public GameObject OverlayVisualData;
        [Space]
        [Tooltip("If we want this to be part of the top left running list of tasks")]
        public OverlayType OverlayTaskType;
        public float OverlayDuration;
        [Tooltip("If we want to delay showing the information, only works on type 'information'")]
        public float DelayBeforeOverlay;
        [Tooltip("Ignore if you don't have a character")]
        public NPCHackTalkingState NPCDialogueState;

    }
}
