namespace FuzzPhyte.Utility
{
    using System.Collections.Generic;
    using System.Collections;
    using UnityEngine;
    using System.Linq;
    using System;
    public class FPColorPulse : MonoBehaviour, IFPOnEnableDisable
    {
        [Tooltip("The object we want to start with")]
        public GameObject RootItem;
        [Tooltip("All Items of Mesh Renderers we want to change")]
        public List<MeshRenderer> AllMeshes = new List<MeshRenderer>();
        public AnimationCurve LerpCurve;
        //using Dictionaries to manage the reference to the materials and the colors by material
        //this should let you have multiple materials with colors tied to them
        protected Dictionary<MeshRenderer, List<Material>> _allMaterials = new Dictionary<MeshRenderer, List<Material>>();
        protected Dictionary<MeshRenderer, List<Color>> _allStartColors = new Dictionary<MeshRenderer, List<Color>>();

        [SerializeField] protected bool buildOnEnable;
        [SerializeField] protected bool useOnDisable;
        public bool UseOnEnable { get => buildOnEnable;set=>buildOnEnable = value; }
        public bool UseOnDisable { get => useOnDisable;set=>useOnDisable = value; }
        protected Coroutine flashCoroutine;
        
        public delegate void OnEnableDisable(GameObject go);
        public event OnEnableDisable OnEnableEvent;
        public event OnEnableDisable OnDisableEvent;
        [Tooltip("The Color we want to flash too via emission")]
        public Color FlashColor;
        //[Tooltip("Are we already in the loop")]
        //[SerializeField]
        //private bool _flashActive = false;
        [Tooltip("Time for flash to take")]
        public float FlashTime = 2f;
        private float _runningTime = 0;

        #region Unity Methods
        /// <summary>
        /// Do you want to build this list
        /// </summary>
        public virtual void OnEnable()
        {
            if (buildOnEnable)
            {
                AllMeshes.Clear();
                _allMaterials.Clear();
                _allStartColors.Clear();
                if (RootItem == null)
                {
                    Debug.LogWarning($"Missing a reference to the RootItem - going to assume you meant this item");
                    RootItem = this.gameObject;
                }
                BuildMeshList(RootItem);
                OnEnableEvent?.Invoke(RootItem);
            }
        }
        /// <summary>
        /// Reset on Disable
        /// </summary>
        public virtual void OnDisable()
        {
            //reset
            if (useOnDisable)
            {
                ResetColors();
                AllMeshes.Clear();
                _allMaterials.Clear();
                _allStartColors.Clear();
                OnDisableEvent?.Invoke(RootItem);
            }
        }

        /*
        /// <summary>
        /// Unity Standard Loop
        /// </summary>
        private void Update()
        {
            if (_flashActive)
            {
                if (_runningTime < FlashTime)
                {
                    //countdown: means when we first start we are at peak flash/white emission and then we lerp down
                    float ratio = 1 - (_runningTime / FlashTime);
                    _runningTime += Time.deltaTime;
                    //color lerp over ratio across all mesh renderers
                    for (int i = 0; i < AllMeshes.Count; i++)
                    {
                        ColorLerpEachMaterial(_allMaterials[AllMeshes[i]], ratio, _allStartColors[AllMeshes[i]]);
                    }

                }
                else
                {
                    _runningTime = 0;
                    _flashActive = false;
                    for (int i = 0; i < AllMeshes.Count; i++)
                    {
                        ResetColorLerp(_allMaterials[AllMeshes[i]], _allStartColors[AllMeshes[i]]);
                    }

                }
            }
        }
        */
        #endregion
        protected IEnumerator FlashCoroutine()
        {
            //_flashActive = true;
            _runningTime = 0f;

            while (_runningTime < FlashTime)
            {
                float ratio = (_runningTime / FlashTime);
                

                for (int i = 0; i < AllMeshes.Count; i++)
                {
                    ColorLerpEachMaterial(_allMaterials[AllMeshes[i]], ratio, _allStartColors[AllMeshes[i]]);
                }
                _runningTime += Time.deltaTime;
                yield return null;
            }

            // Flash completed
            for (int i = 0; i < AllMeshes.Count; i++)
            {
                ResetColorLerp(_allMaterials[AllMeshes[i]], _allStartColors[AllMeshes[i]]);
            }

            //_flashActive = false;
            flashCoroutine = null;
        }
#if UNITY_EDITOR
        [ContextMenu("Build the mesh in the editor")]
        public void EditorBuildMesh()
        {
            BuildMeshList(RootItem);
        }
        [ContextMenu("Flash the mesh renderers")]
        public void EditorFlashMeshTest()
        {
            ActivateFlash();
        }
#endif
        /// <summary>
        /// Build a list of mesh renderers from all nested children/gameobjects
        /// This is a recursive function
        /// </summary>
        /// <param name="theItem"></param>
        protected void BuildMeshList(GameObject theItem)
        {
            if (theItem.GetComponent<MeshRenderer>())
            {
                var curMeshRenderer = theItem.GetComponent<MeshRenderer>();
                AllMeshes.Add(curMeshRenderer);
                var newMatList = new List<Material>();
                var newColorList = new List<Color>();
                for (int j = 0; j < curMeshRenderer.materials.Length; j++)
                {
                    newMatList.Add(curMeshRenderer.materials[j]);
                    try
                    {
                        newColorList.Add(curMeshRenderer.materials[j].GetColor("_EmissionColor"));
                    }
                    catch (Exception e)
                    {
                        Debug.LogError($"Something up with EmissionColors");
                        Debug.LogException(e);
                    }
                }
                //update our dictionaries
                _allMaterials.Add(curMeshRenderer, newMatList);
                _allStartColors.Add(curMeshRenderer, newColorList);
            }
            //check each child
            for (int i = 0; i < theItem.transform.childCount; i++)
            {
                var aChild = theItem.transform.GetChild(i);
                BuildMeshList(aChild.gameObject);
            }
        }
        #region Public Accessors to Activate/Deactivate Flash Whenever
        /// <summary>
        /// Called from whatever item you want
        /// </summary>
        public void ActivateFlash()
        {
            if (flashCoroutine!= null)
            {
                StopCoroutine(flashCoroutine);
            }
            flashCoroutine = StartCoroutine(FlashCoroutine());
            /*
            if (_flashActive)
            {
                //reset the clock
                _runningTime = 0;
            }
            else
            {
                _flashActive = true;
            }
            */
        }
        /// <summary>
        /// Called from whatever item you want
        /// </summary>
        public void DeactiateFlash()
        {
            if (flashCoroutine != null)
            {
                StopCoroutine(flashCoroutine);
                flashCoroutine = null;
            }

            //_flashActive = false;
            ResetColors();
            /*
            _flashActive = false;
            ResetColors();
            */
        }
        #endregion
        #region Functions to Adjust Colors of Emission Property on URP Material
        protected void ResetColors()
        {
            foreach (var mRenderer in AllMeshes)
            {
                ResetColorLerp(_allMaterials[mRenderer], _allStartColors[mRenderer]);
            }
        }
        /// <summary>
        /// Reset the colors
        /// </summary>
        /// <param name="theMat">List of materials to adjust emission on</param>
        /// <param name="beginColors">What to go back to</param>
        protected void ResetColorLerp(List<Material> theMat, List<Color> beginColors)
        {
            for (int i = 0; i < theMat.Count; i++)
            {
                theMat[i].SetColor("_EmissionColor", beginColors[i]);
            }
        }
        /// <summary>
        /// Emission change by ratio between 0-1
        /// </summary>
        /// <param name="theMat"></param>
        /// <param name="theRatio"></param>
        /// <param name="beginColor"></param>
        protected void ColorLerpEachMaterial(List<Material> theMat, float theRatio, List<Color> beginColor)
        {
            if (theRatio > 1)
            {
                Debug.LogWarning($"This shouldn't be greater than 1");
                theRatio = 1;
            }
            for (int i = 0; i < theMat.Count; i++)
            {
                var emissionColor = Color.Lerp(FlashColor, beginColor[i], LerpCurve.Evaluate(theRatio));
                if (theMat[i].HasProperty("_EmissionColor"))
                {
                    theMat[i].EnableKeyword("_EMISSION");
                    theMat[i].SetColor("_EmissionColor", emissionColor);
                }
                else
                {
                    Debug.LogWarning($"No emission on material index: {i}");
                }

                //theMat[i].SetColor("_EmissionColor", emissionColor);
            }
        }
        #endregion
    }
}
