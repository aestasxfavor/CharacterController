#if UNITY_6000_0_OR_NEWER
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace EmpathyHierarchy
{
    /// <summary>
    /// A preset file: the project-independent part of the look (rules, theme, tone, settings) in one .json,
    /// to bring into another project or hand to someone. Import replaces the current values (after a backup).
    /// Not included: memos, bookmarks, per-object styles (Alt+E), favorite scenes and project icons,
    /// because they point at this project's objects and assets.
    /// </summary>
    [Serializable]
    class EHPresetFile
    {
        public string format = Format;
        public int version = 1;
        public string unity;
        public string created;

        public bool hasRules;
        public string rules;     // EHRules as JSON
        public int theme = -1;   // palette index
        public float saturation = 1f, brightness = 1f, opacity = 1f;

        public bool hasSettings;
        public string settings;  // EHSettings as JSON

        public const string Format = "EmpathyHierarchyPreset";
        public const string Extension = "ehpreset";
    }

    static class EHPreset
    {
        [MenuItem("Tools/Empathy Hierarchy/Preset/Export Preset…", false, 30)]
        public static void OpenExport() => EHPresetWindow.Open(null);

        [MenuItem("Tools/Empathy Hierarchy/Preset/Import Preset…", false, 31)]
        public static void OpenImport()
        {
            var path = EditorUtility.OpenFilePanel("Import Empathy Hierarchy Preset", "", EHPresetFile.Extension + ",json");
            if (string.IsNullOrEmpty(path)) return;
            EHPresetFile file;
            try { file = JsonUtility.FromJson<EHPresetFile>(File.ReadAllText(path)); }
            catch { file = null; }
            if (file == null || file.format != EHPresetFile.Format)
            {
                EditorUtility.DisplayDialog("Import Preset", "This file is not an Empathy Hierarchy preset.", "OK");
                return;
            }
            EHPresetWindow.Open(file, Path.GetFileName(path));
        }

        // ------------------------------------------------------------------ export

        public static void Export(bool rules, bool settings)
        {
            var path = EditorUtility.SaveFilePanel("Export Empathy Hierarchy Preset", "", "MyHierarchyPreset", EHPresetFile.Extension);
            if (string.IsNullOrEmpty(path)) return;
            var s = EHSettings.instance;
            var f = new EHPresetFile
            {
                unity = Application.unityVersion,
                created = DateTime.Now.ToString("yyyy-MM-dd HH:mm"),
                hasRules = rules,
                hasSettings = settings,
            };
            if (rules)
            {
                f.rules = EditorJsonUtility.ToJson(EHRules.instance);
                f.theme = EHStyleWindow.Palette;
                f.saturation = s.globalSaturation;
                f.brightness = s.globalBrightness;
                f.opacity = s.globalOpacity;
            }
            if (settings) f.settings = EditorJsonUtility.ToJson(s);
            File.WriteAllText(path, JsonUtility.ToJson(f, true));
            EditorUtility.RevealInFinder(path);
            Debug.Log("[Empathy Hierarchy] Preset exported: " + path);
        }

        // ------------------------------------------------------------------ import

        public static void Import(EHPresetFile f, bool rules, bool settings)
        {
            EHDataGuard.BackupNow(); // the current values can always be found again
            var s = EHSettings.instance;

            if (settings && f.hasSettings && !string.IsNullOrEmpty(f.settings))
            {
                // keep what belongs to this project (and the tone, which travels with the rules)
                var favorites = new List<string>(s.favoriteScenes);
                var icons = new List<string>(s.customIcons);
                string mode = s.defaultsMode;
                float sat = s.globalSaturation, bri = s.globalBrightness, opa = s.globalOpacity;
                EditorJsonUtility.FromJsonOverwrite(f.settings, s);
                s.favoriteScenes = favorites;
                s.customIcons = icons;
                s.defaultsMode = mode;
                s.globalSaturation = sat; s.globalBrightness = bri; s.globalOpacity = opa;
            }

            if (rules && f.hasRules && !string.IsNullOrEmpty(f.rules))
            {
                EditorJsonUtility.FromJsonOverwrite(f.rules, EHRules.instance);
                if (f.theme >= 0) EHStyleWindow.Palette = f.theme;
                s.globalSaturation = f.saturation;
                s.globalBrightness = f.brightness;
                s.globalOpacity = f.opacity;
            }

            s.SaveSettings();
            EHIcons.ResetIconLists();
            EHRules.MarkChanged(); // saves the rules, clears caches, repaints every window
            Debug.Log("[Empathy Hierarchy] Preset imported" + (rules ? " · rules, theme, tone" : "") + (settings ? " · settings" : "")
                      + ". The previous values are in " + EHDataGuard.BackupFolder + ".");
        }
    }

    /// <summary>Export / Import window: what goes into the file, item by item.</summary>
    class EHPresetWindow : EditorWindow
    {
        EHPresetFile _import; // null = export
        string _fileName;
        bool _rules = true, _settings = true;

        static readonly string[] RuleItems =
        {
            "Naming, Component and Custom rule sets: names, components, colors, gradients, icons, bold",
            "The set in use, the fill (Soft / Solid / Outline) and Inside a matched parent",
            "Theme (Classic / Cocoa / Pastel / Deep)",
            "Tone: saturation, brightness, opacity",
        };

        static readonly string[] SettingItems =
        {
            "Tag & Layer: style, colors",
            "Child count badge",
            "Tree lines, striped rows, child colors",
            "Active checkbox",
            "Bookmarks: bar mode, number shortcuts, ★ on rows",
            "Separators: prefixes, align, outline",
            "Accent bar, outlines, foldout arrows",
            "Icon lists: hidden and recommended icons",
        };

        public static void Open(EHPresetFile import, string fileName = null)
        {
            var w = CreateInstance<EHPresetWindow>();
            w._import = import;
            w._fileName = fileName;
            w._rules = import == null || import.hasRules;
            w._settings = import == null || import.hasSettings;
            w.titleContent = new GUIContent(import == null ? "Export Preset" : "Import Preset");
            var size = new Vector2(460, 560);
            var main = EditorGUIUtility.GetMainWindowPosition();
            w.position = new Rect(main.center - size * 0.5f, size);
            w.minSize = new Vector2(380, 420);
            w.ShowUtility();
        }

        void CreateGUI()
        {
            bool export = _import == null;
            var root = rootVisualElement;
            root.style.backgroundColor = EHTheme.Bg;

            var scroll = new ScrollView(ScrollViewMode.Vertical);
            scroll.style.flexGrow = 1;
            root.Add(scroll);
            var body = scroll.contentContainer;
            body.style.paddingLeft = 18; body.style.paddingRight = 18; body.style.paddingTop = 16; body.style.paddingBottom = 8;

            body.Add(Text(export ? "Export Preset" : "Import Preset", 15, EHTheme.Point, true));
            body.Add(Text(export
                ? "Save the look to one file, to use it in another project or share it."
                : "From " + _fileName + (string.IsNullOrEmpty(_import.created) ? "" : "  ·  " + _import.created)
                  + (string.IsNullOrEmpty(_import.unity) ? "" : "  ·  Unity " + _import.unity), 11, EHTheme.Dim, false, 4, 14));

            body.Add(Section(export ? "Rules" : "Rules", "Rules window", RuleItems, _rules,
                export || _import.hasRules, v => _rules = v));
            body.Add(Section("Settings", "Project Settings > Empathy Hierarchy", SettingItems, _settings,
                export || _import.hasSettings, v => _settings = v));

            var note = Text("Not included: memos, bookmarks, styles given to single objects (Alt+E), favorite scenes and icons added from "
                            + "this project. They point at this project's objects and assets.", 11, EHTheme.Dim, false, 4, 4);
            body.Add(note);
            if (!export)
                body.Add(Text("Importing replaces the current values. A copy of them is kept first (Library/EmpathyHierarchy/Backups).",
                    11, EHTheme.Point, false, 8, 4));

            // footer
            var footer = new VisualElement();
            footer.style.flexDirection = FlexDirection.Row;
            footer.style.justifyContent = Justify.FlexEnd;
            footer.style.paddingLeft = 18; footer.style.paddingRight = 18; footer.style.paddingTop = 10; footer.style.paddingBottom = 14;
            footer.style.borderTopWidth = 1;
            footer.style.borderTopColor = EHTheme.Divider;
            var cancel = MemoUI.SmallButton("Cancel", null, Close);
            cancel.style.height = 26; cancel.style.paddingLeft = 14; cancel.style.paddingRight = 14; cancel.style.marginRight = 6;
            footer.Add(cancel);
            var go = MemoUI.SmallButton(export ? "Export…" : "Import", null, () =>
            {
                if (!_rules && !_settings) return;
                if (export) EHPreset.Export(_rules, _settings);
                else EHPreset.Import(_import, _rules, _settings);
                Close();
            });
            go.style.height = 26; go.style.paddingLeft = 18; go.style.paddingRight = 18;
            go.userData = true; // keeps the accent: the shared hover look is for plain buttons
            go.style.backgroundColor = EHTheme.Accent;
            go.style.color = EHTheme.AccentText;
            go.style.unityFontStyleAndWeight = FontStyle.Bold;
            go.RegisterCallback<PointerEnterEvent>(_ => go.style.backgroundColor = Color.Lerp(EHTheme.Accent, Color.white, 0.12f));
            go.RegisterCallback<PointerLeaveEvent>(_ => go.style.backgroundColor = EHTheme.Accent);
            footer.Add(go);
            root.Add(footer);
        }

        static Label Text(string text, int size, Color color, bool bold, float top = 0f, float bottom = 0f)
        {
            var l = new Label(text);
            l.style.fontSize = size;
            l.style.color = color;
            l.style.whiteSpace = WhiteSpace.Normal;
            l.style.unityFontStyleAndWeight = bold ? FontStyle.Bold : FontStyle.Normal;
            l.style.marginTop = top;
            l.style.marginBottom = bottom;
            return l;
        }

        /// <summary>A checkbox title, where it lives, and the exact list of what it carries.</summary>
        VisualElement Section(string title, string where, string[] items, bool on, bool available, Action<bool> set)
        {
            var box = new VisualElement();
            box.style.backgroundColor = EHTheme.GroupBg;
            box.style.paddingLeft = 12; box.style.paddingRight = 12; box.style.paddingTop = 10; box.style.paddingBottom = 10;
            box.style.marginBottom = 10;
            MemoUI.Radius(box, 8);

            var head = new VisualElement();
            head.style.flexDirection = FlexDirection.Row;
            head.style.alignItems = Align.Center;
            var check = new Toggle { value = on && available };
            check.SetEnabled(available);
            check.style.marginRight = 6;
            head.Add(check);
            head.Add(Text(title, 13, EHTheme.Text, true));
            var whereLabel = Text("  ·  " + where, 11, EHTheme.Dim, false);
            head.Add(whereLabel);
            box.Add(head);

            var list = new VisualElement();
            list.style.marginTop = 6;
            list.style.paddingLeft = 24;
            foreach (var item in items)
            {
                var row = new VisualElement();
                row.style.flexDirection = FlexDirection.Row;
                row.style.marginTop = 2;
                row.Add(Text("•", 11, EHTheme.Point, true));
                var t = Text(item, 11, EHTheme.Text, false);
                t.style.marginLeft = 6;
                t.style.flexShrink = 1;
                row.Add(t);
                list.Add(row);
            }
            if (!available) list.Add(Text("Not in this file.", 11, EHTheme.Dim, false, 4));
            box.Add(list);

            Action paint = () => list.style.opacity = check.value ? 1f : 0.4f;
            paint();
            check.RegisterValueChangedCallback(e => { set(e.newValue); paint(); });
            head.RegisterCallback<PointerDownEvent>(e =>
            {
                if (!available || e.target == check || check.Contains(e.target as VisualElement)) return;
                check.value = !check.value;
            });
            return box;
        }
    }
}
#endif
