// Copyright (c) 2026 John B. Shull.
// FuzzPhyte LLC is a company associated with John B. Shull
//
// Public license: GNU GPLv3-or-later.
// Commercial/proprietary use requires a separate license from John B. Shull.
//
// See LICENSE.md.

namespace FuzzPhyte.Utility.Editor
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using UnityEditor;
    using UnityEditor.SceneManagement;
    using UnityEngine;
    using UnityEngine.SceneManagement;

    /// <summary>
    /// Removes attached programming scripts from selected scene hierarchies while preserving visual structure.
    /// </summary>
    public class FPRemoveCSWindow : EditorWindow
    {
        private enum SceneDestinationMode
        {
            NewScene,
            ExistingScene
        }

        private enum SceneCompletionAction
        {
            HighlightSceneAsset,
            OpenSceneAdditively
        }

        [SerializeField]
        private List<GameObject> targets = new List<GameObject>();
        [SerializeField]
        private bool showTargets = true;
        [SerializeField]
        private GameObject targetToAdd;
        [SerializeField]
        private bool includeChildren = true;
        [SerializeField]
        private bool includeInactive = true;
        [SerializeField]
        private bool keepAudioSources = true;
        [SerializeField]
        private bool keepUiTextComponents = true;
        [SerializeField]
        private bool cleanCopiesInAnotherScene;
        [SerializeField]
        private SceneDestinationMode sceneDestinationMode;
        [SerializeField]
        private string newSceneName;
        [SerializeField]
        private string newSceneFolder = "Assets";
        [SerializeField]
        private SceneAsset destinationSceneAsset;
        [SerializeField]
        private SceneCompletionAction sceneCompletionAction;
        [SerializeField]
        private Vector2 targetScrollPosition;
        [SerializeField]
        private Vector2 windowScrollPosition;

        private FPRemoveCSPlan previewPlan;
        private bool previewDirty = true;

        [MenuItem(FP_UtilityData.MENU_UTILITY_EDITOR_PATH + "Remove CS", priority = FP_UtilityData.MENU_UTILITY_EDITOR + 3)]
        public static void ShowWindow()
        {
            var window = GetWindow<FPRemoveCSWindow>("Remove CS");
            window.minSize = new Vector2(430f, 520f);
            window.Show();
        }

        private void OnEnable()
        {
            if (targets == null)
            {
                targets = new List<GameObject>();
            }

            if (string.IsNullOrWhiteSpace(newSceneName))
            {
                newSceneName = GetSuggestedNewSceneName(SceneManager.GetActiveScene());
            }

            if (!AssetDatabase.IsValidFolder(newSceneFolder))
            {
                newSceneFolder = "Assets";
            }

            EditorApplication.hierarchyChanged += OnHierarchyChanged;
            EditorSceneManager.activeSceneChangedInEditMode += OnActiveSceneChanged;
            previewDirty = true;
        }

        private void OnDisable()
        {
            EditorApplication.hierarchyChanged -= OnHierarchyChanged;
            EditorSceneManager.activeSceneChangedInEditMode -= OnActiveSceneChanged;
        }

        private void OnGUI()
        {
            GUILayout.Label("Remove CS", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Removes attached MonoBehaviour scripts from the listed scene objects. Transforms, renderers, meshes, " +
                "materials, colliders, lights, cameras, Animator / Animation components, and hierarchy placement remain.",
                MessageType.Info);

            windowScrollPosition = EditorGUILayout.BeginScrollView(windowScrollPosition);
            DrawTargets();
            FPMeshPreviewEditorUtility.DrawSectionDivider();
            DrawOptions();
            FPMeshPreviewEditorUtility.DrawSectionDivider();
            DrawPreview();
            EditorGUILayout.EndScrollView();

            DrawActionArea();
        }

        private void DrawTargets()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.BeginHorizontal();
            showTargets = EditorGUILayout.Foldout(showTargets, $"Target Objects ({targets.Count})", true, EditorStyles.foldoutHeader);
            if (GUILayout.Button("Clean Up", EditorStyles.miniButton, GUILayout.Width(72f)))
            {
                CleanTargetList();
            }

            using (new EditorGUI.DisabledScope(targets.Count == 0))
            {
                if (GUILayout.Button("Clear", EditorStyles.miniButton, GUILayout.Width(48f)))
                {
                    targets.Clear();
                    MarkPreviewDirty();
                }
            }
            EditorGUILayout.EndHorizontal();

            if (!showTargets)
            {
                EditorGUILayout.EndVertical();
                return;
            }

            targetScrollPosition = EditorGUILayout.BeginScrollView(
                targetScrollPosition,
                GUILayout.MinHeight(80f),
                GUILayout.MaxHeight(220f));

            if (targets.Count == 0)
            {
                EditorGUILayout.LabelField("No target objects added.", EditorStyles.centeredGreyMiniLabel);
            }

            for (int i = 0; i < targets.Count; i++)
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUI.BeginChangeCheck();
                GameObject updatedTarget = (GameObject)EditorGUILayout.ObjectField(
                    targets[i],
                    typeof(GameObject),
                    true);
                if (EditorGUI.EndChangeCheck())
                {
                    targets[i] = updatedTarget;
                    MarkPreviewDirty();
                }

                if (GUILayout.Button("-", EditorStyles.miniButton, GUILayout.Width(24f)))
                {
                    targets.RemoveAt(i);
                    MarkPreviewDirty();
                    GUIUtility.ExitGUI();
                }
                EditorGUILayout.EndHorizontal();
            }
            EditorGUILayout.EndScrollView();

            EditorGUILayout.BeginHorizontal();
            targetToAdd = (GameObject)EditorGUILayout.ObjectField(
                "Add Object",
                targetToAdd,
                typeof(GameObject),
                true);
            using (new EditorGUI.DisabledScope(targetToAdd == null))
            {
                if (GUILayout.Button("Add", GUILayout.Width(48f)))
                {
                    AddTarget(targetToAdd);
                    targetToAdd = null;
                }
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Add Current Selection"))
            {
                AddTargets(Selection.gameObjects);
            }
            if (GUILayout.Button("Add Empty Slot"))
            {
                targets.Add(null);
                MarkPreviewDirty();
            }
            EditorGUILayout.EndHorizontal();

            DrawDropArea();
            EditorGUILayout.EndVertical();
        }

        private void DrawOptions()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Cleanup Options", EditorStyles.boldLabel);
            EditorGUI.BeginChangeCheck();
            using (new EditorGUI.DisabledScope(cleanCopiesInAnotherScene))
            {
                includeChildren = EditorGUILayout.ToggleLeft("Include Children", includeChildren);
                using (new EditorGUI.DisabledScope(!includeChildren))
                {
                    includeInactive = EditorGUILayout.ToggleLeft("Include Inactive Children", includeInactive);
                }
            }
            keepAudioSources = EditorGUILayout.ToggleLeft("Keep AudioSource Components", keepAudioSources);
            keepUiTextComponents = EditorGUILayout.ToggleLeft("Keep UI / Text Components", keepUiTextComponents);
            if (EditorGUI.EndChangeCheck())
            {
                MarkPreviewDirty();
            }

            EditorGUILayout.Space(3f);
            EditorGUILayout.LabelField(
                "UI / Text includes Canvas, CanvasRenderer, CanvasGroup, TextMesh, Unity UI, and TextMesh Pro. " +
                "RectTransforms always remain so layout and orientation are preserved.",
                EditorStyles.wordWrappedMiniLabel);

            EditorGUILayout.Space(6f);
            EditorGUI.BeginChangeCheck();
            cleanCopiesInAnotherScene = EditorGUILayout.ToggleLeft(
                "Clean Copies In Another Scene",
                cleanCopiesInAnotherScene,
                EditorStyles.boldLabel);
            if (EditorGUI.EndChangeCheck())
            {
                MarkPreviewDirty();
            }

            if (cleanCopiesInAnotherScene)
            {
                EditorGUILayout.LabelField(
                    "Scene copies always clean the complete hierarchy, including inactive children.",
                    EditorStyles.wordWrappedMiniLabel);
                DrawSceneCopyOptions();
            }
            EditorGUILayout.EndVertical();
        }

        private void DrawSceneCopyOptions()
        {
            EditorGUI.indentLevel++;
            sceneDestinationMode = (SceneDestinationMode)EditorGUILayout.EnumPopup(
                "Destination",
                sceneDestinationMode);

            if (sceneDestinationMode == SceneDestinationMode.ExistingScene)
            {
                destinationSceneAsset = (SceneAsset)EditorGUILayout.ObjectField(
                    "Destination Scene",
                    destinationSceneAsset,
                    typeof(SceneAsset),
                    false);
            }
            else
            {
                newSceneName = EditorGUILayout.TextField(
                    new GUIContent("New Scene Name", "Filename for the new cleaned scene asset."),
                    newSceneName);

                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.PrefixLabel("New Scene Folder");
                using (new EditorGUI.DisabledScope(true))
                {
                    EditorGUILayout.TextField(newSceneFolder);
                }

                if (GUILayout.Button("Browse", GUILayout.Width(64f)))
                {
                    SelectNewSceneFolder();
                }
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.LabelField("Scene Asset Path", EditorStyles.miniBoldLabel);
                EditorGUILayout.SelectableLabel(
                    GetNewSceneAssetPath(SceneManager.GetActiveScene()),
                    EditorStyles.textField,
                    GUILayout.Height(EditorGUIUtility.singleLineHeight));
            }

            sceneCompletionAction = (SceneCompletionAction)EditorGUILayout.EnumPopup(
                "After Saving",
                sceneCompletionAction);
            EditorGUI.indentLevel--;

            EditorGUILayout.HelpBox(
                "Only listed objects from the active scene are copied. Parent/child overlaps are copied once, " +
                "prefab connections are fully unpacked, and cleanup runs only on the destination copies. " +
                "Opening leaves the destination loaded additively and makes it active.",
                MessageType.Info);
        }

        private void DrawPreview()
        {
            EnsurePreviewPlan();

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Removal Preview", EditorStyles.boldLabel);
            if (GUILayout.Button("Refresh", EditorStyles.miniButton, GUILayout.Width(58f)))
            {
                MarkPreviewDirty();
                EnsurePreviewPlan();
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.LabelField(
                cleanCopiesInAnotherScene ? "Active Scene Copy Roots" : "Eligible Root Objects",
                previewPlan.EligibleRootCount.ToString());
            EditorGUILayout.LabelField("GameObjects Scanned", previewPlan.ScannedGameObjectCount.ToString());
            EditorGUILayout.LabelField("Programming Scripts", $"{previewPlan.ProgrammingScriptCount} (remove)");
            EditorGUILayout.LabelField(
                "AudioSources",
                $"{previewPlan.AudioSourceCount} ({(keepAudioSources ? "keep" : "remove")})");
            EditorGUILayout.LabelField(
                "UI / Text Components",
                $"{previewPlan.UiTextComponentCount} ({(keepUiTextComponents ? "keep" : "remove")})");
            EditorGUILayout.LabelField("Missing Script Entries", $"{previewPlan.MissingScriptCount} (remove)");
            EditorGUILayout.LabelField("Total Components To Remove", previewPlan.TotalRemovalCount.ToString(), EditorStyles.boldLabel);

            if (cleanCopiesInAnotherScene && previewPlan.EligibleRootCount == 0)
            {
                EditorGUILayout.HelpBox(
                    "No listed objects belong to the currently active scene.",
                    MessageType.Warning);
            }
            else if (previewPlan.SkippedPersistentRootCount > 0)
            {
                EditorGUILayout.HelpBox(
                    $"Skipped {previewPlan.SkippedPersistentRootCount} Project asset target(s). " +
                    "Open a prefab in Prefab Mode to clean its editable hierarchy.",
                    MessageType.Warning);
            }
            else if (previewPlan.TotalRemovalCount == 0)
            {
                EditorGUILayout.HelpBox("No removable components were found with the current options.", MessageType.Info);
            }
            EditorGUILayout.EndVertical();
        }

        private void DrawActionArea()
        {
            EnsurePreviewPlan();
            FPMeshPreviewEditorUtility.DrawSectionDivider();

            Color defaultColor = GUI.color;
            GUI.color = FP_Utility_Editor.OkayColor;
            bool canExecute = cleanCopiesInAnotherScene
                ? previewPlan.EligibleRootCount > 0 && HasValidSceneDestinationSelection()
                : previewPlan.TotalRemovalCount > 0;
            using (new EditorGUI.DisabledScope(!canExecute))
            {
                string buttonLabel = cleanCopiesInAnotherScene
                    ? "Create Clean Scene Copy"
                    : "Clean Listed Objects";
                if (GUILayout.Button(buttonLabel, GUILayout.Height(34f)))
                {
                    if (cleanCopiesInAnotherScene)
                    {
                        ConfirmAndCreateSceneCopy();
                    }
                    else
                    {
                        ConfirmAndClean();
                    }
                }
            }
            GUI.color = defaultColor;
        }

        private bool HasValidSceneDestinationSelection()
        {
            return sceneDestinationMode == SceneDestinationMode.NewScene
                ? AssetDatabase.IsValidFolder(newSceneFolder)
                : destinationSceneAsset != null;
        }

        private void ConfirmAndClean()
        {
            EnsurePreviewPlan();
            string message =
                $"Clean {previewPlan.ScannedGameObjectCount} GameObject(s)?\n\n" +
                $"Programming scripts: {previewPlan.ProgrammingScriptCount}\n" +
                $"AudioSources: {(keepAudioSources ? 0 : previewPlan.AudioSourceCount)}\n" +
                $"UI / Text components: {(keepUiTextComponents ? 0 : previewPlan.UiTextComponentCount)}\n" +
                $"Missing script entries: {previewPlan.MissingScriptCount}\n\n" +
                "The operation is registered with Unity Undo.";

            if (!EditorUtility.DisplayDialog("Remove CS", message, "Remove Components", "Cancel"))
            {
                return;
            }

            FPRemoveCSResult result = FPRemoveCSUtility.Execute(previewPlan);
            MarkPreviewDirty();
            EnsurePreviewPlan();
            Debug.Log(
                $"[Remove CS] Removed {result.RemovedComponentCount} component(s) and " +
                $"{result.RemovedMissingScriptCount} missing script entry/entries from the listed objects.");
            EditorUtility.DisplayDialog(
                "Remove CS Complete",
                $"Removed {result.TotalRemovedCount} component(s). Use Edit > Undo Remove CS Components to restore them.",
                "OK");
        }

        private void ConfirmAndCreateSceneCopy()
        {
            EnsurePreviewPlan();
            Scene sourceScene = SceneManager.GetActiveScene();
            string destinationPath = ResolveDestinationScenePath(sourceScene);
            if (string.IsNullOrEmpty(destinationPath))
            {
                return;
            }

            string message =
                $"Copy {previewPlan.EligibleRootCount} root object(s) and scan " +
                $"{previewPlan.ScannedGameObjectCount} GameObject(s)?\n\n" +
                $"Destination: {destinationPath}\n" +
                $"Programming scripts: {previewPlan.ProgrammingScriptCount}\n" +
                $"AudioSources removed: {(keepAudioSources ? 0 : previewPlan.AudioSourceCount)}\n" +
                $"UI / Text components removed: {(keepUiTextComponents ? 0 : previewPlan.UiTextComponentCount)}\n" +
                $"Missing script entries: {previewPlan.MissingScriptCount}\n\n" +
                "The active scene objects will not be changed. Existing destination scene content is preserved.";

            if (!EditorUtility.DisplayDialog("Create Clean Scene Copy", message, "Copy, Clean, and Save", "Cancel"))
            {
                return;
            }

            CreateAndSaveSceneCopy(sourceScene, destinationPath);
        }

        private string ResolveDestinationScenePath(Scene sourceScene)
        {
            string path;
            if (sceneDestinationMode == SceneDestinationMode.NewScene)
            {
                newSceneName = SanitizeNewSceneName(newSceneName, sourceScene);
                if (!AssetDatabase.IsValidFolder(newSceneFolder))
                {
                    EditorUtility.DisplayDialog(
                        "Invalid Scene Folder",
                        "Choose a valid folder inside this project's Assets folder.",
                        "OK");
                    return null;
                }

                path = GetNewSceneAssetPath(sourceScene);

                if (AssetDatabase.LoadAssetAtPath<SceneAsset>(path) != null)
                {
                    EditorUtility.DisplayDialog(
                        "Scene Already Exists",
                        "Choose a new scene path, or select Existing Scene as the destination.",
                        "OK");
                    return null;
                }
            }
            else
            {
                path = AssetDatabase.GetAssetPath(destinationSceneAsset);
            }

            if (string.IsNullOrEmpty(path))
            {
                EditorUtility.DisplayDialog("Invalid Destination", "Select a valid destination scene.", "OK");
                return null;
            }

            if (!string.IsNullOrEmpty(sourceScene.path) &&
                string.Equals(path, sourceScene.path, StringComparison.OrdinalIgnoreCase))
            {
                EditorUtility.DisplayDialog(
                    "Invalid Destination",
                    "The destination must be different from the active source scene.",
                    "OK");
                return null;
            }

            return path;
        }

        private void SelectNewSceneFolder()
        {
            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string currentFolder = Path.GetFullPath(Path.Combine(projectRoot, newSceneFolder));
            string selectedFolder = EditorUtility.OpenFolderPanel(
                "Select Clean Scene Folder",
                currentFolder,
                string.Empty);
            if (string.IsNullOrWhiteSpace(selectedFolder))
            {
                return;
            }

            string assetsRoot = Path.GetFullPath(Application.dataPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string selectedFullPath = Path.GetFullPath(selectedFolder).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            if (string.Equals(selectedFullPath, assetsRoot, StringComparison.OrdinalIgnoreCase))
            {
                newSceneFolder = "Assets";
                return;
            }

            string assetsPrefix = assetsRoot + Path.DirectorySeparatorChar;
            if (!selectedFullPath.StartsWith(assetsPrefix, StringComparison.OrdinalIgnoreCase))
            {
                EditorUtility.DisplayDialog(
                    "Invalid Scene Folder",
                    "Select a folder inside this Unity project's Assets folder.",
                    "OK");
                return;
            }

            string relativeFolder = selectedFullPath.Substring(assetsPrefix.Length).Replace('\\', '/');
            newSceneFolder = "Assets/" + relativeFolder;
        }

        private string GetNewSceneAssetPath(Scene sourceScene)
        {
            string folder = string.IsNullOrWhiteSpace(newSceneFolder)
                ? "Assets"
                : newSceneFolder.TrimEnd('/', '\\');
            return $"{folder}/{SanitizeNewSceneName(newSceneName, sourceScene)}.unity";
        }

        private static string GetSuggestedNewSceneName(Scene sourceScene)
        {
            return string.IsNullOrWhiteSpace(sourceScene.name)
                ? "FP_CleanVisualScene"
                : sourceScene.name + "_CleanVisuals";
        }

        internal static string SanitizeNewSceneName(string sceneName, Scene sourceScene)
        {
            string sanitizedName = string.IsNullOrWhiteSpace(sceneName)
                ? GetSuggestedNewSceneName(sourceScene)
                : sceneName.Trim();
            if (sanitizedName.EndsWith(".unity", StringComparison.OrdinalIgnoreCase))
            {
                sanitizedName = sanitizedName.Substring(0, sanitizedName.Length - ".unity".Length);
            }

            char[] invalidCharacters = Path.GetInvalidFileNameChars();
            for (int i = 0; i < invalidCharacters.Length; i++)
            {
                sanitizedName = sanitizedName.Replace(invalidCharacters[i], '_');
            }

            return string.IsNullOrWhiteSpace(sanitizedName)
                ? GetSuggestedNewSceneName(sourceScene)
                : sanitizedName;
        }

        private void CreateAndSaveSceneCopy(Scene sourceScene, string destinationPath)
        {
            Scene destinationScene = SceneManager.GetSceneByPath(destinationPath);
            bool destinationWasLoaded = destinationScene.IsValid() && destinationScene.isLoaded;
            FPRemoveCSSceneCopyResult copyResult = null;

            try
            {
                if (!destinationWasLoaded)
                {
                    destinationScene = sceneDestinationMode == SceneDestinationMode.NewScene
                        ? EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive)
                        : EditorSceneManager.OpenScene(destinationPath, OpenSceneMode.Additive);
                }

                SceneManager.SetActiveScene(sourceScene);

                copyResult = FPRemoveCSUtility.CopyAndCleanToScene(
                    targets,
                    sourceScene,
                    destinationScene,
                    CurrentOptions());

                if (!EditorSceneManager.SaveScene(destinationScene, destinationPath))
                {
                    throw new InvalidOperationException("Unity could not save the destination scene.");
                }

                SceneAsset savedScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(destinationPath);
                if (sceneCompletionAction == SceneCompletionAction.OpenSceneAdditively)
                {
                    SceneManager.SetActiveScene(destinationScene);
                }
                else
                {
                    if (!destinationWasLoaded)
                    {
                        EditorSceneManager.CloseScene(destinationScene, true);
                    }

                    Selection.activeObject = savedScene;
                    EditorGUIUtility.PingObject(savedScene);
                }

                Debug.Log(
                    $"[Remove CS] Copied {copyResult.CopiedRoots.Count} root object(s), removed " +
                    $"{copyResult.CleanupResult.TotalRemovedCount} component(s), and saved {destinationPath}.");
                EditorUtility.DisplayDialog(
                    "Clean Scene Copy Complete",
                    $"Saved {copyResult.CopiedRoots.Count} copied root object(s) to:\n{destinationPath}\n\n" +
                    $"Removed {copyResult.CleanupResult.TotalRemovedCount} component(s).",
                    "OK");
            }
            catch (Exception exception)
            {
                if (copyResult != null)
                {
                    FPRemoveCSUtility.DestroyCopiedRoots(copyResult.CopiedRoots);
                }

                if (!destinationWasLoaded && destinationScene.IsValid() && destinationScene.isLoaded)
                {
                    EditorSceneManager.CloseScene(destinationScene, true);
                }

                if (sourceScene.IsValid() && sourceScene.isLoaded)
                {
                    SceneManager.SetActiveScene(sourceScene);
                }

                Debug.LogException(exception);
                EditorUtility.DisplayDialog(
                    "Clean Scene Copy Failed",
                    exception.Message,
                    "OK");
            }
            finally
            {
                MarkPreviewDirty();
            }
        }

        private void DrawDropArea()
        {
            Rect dropRect = GUILayoutUtility.GetRect(0f, 38f, GUILayout.ExpandWidth(true));
            Event currentEvent = Event.current;
            bool isDragEvent = dropRect.Contains(currentEvent.mousePosition) &&
                (currentEvent.type == EventType.DragUpdated || currentEvent.type == EventType.DragPerform);

            Color background = isDragEvent
                ? new Color(FP_UtilityData.FPActiveColor.r, FP_UtilityData.FPActiveColor.g, FP_UtilityData.FPActiveColor.b, 0.22f)
                : FP_Utility_Editor.UnityEditorMiddleGrey;
            EditorGUI.DrawRect(dropRect, background);
            GUI.Box(dropRect, "Drag scene objects here", EditorStyles.centeredGreyMiniLabel);

            if (!isDragEvent)
            {
                return;
            }

            DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
            if (currentEvent.type == EventType.DragPerform)
            {
                DragAndDrop.AcceptDrag();
                AddTargets(DragAndDrop.objectReferences);
            }

            currentEvent.Use();
        }

        private void AddTargets(UnityEngine.Object[] objects)
        {
            if (objects == null)
            {
                return;
            }

            bool changed = false;
            for (int i = 0; i < objects.Length; i++)
            {
                GameObject gameObject = objects[i] as GameObject;
                if (gameObject == null && objects[i] is Component component)
                {
                    gameObject = component.gameObject;
                }

                changed |= AddTarget(gameObject, false);
            }

            if (changed)
            {
                MarkPreviewDirty();
            }
        }

        private bool AddTarget(GameObject gameObject, bool markDirty = true)
        {
            if (gameObject == null || targets.Contains(gameObject))
            {
                return false;
            }

            targets.Add(gameObject);
            if (markDirty)
            {
                MarkPreviewDirty();
            }

            return true;
        }

        private void CleanTargetList()
        {
            var seen = new HashSet<GameObject>();
            for (int i = targets.Count - 1; i >= 0; i--)
            {
                GameObject target = targets[i];
                if (target == null || !seen.Add(target))
                {
                    targets.RemoveAt(i);
                }
            }

            MarkPreviewDirty();
        }

        private void EnsurePreviewPlan()
        {
            if (!previewDirty && previewPlan != null)
            {
                return;
            }

            IList<GameObject> previewRoots = cleanCopiesInAnotherScene
                ? FPRemoveCSUtility.CollectTopLevelRootsInScene(targets, SceneManager.GetActiveScene())
                : targets;
            previewPlan = FPRemoveCSUtility.BuildPlan(previewRoots, CurrentOptions());
            previewDirty = false;
        }

        private FPRemoveCSOptions CurrentOptions()
        {
            return new FPRemoveCSOptions
            {
                IncludeChildren = cleanCopiesInAnotherScene || includeChildren,
                IncludeInactive = cleanCopiesInAnotherScene || includeInactive,
                KeepAudioSources = keepAudioSources,
                KeepUiTextComponents = keepUiTextComponents
            };
        }

        private void OnHierarchyChanged()
        {
            MarkPreviewDirty();
        }

        private void OnActiveSceneChanged(Scene previousScene, Scene newScene)
        {
            MarkPreviewDirty();
        }

        private void MarkPreviewDirty()
        {
            previewDirty = true;
            Repaint();
        }
    }
}
