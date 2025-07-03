using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine.Rendering;

namespace FuzzPhyte.Utility.Editor
{
    /// <summary>
    /// This work started based on -->https://gist.github.com/cjaube/944b0d5221808c2a761d616f29deaf49
    /// Started to need a way to identify things like URP vs SRP
    /// Also might need to define my own later on 
    /// </summary>
    [InitializeOnLoad]
    public class FPUnityDefine 
    {
        enum PipelineType
        {
            Unsupported,
            BuiltInPipeline,
            UniversalPipeline,
            HDPipeline
        }

        static FPUnityDefine()
        {
            UpdateDefines();
        }

        
        /// <summary>
        /// Main Entry Point to then reference other define needs
        /// </summary>
        static void UpdateDefines()
        {
            UpdateRenderDefines();
        }
        /// <summary>
        /// 
        /// </summary>
        static void UpdateRenderDefines()
        {
            var pipeline = GetPipeline();

            if (pipeline == PipelineType.UniversalPipeline)
            {
                AddDefine("UNITY_PIPELINE_URP");
            }
            else
            {
                RemoveDefine("UNITY_PIPELINE_URP");
            }
            if (pipeline == PipelineType.HDPipeline)
            {
                AddDefine("UNITY_PIPELINE_HDRP");
            }
            else
            {
                RemoveDefine("UNITY_PIPELINE_HDRP");
            }
        }
        /// <summary>
        /// Returns the type of renderpipeline that is currently running
        /// </summary>
        /// <returns></returns>
        static PipelineType GetPipeline()
        {
#if UNITY_2019_1_OR_NEWER
            if (GraphicsSettings.defaultRenderPipeline != null)
            {
                // SRP
                var srpType = GraphicsSettings.defaultRenderPipeline.GetType().ToString();
                if (srpType.Contains("HDRenderPipelineAsset"))
                {
                    return PipelineType.HDPipeline;
                }
                else if (srpType.Contains("UniversalRenderPipelineAsset") || srpType.Contains("LightweightRenderPipelineAsset"))
                {
                    return PipelineType.UniversalPipeline;
                }
                else return PipelineType.Unsupported;
            }
#elif UNITY_2017_1_OR_NEWER
        if (GraphicsSettings.renderPipelineAsset != null) {
            // SRP not supported before 2019
            return PipelineType.Unsupported;
        }
#endif
            // no SRP
            return PipelineType.BuiltInPipeline;
        }

        /// <summary>
        /// Add a custom define
        /// </summary>
        /// <param name="define"></param>
        /// <param name="buildTargetGroup"></param>
        static void AddDefine(string define)
        {
            var definesList = GetDefines();
            if (!definesList.Contains(define))
            {
                definesList.Add(define);
                SetDefines(definesList);
            }
        }

        /// <summary>
        /// Remove a custom define
        /// </summary>
        /// <param name="_define"></param>
        /// <param name="_buildTargetGroup"></param>
        public static void RemoveDefine(string define)
        {
            var definesList = GetDefines();
            if (definesList.Contains(define))
            {
                definesList.Remove(define);
                SetDefines(definesList);
            }
        }

        public static List<string> GetDefines()
        {
            
            var target = EditorUserBuildSettings.activeBuildTarget;
            var buildTargetGroup = BuildPipeline.GetBuildTargetGroup(target);
            // new way to get the named build target
            var namedBuildTarget = UnityEditor.Build.NamedBuildTarget.FromBuildTargetGroup(buildTargetGroup);
            var defines = PlayerSettings.GetScriptingDefineSymbols(namedBuildTarget);
            return defines.Split(';').ToList();
        }

        public static void SetDefines(List<string> definesList)
        {
            var target = EditorUserBuildSettings.activeBuildTarget;
            var buildTargetGroup = BuildPipeline.GetBuildTargetGroup(target);
            var namedBuildTarget = UnityEditor.Build.NamedBuildTarget.FromBuildTargetGroup(buildTargetGroup);
            var defines = string.Join(";", definesList.ToArray());
            PlayerSettings.SetScriptingDefineSymbols(namedBuildTarget, defines);
        }
    }
}
