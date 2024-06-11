using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace FuzzPhyte.Utility
{
    /// <summary>
    /// Base Interface for misc. WebGL functions we might need via a Javascript library call
    /// </summary>
    public interface IFPWebGL 
    {
        public void OnFullScreenChange(string message);
    }
    // Unity example of use
    /*
     *  // Full screen listener example on the js side
      
    function unityFullScreenListener() {
        document.addEventListener("fullscreenchange", function() {
            var isInFullScreen = document.fullscreenElement != null;
            unityInstance.SendMessage('GenericController', 'OnFullScreenChange', isInFullScreen.toString());
        });
    }
     *
     //UNITY side
     * public void OnFullScreenChange(string isInFullScreen)
        {
            bool isFullScreen = bool.Parse(isInFullScreen);
            Debug.Log("Full-screen mode: " + isFullScreen);
            if (isFullScreen)
            {
                //open the menu
                MenuOpenUI();
            }
            // Add any additional logic here for when the full-screen state changes
        }
     */
}
