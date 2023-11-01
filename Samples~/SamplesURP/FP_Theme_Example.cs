using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using FuzzPhyte.Utility;
using TMPro;
namespace FuzzPhyte.Utility.Samples
{
    public class FP_Theme_Example : MonoBehaviour
    {
        [Tooltip("Core data to be pulled from")]
        public FP_Theme ExampleTheme;
        [Header("References to objects/UI elements in scene")]
        public Image BackGroundRef;
        public Image IconRef;
        public TextMeshProUGUI HeaderRef;
        public TextMeshProUGUI BodyRef;
        public Image LowerPortionBackdrop;
        

        public void Awake()
        {
            if (ExampleTheme != null)
            {
                if (BackGroundRef != null)
                {
                    BackGroundRef.color = ExampleTheme.MainColor;
                }
                if (IconRef != null)
                {
                    //if we have a sprite we can also utilize the texture here to do something else with a Texture2D/Texture
                    IconRef.sprite = ExampleTheme.Icon;
                    IconRef.preserveAspect = true;
                }
                if (HeaderRef != null)
                {
                    HeaderRef.color = ExampleTheme.FontPrimaryColor;
                    HeaderRef.text = ExampleTheme.ThemeLabel;
                }
                if (BodyRef != null)
                {
                    BodyRef.color = ExampleTheme.FontSecondaryColor;
                    BodyRef.text = ExampleTheme.Description;
                }
                if (LowerPortionBackdrop != null)
                {
                    LowerPortionBackdrop.color = ExampleTheme.TertiaryColor;
                }
            }
        }

    }
}
