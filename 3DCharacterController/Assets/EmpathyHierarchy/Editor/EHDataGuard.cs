using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace EmpathyHierarchy
{
    /// <summary>
    /// Keeps the saved data safe across updates and copies.
    /// Runs once per editor load, before Settings / Rules / Memos are read:
    /// <list type="bullet">
    /// <item>Backup: a copy of every data file whenever it changed since the last copy
    /// (Library/EmpathyHierarchy/Backups, the newest 10 per file; Library is local and never versioned).</item>
    /// <item>Script link: a data file stores which script it belongs to. If that link no longer matches
    /// (scripts copied without their .meta files, a moved assembly…), Unity would load an empty object and
    /// later save it over the data. The link is rewritten to the current script first, so the data loads.</item>
    /// </list>
    /// </summary>
    static class EHDataGuard
    {
        internal const string DataFolder = "ProjectSettings/EmpathyHierarchy";
        internal const string BackupFolder = "Library/EmpathyHierarchy/Backups";
        const int KeepBackups = 10;

        static bool _done;

        static readonly (Type type, string file)[] Files =
        {
            (typeof(EHSettings), "Settings.asset"),
            (typeof(EHRules), "Rules.asset"),
            (typeof(EHMemoStore), "Memos.asset"),
            (null, "Bookmarks.json"),
        };

        static readonly Regex ScriptRef =
            new Regex(@"m_Script: \{fileID: (\d+)(?:, guid: ([0-9a-f]{32}), type: \d+)?\}", RegexOptions.Compiled);
        static readonly Regex ClassId = new Regex(@"m_EditorClassIdentifier: .*", RegexOptions.Compiled);

        public static void Ensure()
        {
            if (_done) return;
            _done = true;
            foreach (var entry in Files)
            {
                var type = entry.type;
                var file = entry.file;
                try
                {
                    var path = Path.Combine(DataFolder, file);
                    if (!File.Exists(path)) continue;
                    Backup(path);
                    if (type != null) Relink(type, path);
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[Empathy Hierarchy] Could not check {file}: {e.Message}");
                }
            }
        }

        /// <summary>Backup of every data file right now (before an import replaces them).</summary>
        public static void BackupNow()
        {
            foreach (var entry in Files)
            {
                var path = Path.Combine(DataFolder, entry.file);
                try { if (File.Exists(path)) Backup(path); }
                catch (Exception e) { Debug.LogWarning($"[Empathy Hierarchy] Could not back up {entry.file}: {e.Message}"); }
            }
        }

        /// <summary>Copies the file unless the newest backup already has the same content; keeps the newest few.</summary>
        static void Backup(string path)
        {
            var name = Path.GetFileName(path);
            Directory.CreateDirectory(BackupFolder);
            var bytes = File.ReadAllBytes(path);
            var old = Directory.GetFiles(BackupFolder, name + ".*.bak").OrderBy(f => f, StringComparer.Ordinal).ToArray();
            if (old.Length > 0 && File.ReadAllBytes(old[old.Length - 1]).SequenceEqual(bytes)) return;
            File.WriteAllBytes(Path.Combine(BackupFolder, $"{name}.{DateTime.Now:yyyyMMdd-HHmmss}.bak"), bytes);
            for (int i = 0; i < old.Length + 1 - KeepBackups; i++) File.Delete(old[i]);
        }

        /// <summary>Points the data file at the current script (and assembly) if the link is missing or stale.</summary>
        static void Relink(Type type, string path)
        {
            var text = File.ReadAllText(path);
            var m = ScriptRef.Match(text);
            if (!m.Success) return; // not a text file: nothing to check
            var guid = ScriptGuid(type);
            if (guid == null) return;
            if (m.Groups[1].Value == "11500000" && m.Groups[2].Value == guid) return; // fine

            var fixedText = text.Substring(0, m.Index)
                            + $"m_Script: {{fileID: 11500000, guid: {guid}, type: 3}}"
                            + text.Substring(m.Index + m.Length);
            fixedText = ClassId.Replace(fixedText,
                $"m_EditorClassIdentifier: {type.Assembly.GetName().Name}:{type.Namespace}:{type.Name}", 1);
            File.WriteAllText(path, fixedText);
            Debug.Log($"[Empathy Hierarchy] {Path.GetFileName(path)} was linked to an old script id; relinked so its data loads " +
                      $"(a copy is in {BackupFolder}).");
        }

        /// <summary>The script asset whose file carries the class name.</summary>
        static string ScriptGuid(Type type)
        {
            foreach (var g in AssetDatabase.FindAssets(type.Name + " t:MonoScript"))
            {
                var p = AssetDatabase.GUIDToAssetPath(g);
                if (Path.GetFileNameWithoutExtension(p) != type.Name) continue;
                var script = AssetDatabase.LoadAssetAtPath<MonoScript>(p);
                if (script != null && script.GetClass() == type) return g;
            }
            return null;
        }
    }
}
