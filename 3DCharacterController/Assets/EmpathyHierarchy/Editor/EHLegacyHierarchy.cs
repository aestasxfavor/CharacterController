using System;
using System.Collections;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace EmpathyHierarchy
{
    /// <summary>
    /// Small reflection layer over Unity's classic Hierarchy (SceneHierarchyWindow).
    /// Object ids are int InstanceIDs on 6.3 and EntityIds on 6.4+, so methods are matched by name
    /// and the id is converted to whatever the parameter wants. Everything fails quietly.
    /// </summary>
    static class EHLegacyHierarchy
    {
        const BindingFlags Any = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;

        static readonly Type WindowType = typeof(EditorWindow).Assembly.GetType("UnityEditor.SceneHierarchyWindow");
        static MethodInfo _setExpanded, _setExpandedRecursive;
        static bool _looked;

        static void Look()
        {
            if (_looked) return;
            _looked = true;
            if (WindowType == null) return;
            _setExpanded = IdMethod("SetExpanded");
            _setExpandedRecursive = IdMethod("SetExpandedRecursive");
        }

        static MethodInfo IdMethod(string name)
        {
            foreach (var m in WindowType.GetMethods(Any))
            {
                if (m.Name != name || m.IsStatic) continue;
                var p = m.GetParameters();
                if (p.Length == 2 && p[1].ParameterType == typeof(bool) &&
                    (p[0].ParameterType == typeof(int) || p[0].ParameterType == typeof(EntityId)))
                    return m;
            }
            return null;
        }

        static object Id(GameObject go, MethodInfo m)
        {
#if UNITY_6000_4_OR_NEWER
            return go.GetEntityId();
#else
            return m.GetParameters()[0].ParameterType == typeof(int) ? (object)go.GetInstanceID() : go.GetEntityId();
#endif
        }

        /// <summary>Every open classic Hierarchy window.</summary>
        internal static EditorWindow[] Windows()
        {
            if (WindowType == null) return Array.Empty<EditorWindow>();
            var found = Resources.FindObjectsOfTypeAll(WindowType);
            var result = new EditorWindow[found.Length];
            for (int i = 0; i < found.Length; i++) result[i] = found[i] as EditorWindow;
            return result;
        }

        internal static bool SetExpanded(GameObject go, bool expanded, bool recursive)
        {
            Look();
            var m = recursive ? _setExpandedRecursive : _setExpanded;
            if (m == null || go == null) return false;
            try
            {
                var id = Id(go, m);
                foreach (var w in Windows()) m.Invoke(w, new[] { id, expanded });
                return true;
            }
            catch { return false; }
        }

        // ---- expanded-state snapshot (window → SceneHierarchy → TreeViewState.expandedIDs)

        static IList ExpandedList(EditorWindow w)
        {
            try
            {
                var sh = WindowType.GetField("m_SceneHierarchy", Any)?.GetValue(w);
                var state = sh?.GetType().GetField("m_TreeViewState", Any)?.GetValue(sh);
                return state?.GetType().GetProperty("expandedIDs", Any)?.GetValue(state) as IList;
            }
            catch { return null; }
        }

        /// <summary>Copies the expanded rows of every classic Hierarchy window (null if not available).</summary>
        internal static object Snapshot()
        {
            var windows = Windows();
            var copies = new ArrayList[windows.Length];
            bool any = false;
            for (int i = 0; i < windows.Length; i++)
            {
                var list = ExpandedList(windows[i]);
                if (list == null) continue;
                copies[i] = new ArrayList(list);
                any = true;
            }
            return any ? new object[] { windows, copies } : null;
        }

        internal static bool Restore(object snapshot)
        {
            if (!(snapshot is object[] pair) || !(pair[0] is EditorWindow[] windows) || !(pair[1] is ArrayList[] copies)) return false;
            bool done = false;
            for (int i = 0; i < windows.Length; i++)
            {
                if (windows[i] == null || copies[i] == null) continue;
                var list = ExpandedList(windows[i]);
                if (list == null) continue;
                try
                {
                    list.Clear();
                    foreach (var id in copies[i]) list.Add(id);
                    ReloadData(windows[i]);
                    windows[i].Repaint();
                    done = true;
                }
                catch { /* keep going */ }
            }
            return done;
        }

        static void ReloadData(EditorWindow w)
        {
            try
            {
                var sh = WindowType.GetField("m_SceneHierarchy", Any)?.GetValue(w);
                sh?.GetType().GetMethod("ReloadData", Any, null, Type.EmptyTypes, null)?.Invoke(sh, null);
            }
            catch { /* repaint is enough most of the time */ }
        }

        /// <summary>Shows a short message over the classic Hierarchy under the mouse (or the first one).</summary>
        internal static void Notify(string message)
        {
            var w = EditorWindow.mouseOverWindow;
            if (w == null || w.GetType() != WindowType)
            {
                var all = Windows();
                w = all.Length > 0 ? all[0] : null;
            }
            w?.ShowNotification(new GUIContent(message), 1.2);
        }
    }
}
