using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace EmpathyHierarchy
{
    [Serializable]
    public class BookmarkEntry
    {
        public string id;     // GlobalObjectId string
        public string scene;  // scene asset GUID (for grouping)
    }

    [Serializable]
    class BookmarkFile
    {
        public int version = 1;
        public List<BookmarkEntry> items = new List<BookmarkEntry>();
    }

    /// <summary>
    /// Navigation-bar bookmarks, shared with the team through
    /// ProjectSettings/EmpathyHierarchy/Bookmarks.json. Version independent.
    /// </summary>
    [InitializeOnLoad]
    public static class EHBookmarks
    {
        const string FilePath = "ProjectSettings/EmpathyHierarchy/Bookmarks.json";

        static BookmarkFile _file;
        static readonly Dictionary<string, GameObject> Resolved = new Dictionary<string, GameObject>();

        public static event Action Changed;

        static EHBookmarks()
        {
            EditorSceneManager.sceneOpened += (s, m) => ClearCache();
            EditorSceneManager.sceneClosed += s => ClearCache();
            EditorSceneManager.sceneSaved += s => ClearCache();
        }

        static void ClearCache()
        {
            Resolved.Clear();
            _members = null;
            Changed?.Invoke();
        }

        // fast "is this object bookmarked?" for row stars (rebuilt lazily after any change)
        static HashSet<GameObject> _members;

        public static bool Contains(GameObject go)
        {
            if (go == null) return false;
            if (_members == null)
            {
                _members = new HashSet<GameObject>();
                foreach (var e in Items)
                {
                    var g = Resolve(e);
                    if (g != null) _members.Add(g);
                }
            }
            return _members.Contains(go);
        }

        /// <summary>Adds the object, or removes it if it's already bookmarked. Returns true if it was added.</summary>
        public static bool Toggle(GameObject go)
        {
            int i = IndexOf(go);
            if (i >= 0)
            {
                Items.RemoveAt(i);
                Save();
                return false;
            }
            TempList.Clear();
            TempList.Add(go);
            Add(TempList);
            return IndexOf(go) >= 0;
        }

        static readonly List<GameObject> TempList = new List<GameObject>();

        /// <summary>Bookmark list key of an object (for the flash after adding).</summary>
        public static string IdOf(GameObject go)
        {
            int i = IndexOf(go);
            return i >= 0 ? Items[i].id : null;
        }

        public static List<BookmarkEntry> Items
        {
            get
            {
                Load();
                return _file.items;
            }
        }

        static void Load()
        {
            if (_file != null) return;
            _file = new BookmarkFile();
            try
            {
                if (File.Exists(FilePath))
                {
                    var f = JsonUtility.FromJson<BookmarkFile>(File.ReadAllText(FilePath));
                    if (f?.items != null) _file = f;
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Empathy Hierarchy] Could not read bookmarks: " + e.Message);
            }
        }

        static void Save()
        {
            _members = null;
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(FilePath));
                File.WriteAllText(FilePath, JsonUtility.ToJson(_file, true));
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Empathy Hierarchy] Could not save bookmarks: " + e.Message);
            }
            Changed?.Invoke();
        }

        /// <summary>The GameObject for a bookmark, or null if its scene isn't loaded. Cached.</summary>
        public static GameObject Resolve(BookmarkEntry e)
        {
            if (e == null || string.IsNullOrEmpty(e.id)) return null;
            if (Resolved.TryGetValue(e.id, out var go))
            {
                // keep cached nulls (scene not loaded); re-resolve only objects that were destroyed
                if (ReferenceEquals(go, null) || go != null) return go;
            }
            go = null;
            if (GlobalObjectId.TryParse(e.id, out var gid))
                go = GlobalObjectId.GlobalObjectIdentifierToObjectSlow(gid) as GameObject;
            Resolved[e.id] = go;
            return go;
        }

        public static int IndexOf(GameObject go)
        {
            if (go == null) return -1;
            var items = Items;
            for (int i = 0; i < items.Count; i++)
                if (Resolve(items[i]) == go) return i;
            return -1;
        }

        /// <summary>Adds objects at <paramref name="index"/> (-1 = end). Existing ones are moved there.</summary>
        public static void Add(IList<GameObject> objects, int index = -1)
        {
            var items = Items;
            if (index < 0 || index > items.Count) index = items.Count;
            foreach (var go in objects)
            {
                if (go == null || EditorUtility.IsPersistent(go)) continue;
                var gid = GlobalObjectId.GetGlobalObjectIdSlow(go);
                if (gid.identifierType != 2 || gid.targetObjectId == 0 || gid.assetGUID.Empty()) continue;
                var id = gid.ToString();
                int existing = items.FindIndex(x => x.id == id);
                if (existing >= 0)
                {
                    var entry = items[existing];
                    items.RemoveAt(existing);
                    if (existing < index) index--;
                    items.Insert(Mathf.Clamp(index, 0, items.Count), entry);
                }
                else
                {
                    items.Insert(Mathf.Clamp(index, 0, items.Count), new BookmarkEntry { id = id, scene = gid.assetGUID.ToString() });
                    Resolved[id] = go;
                }
                index++;
            }
            // objects without a saved ID are skipped; callers explain why (WhyNot)
            Save();
        }

        /// <summary>
        /// Why <paramref name="go"/> can't be bookmarked. Bookmarks are saved by the object's ID in the scene file,
        /// so objects without one (not saved yet, or never saved because a script makes them) are skipped.
        /// </summary>
        public static string WhyNot(GameObject go)
        {
            if (go == null) return null;
            if ((go.hideFlags & (HideFlags.DontSaveInEditor | HideFlags.HideAndDontSave)) != 0)
                return "Can't bookmark \"" + go.name + "\": it isn't saved with the scene (a script creates it)";
            if (PrefabStageUtility.GetPrefabStage(go) != null)
                return "Objects in Prefab Mode can't be bookmarked";
            var scene = go.scene;
            if (!scene.IsValid() || string.IsNullOrEmpty(scene.path))
                return "Save the scene first to bookmark its objects";
            if (EditorApplication.isPlaying)
                return "Objects created in Play Mode can't be bookmarked";
            if (scene.isDirty)
                return "Save the scene first to bookmark new objects";
            var gid = GlobalObjectId.GetGlobalObjectIdSlow(go);
            Debug.Log("[Empathy Hierarchy] No saved ID for \"" + go.name + "\" (" + gid + ")", go);
            return "Can't bookmark \"" + go.name + "\": Unity has no saved ID for it (see Console)";
        }

        public static void Remove(BookmarkEntry e)
        {
            if (Items.Remove(e)) Save();
        }

        /// <summary>Moves a bookmark so it ends up at <paramref name="index"/> in the list as it was before the move.</summary>
        public static void Move(BookmarkEntry e, int index)
        {
            var items = Items;
            int from = items.IndexOf(e);
            if (from < 0) return;
            items.RemoveAt(from);
            if (from < index) index--;
            items.Insert(Mathf.Clamp(index, 0, items.Count), e);
            Save();
        }
    }
}
