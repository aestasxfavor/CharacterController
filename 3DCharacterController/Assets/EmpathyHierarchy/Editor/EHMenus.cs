using UnityEditor;
using UnityEditor.ShortcutManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace EmpathyHierarchy
{
    static class EHMenus
    {
        const string Root = "GameObject/Empathy Hierarchy/";

        // Hierarchy context-menu items run once per selected object; only act on the first call.
        static double _lastRun;
        static bool Once()
        {
            double now = EditorApplication.timeSinceStartup;
            if (now - _lastRun < 0.2) return false;
            _lastRun = now;
            return true;
        }

        static GameObject[] Targets(MenuCommand cmd)
        {
            var sel = Selection.gameObjects;
            if (sel.Length == 0 && cmd != null && cmd.context is GameObject g) sel = new[] { g };
            return sel;
        }

        // ---- shortcuts (remap in Edit > Shortcuts > Empathy Hierarchy)

        [Shortcut("Empathy Hierarchy/Edit Style", KeyCode.E, ShortcutModifiers.Alt)]
        static void EditStyleShortcut() => EHStyleWindow.Open(Selection.gameObjects);

        [Shortcut("Empathy Hierarchy/Toggle Bold", KeyCode.B, ShortcutModifiers.Alt)]
        static void ToggleBoldShortcut()
        {
            var sel = Selection.gameObjects;
            if (sel.Length == 0) return;
            var first = EHStore.Get(sel[0]);
            bool value = first == null || !first.bold;
            EHStore.Modify(sel, s => s.bold = value, "Toggle Bold");
        }

        // ---- menu items (also shown in the Hierarchy right-click menu)

        [MenuItem(Root + "Edit Style...", false, 0)]
        static void EditStyle(MenuCommand cmd)
        {
            if (!Once()) return;
            EHStyleWindow.Open(Targets(cmd));
        }

        [MenuItem(Root + "Clear Style", false, 1)]
        static void ClearStyle(MenuCommand cmd)
        {
            if (!Once()) return;
            EHStore.Clear(Targets(cmd));
        }

        [MenuItem(Root + "Create Separator", false, 12)]
        static void CreateSeparator(MenuCommand cmd)
        {
            if (!Once()) return;
            var anchor = Selection.activeTransform;
            var go = new GameObject("--- Separator ---");
            if (EHSettings.instance.separatorsEditorOnly) go.tag = "EditorOnly";
            Undo.RegisterCreatedObjectUndo(go, "Create Separator");

            if (anchor != null)
            {
                if (anchor.parent != null)
                    Undo.SetTransformParent(go.transform, anchor.parent, "Create Separator");
                else if (go.scene != anchor.gameObject.scene)
                    SceneManager.MoveGameObjectToScene(go, anchor.gameObject.scene);
                go.transform.SetSiblingIndex(anchor.GetSiblingIndex() + 1);
            }
            Selection.activeGameObject = go;
            EditorGUIUtility.PingObject(go);
        }

        [Shortcut("Empathy Hierarchy/Collapse All", KeyCode.C, ShortcutModifiers.Alt | ShortcutModifiers.Shift)]
        static void CollapseAllShortcut() => CollapseAll();

        [MenuItem(Root + "Collapse All", false, 20)]
        static void CollapseAllMenu(MenuCommand cmd)
        {
            if (!Once()) return;
            CollapseAll();
        }

        /// <summary>Folds every GameObject (scene headers stay open) in both Hierarchy versions.</summary>
        internal static void CollapseAll()
        {
#if UNITY_6000_5_OR_NEWER
            EHNewHierarchy.CollapseAll();
#endif
            CollapseLegacy();
        }

        static void CollapseLegacy()
        {
            if (EHLegacyHierarchy.Windows().Length == 0) return;
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                var scene = SceneManager.GetSceneAt(i);
                if (!scene.isLoaded) continue;
                foreach (var root in scene.GetRootGameObjects())
                    EHLegacyHierarchy.SetExpanded(root, false, true);
            }
        }

        [MenuItem(Root + "Settings...", false, 30)]
        static void OpenSettings() => SettingsService.OpenProjectSettings("Project/Empathy Hierarchy");
    }
}
