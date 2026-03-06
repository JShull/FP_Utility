namespace FuzzPhyte.Utility.Editor
{
    using System;
    using System.Collections.Generic;
    using UnityEditor;
    using UnityEditor.SceneManagement;
    using UnityEngine;
    using UnityEngine.SceneManagement;

    [InitializeOnLoad]
    public static class FP_HHeaderMeshPickerCache
    {
        internal sealed class CachedMeshSource
        {
            public Mesh Mesh;
            public Matrix4x4 LocalToWorldMatrix;
            public Vector3[] Vertices;
            public int[] Triangles;
            public Bounds WorldBounds;
            public bool IsSkinned;
        }

        internal sealed class CachedPickTarget
        {
            public GameObject Target;
            public GameObject Header;
            public Bounds CombinedBounds;
            public List<CachedMeshSource> MeshSources = new List<CachedMeshSource>();

            public int TriangleCount
            {
                get
                {
                    int count = 0;
                    for (int i = 0; i < MeshSources.Count; i++)
                    {
                        count += MeshSources[i].Triangles.Length / 3;
                    }

                    return count;
                }
            }
        }

        private static readonly Dictionary<int, CachedPickTarget> cachedTargets = new Dictionary<int, CachedPickTarget>();
        internal static bool IsEnabled => EditorPrefs.GetBool(FP_UtilityData.FP_HHeader_MESHPICKER_ENABLED_KEY + "_" + SceneManager.GetActiveScene().name, true);
        private static readonly Dictionary<int, List<int>> cachedTargetsByHeader = new Dictionary<int, List<int>>();
        private static bool pendingRefresh;
        private static bool debugScenePicking;

        static FP_HHeaderMeshPickerCache()
        {
            EditorApplication.hierarchyChanged += RequestRefresh;
            EditorApplication.projectChanged += RequestRefresh;
            EditorSceneManager.sceneOpened += OnSceneOpened;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            EditorApplication.update += OnEditorUpdate;
            SceneView.duringSceneGui += OnSceneGUI;
            RequestRefresh();
        }

        internal static bool TryGetCachedTarget(GameObject targetObj, out CachedPickTarget cachedTarget)
        {
            cachedTarget = null;
            if (targetObj == null)
            {
                return false;
            }

            return cachedTargets.TryGetValue(targetObj.GetInstanceID(), out cachedTarget);
        }

        internal static bool TryRevealCachedObject(GameObject targetObj)
        {
            if (targetObj == null || !cachedTargets.ContainsKey(targetObj.GetInstanceID()))
            {
                return false;
            }

            return FP_HHeader.TryExpandHeaderForObject(targetObj);
        }

        internal static int CachedTargetCount => cachedTargets.Count;

        internal static void RequestCacheRefresh()
        {
            RequestRefresh();
        }

        [MenuItem("FuzzPhyte/Header/Enable Scene Mesh Picker", false, priority = FP_UtilityData.ORDER_SUBMENU_LVL7 + 1)]
        private static void ToggleSceneMeshPickerMenu()
        {
            bool newValue = !IsEnabled;
            EditorPrefs.SetBool(FP_UtilityData.FP_HHeader_MESHPICKER_ENABLED_KEY + "_" + SceneManager.GetActiveScene().name, newValue);
            if (newValue)
            {
                RequestRefresh();
            }
            else
            {
                ClearCache();
            }

            Debug.Log($"FP_HHeaderMeshPickerCache is now {(newValue ? "Enabled" : "Disabled")}");
            SceneView.RepaintAll();
            EditorApplication.RepaintHierarchyWindow();
        }

        [MenuItem("FuzzPhyte/Header/Enable Scene Mesh Picker", true, priority = FP_UtilityData.ORDER_SUBMENU_LVL7 + 1)]
        private static bool ValidateSceneMeshPickerMenu()
        {
            Menu.SetChecked("FuzzPhyte/Header/Enable Scene Mesh Picker", IsEnabled);
            return true;
        }
        [MenuItem("FuzzPhyte/Header/Debug/Log Mesh Picker Cache", false, 54)]
        private static void LogMeshPickerCache()
        {
            RebuildCache();

            int headerCount = cachedTargetsByHeader.Count;
            int meshCount = 0;
            int triangleCount = 0;
            foreach (var item in cachedTargets.Values)
            {
                meshCount += item.MeshSources.Count;
                triangleCount += item.TriangleCount;
            }

            Debug.Log($"FP_HHeaderMeshPickerCache: cached {cachedTargets.Count} targets across {headerCount} collapsed headers, {meshCount} mesh sources, {triangleCount} triangles.");
        }

        [MenuItem("FuzzPhyte/Header/Debug/Toggle Scene Picker Logging", false, 55)]
        private static void ToggleScenePickerLogging()
        {
            debugScenePicking = !debugScenePicking;
            Debug.Log($"FP_HHeaderMeshPickerCache: Scene picker logging {(debugScenePicking ? "enabled" : "disabled")}");
        }

        private static void OnSceneOpened(Scene scene, OpenSceneMode mode)
        {
            RequestRefresh();
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredPlayMode)
            {
                ClearCache();
                pendingRefresh = false;
                return;
            }

            if (state == PlayModeStateChange.EnteredEditMode)
            {
                RequestRefresh();
            }
        }

        private static void OnEditorUpdate()
        {
            if (!pendingRefresh || EditorApplication.isPlayingOrWillChangePlaymode)
            {
                return;
            }

            pendingRefresh = false;
            RebuildCache();
        }

        private static void OnSceneGUI(SceneView sceneView)
        {
            if (!FP_HHeader.IsEnabled || !IsEnabled || EditorApplication.isPlayingOrWillChangePlaymode)
            {
                return;
            }

            Event currentEvent = Event.current;
            if (currentEvent == null || currentEvent.type != EventType.MouseDown)
            {
                return;
            }

            if (currentEvent.button != 0 || currentEvent.alt)
            {
                return;
            }

            if (cachedTargets.Count == 0)
            {
                if (debugScenePicking)
                {
                    Debug.Log("FP_HHeaderMeshPickerCache: Scene click ignored because cache is empty.");
                }
                return;
            }

            Ray pickRay = HandleUtility.GUIPointToWorldRay(currentEvent.mousePosition);
            if (debugScenePicking)
            {
                Debug.Log($"FP_HHeaderMeshPickerCache: Scene click at {currentEvent.mousePosition}, cached targets {cachedTargets.Count}.");
            }

            if (!TryPickTarget(pickRay, out CachedPickTarget hitTarget, out float hitDistance))
            {
                if (debugScenePicking)
                {
                    Debug.Log("FP_HHeaderMeshPickerCache: no cached mesh hit for this Scene click.");
                }
                return;
            }

            if (!FP_HHeader.TryExpandHeaderForObject(hitTarget.Target))
            {
                if (debugScenePicking)
                {
                    Debug.Log($"FP_HHeaderMeshPickerCache: mesh hit {hitTarget.Target.name} but header expansion failed.");
                }
                return;
            }

            if (debugScenePicking)
            {
                Debug.Log($"FP_HHeaderMeshPickerCache: mesh hit {hitTarget.Target.name} at distance {hitDistance:F3}.");
            }

            Selection.activeGameObject = hitTarget.Target;
            EditorGUIUtility.PingObject(hitTarget.Target);
            currentEvent.Use();
            RequestRefresh();
            SceneView.RepaintAll();
            EditorApplication.RepaintHierarchyWindow();
        }

        private static void RequestRefresh()
        {
            pendingRefresh = true;
        }

        private static void RebuildCache()
        {
            ClearCache();

            if (!FP_HHeader.IsEnabled || !IsEnabled)
            {
                return;
            }

            Scene activeScene = SceneManager.GetActiveScene();
            if (!activeScene.IsValid() || !activeScene.isLoaded)
            {
                return;
            }

            GameObject[] sceneObjects = GameObject.FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < sceneObjects.Length; i++)
            {
                GameObject headerObj = sceneObjects[i];
                if (headerObj == null || headerObj.scene != activeScene || !FP_HHeader.IsHeaderObject(headerObj))
                {
                    continue;
                }

                if (!FP_HHeader.TryGetHeaderState(headerObj, out bool isExpanded) || isExpanded)
                {
                    continue;
                }

                CacheHeaderTargets(headerObj);
            }
        }

        private static void CacheHeaderTargets(GameObject headerObj)
        {
            List<GameObject> groupedObjects = FP_HHeader.CollectObjectsUnderHeader(headerObj);
            if (groupedObjects.Count == 0)
            {
                return;
            }

            int headerId = headerObj.GetInstanceID();
            if (!cachedTargetsByHeader.TryGetValue(headerId, out List<int> targetIds))
            {
                targetIds = new List<int>();
                cachedTargetsByHeader.Add(headerId, targetIds);
            }

            for (int i = 0; i < groupedObjects.Count; i++)
            {
                CachedPickTarget cachedTarget = BuildCachedTarget(groupedObjects[i], headerObj);
                if (cachedTarget == null)
                {
                    continue;
                }

                int targetId = cachedTarget.Target.GetInstanceID();
                cachedTargets[targetId] = cachedTarget;
                targetIds.Add(targetId);
            }
        }

        private static CachedPickTarget BuildCachedTarget(GameObject targetObj, GameObject headerObj)
        {
            if (targetObj == null)
            {
                return null;
            }

            CachedPickTarget cachedTarget = new CachedPickTarget
            {
                Target = targetObj,
                Header = headerObj,
                CombinedBounds = new Bounds(targetObj.transform.position, Vector3.zero)
            };

            bool hasBounds = false;
            MeshFilter[] meshFilters = targetObj.GetComponentsInChildren<MeshFilter>(true);
            for (int i = 0; i < meshFilters.Length; i++)
            {
                MeshFilter meshFilter = meshFilters[i];
                if (meshFilter == null || meshFilter.sharedMesh == null)
                {
                    continue;
                }

                Renderer renderer = meshFilter.GetComponent<Renderer>();
                if (renderer == null)
                {
                    continue;
                }

                if (TryBuildMeshSource(meshFilter.sharedMesh, meshFilter.transform.localToWorldMatrix, renderer.bounds, false, out CachedMeshSource meshSource))
                {
                    cachedTarget.MeshSources.Add(meshSource);
                    if (!hasBounds)
                    {
                        cachedTarget.CombinedBounds = meshSource.WorldBounds;
                        hasBounds = true;
                    }
                    else
                    {
                        cachedTarget.CombinedBounds.Encapsulate(meshSource.WorldBounds);
                    }
                }
            }

            SkinnedMeshRenderer[] skinnedRenderers = targetObj.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            for (int i = 0; i < skinnedRenderers.Length; i++)
            {
                SkinnedMeshRenderer skinnedRenderer = skinnedRenderers[i];
                if (skinnedRenderer == null || skinnedRenderer.sharedMesh == null)
                {
                    continue;
                }

                if (TryBuildMeshSource(skinnedRenderer.sharedMesh, skinnedRenderer.transform.localToWorldMatrix, skinnedRenderer.bounds, true, out CachedMeshSource meshSource))
                {
                    cachedTarget.MeshSources.Add(meshSource);
                    if (!hasBounds)
                    {
                        cachedTarget.CombinedBounds = meshSource.WorldBounds;
                        hasBounds = true;
                    }
                    else
                    {
                        cachedTarget.CombinedBounds.Encapsulate(meshSource.WorldBounds);
                    }
                }
            }

            return cachedTarget.MeshSources.Count > 0 ? cachedTarget : null;
        }

        private static bool TryBuildMeshSource(Mesh mesh, Matrix4x4 localToWorldMatrix, Bounds worldBounds, bool isSkinned, out CachedMeshSource meshSource)
        {
            meshSource = null;
            if (mesh == null)
            {
                return false;
            }

            int[] triangles = mesh.triangles;
            Vector3[] vertices = mesh.vertices;
            if (triangles == null || triangles.Length == 0 || vertices == null || vertices.Length == 0)
            {
                return false;
            }

            meshSource = new CachedMeshSource
            {
                Mesh = mesh,
                LocalToWorldMatrix = localToWorldMatrix,
                Vertices = vertices,
                Triangles = triangles,
                WorldBounds = worldBounds,
                IsSkinned = isSkinned
            };

            return true;
        }

        private static bool TryPickTarget(Ray worldRay, out CachedPickTarget hitTarget, out float hitDistance)
        {
            hitTarget = null;
            hitDistance = float.PositiveInfinity;

            foreach (CachedPickTarget cachedTarget in cachedTargets.Values)
            {
                if (cachedTarget == null || cachedTarget.Target == null)
                {
                    continue;
                }

                if (!cachedTarget.CombinedBounds.IntersectRay(worldRay, out float boundsDistance) || boundsDistance > hitDistance)
                {
                    continue;
                }

                if (TryRaycastCachedTarget(worldRay, cachedTarget, out float targetDistance) && targetDistance < hitDistance)
                {
                    hitDistance = targetDistance;
                    hitTarget = cachedTarget;
                }
            }

            return hitTarget != null;
        }

        private static bool TryRaycastCachedTarget(Ray worldRay, CachedPickTarget cachedTarget, out float hitDistance)
        {
            hitDistance = float.PositiveInfinity;
            bool didHit = false;

            for (int i = 0; i < cachedTarget.MeshSources.Count; i++)
            {
                CachedMeshSource meshSource = cachedTarget.MeshSources[i];
                if (!meshSource.WorldBounds.IntersectRay(worldRay, out float boundsDistance) || boundsDistance > hitDistance)
                {
                    continue;
                }

                if (TryRaycastMeshSource(worldRay, meshSource, out float meshDistance) && meshDistance < hitDistance)
                {
                    hitDistance = meshDistance;
                    didHit = true;
                }
            }

            return didHit;
        }

        private static bool TryRaycastMeshSource(Ray worldRay, CachedMeshSource meshSource, out float hitDistance)
        {
            hitDistance = float.PositiveInfinity;
            Matrix4x4 worldToLocal = meshSource.LocalToWorldMatrix.inverse;
            Vector3 localOrigin = worldToLocal.MultiplyPoint(worldRay.origin);
            Vector3 localDirection = worldToLocal.MultiplyVector(worldRay.direction).normalized;
            Ray localRay = new Ray(localOrigin, localDirection);
            bool didHit = false;

            for (int i = 0; i < meshSource.Triangles.Length; i += 3)
            {
                Vector3 v0 = meshSource.Vertices[meshSource.Triangles[i]];
                Vector3 v1 = meshSource.Vertices[meshSource.Triangles[i + 1]];
                Vector3 v2 = meshSource.Vertices[meshSource.Triangles[i + 2]];

                if (!TryIntersectTriangle(localRay, v0, v1, v2, out float localDistance))
                {
                    continue;
                }

                Vector3 localHitPoint = localRay.origin + (localRay.direction * localDistance);
                Vector3 worldHitPoint = meshSource.LocalToWorldMatrix.MultiplyPoint(localHitPoint);
                float worldDistance = Vector3.Distance(worldRay.origin, worldHitPoint);
                if (worldDistance >= hitDistance)
                {
                    continue;
                }

                hitDistance = worldDistance;
                didHit = true;
            }

            return didHit;
        }

        private static bool TryIntersectTriangle(Ray ray, Vector3 v0, Vector3 v1, Vector3 v2, out float distance)
        {
            distance = 0f;
            const float epsilon = 0.000001f;

            Vector3 edge1 = v1 - v0;
            Vector3 edge2 = v2 - v0;
            Vector3 pVec = Vector3.Cross(ray.direction, edge2);
            float determinant = Vector3.Dot(edge1, pVec);
            if (Mathf.Abs(determinant) < epsilon)
            {
                return false;
            }

            float inverseDeterminant = 1f / determinant;
            Vector3 tVec = ray.origin - v0;
            float u = Vector3.Dot(tVec, pVec) * inverseDeterminant;
            if (u < 0f || u > 1f)
            {
                return false;
            }

            Vector3 qVec = Vector3.Cross(tVec, edge1);
            float v = Vector3.Dot(ray.direction, qVec) * inverseDeterminant;
            if (v < 0f || (u + v) > 1f)
            {
                return false;
            }

            distance = Vector3.Dot(edge2, qVec) * inverseDeterminant;
            return distance > epsilon;
        }

        private static void ClearCache()
        {
            cachedTargets.Clear();
            cachedTargetsByHeader.Clear();
        }
    }
}