namespace  FuzzPhyte.Utility
{    
    using System;
    using System.Collections.Generic;
    using UnityEngine;

    /// <summary>
    /// Class to establish a Dropdown based on a List of Strings and corresponding Unity Events tied to an enum
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public abstract class FP_DropdownPopulate<T> : MonoBehaviour where T : Enum
    {
        public List<string> DropdownOptions = new List<string>();
        public List<UnityEngine.Events.UnityEvent> DropdownEvents = new List<UnityEngine.Events.UnityEvent>();
        public TMPro.TMP_Dropdown Dropdown;
        public bool UseEnum;

        public virtual void Start()
        {
            
            Dropdown.onValueChanged.RemoveAllListeners();
            if(UseEnum)
            {
                FP_UtilityData.EnumToDropDown<T>(Dropdown,OnDropDownChangeEvent);
            }else{
                //check size and actions
                if(DropdownOptions.Count != DropdownEvents.Count){
                    Debug.LogError($"Mismatch on sizing for list and events");
                    return;
                }
                PopulateDropdownFromList();
            }
            
        }
        public virtual void PopulateDropdownFromList()
        {
            Dropdown.ClearOptions();
            Dropdown.AddOptions(DropdownOptions);
            //add listener to the dropdown
            Dropdown.onValueChanged.AddListener(OnDropDownChangeEvent);
        }
        public virtual void OnDropDownChangeEvent(int index)
        {
            Debug.Log($"Selected Index: {index}");
            DropdownEvents[index].Invoke();
        }
    }
}
