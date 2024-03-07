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
}
