using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace EmpathyHierarchy
{
    /// <summary>
    /// Writes a report about the Hierarchy window this editor uses (window type, UI Toolkit tree,
    /// Hierarchy-related APIs) so the new-Hierarchy renderer can be built against the real structure.
    /// Output: Logs/EmpathyHierarchy/hierarchy_diag.txt in the project folder (outside Assets).
    /// </summary>
    static class EHDiagnostics
    {
        const int MaxLines = 6000;
        static int _lines;

        [MenuItem("Tools/Empathy Hierarchy/Help/Create Diagnostics Report", false, 300)]
        static void Run()
        {
            _lines = 0;
            var sb = new StringBuilder();
            sb.AppendLine("Unity " + Application.unityVersion);
            sb.AppendLine("Legacy IMGUI row callback calls so far: " + EHHierarchyGUI.CallCount);
            sb.AppendLine();

            // ---- built-in rule icons: do they exist in this Unity version?
            int missing = 0;
            sb.AppendLine("=== BUILT-IN RULE ICONS");
            foreach (var set in new[] { EHRulePresets.ComponentSet(), EHRulePresets.NamingSet() })
                foreach (var rule in set)
                {
                    if (rule.style == null || rule.style.iconMode != IconMode.Custom) continue;
                    bool ok = EHIcons.Find(rule.style.icon) != null;
                    if (!ok) missing++;
                    sb.AppendLine($"  {(ok ? "OK     " : "MISSING")}  {rule.group} / {rule.name}  {rule.style.icon}");
                }
            sb.AppendLine();
            if (missing == 0) Debug.Log("[Empathy Hierarchy] Built-in rule icons: all found in Unity " + Application.unityVersion);
            else Debug.LogWarning($"[Empathy Hierarchy] Built-in rule icons: {missing} missing in Unity {Application.unityVersion} (see the diagnostics file)");

            // ---- windows
            foreach (var w in Resources.FindObjectsOfTypeAll<EditorWindow>())
            {
                var tn = w.GetType().FullName ?? "";
                if (tn.IndexOf("Hierarchy", StringComparison.OrdinalIgnoreCase) < 0) continue;
                sb.AppendLine($"=== WINDOW {tn}  (assembly {w.GetType().Assembly.GetName().Name})  title='{w.titleContent.text}'");
                try { Dump(w.rootVisualElement, 0, sb); }
                catch (Exception e) { sb.AppendLine("  dump failed: " + e.Message); }
                sb.AppendLine();
            }

            // ---- types
            sb.AppendLine("=== TYPES containing 'Hierarchy'");
#pragma warning disable UAC0005 // diagnostics only: a one-off read of loaded editor assemblies
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
#pragma warning restore UAC0005
            {
                var an = asm.GetName().Name;
                if (!(an.StartsWith("UnityEditor") || an.StartsWith("UnityEngine") || an.StartsWith("Unity."))) continue;
                Type[] types;
                try { types = asm.GetTypes(); }
                catch (ReflectionTypeLoadException e) { types = e.Types.Where(t => t != null).ToArray(); }

                foreach (var t in types.OrderBy(t => t.FullName))
                {
                    if (t.FullName == null || t.FullName.IndexOf("Hierarchy", StringComparison.OrdinalIgnoreCase) < 0) continue;
                    if (t.FullName.Contains("<") || t.FullName.Contains("Profil")) continue;
                    bool pub = t.IsPublic || t.IsNestedPublic;
                    Line(sb, $"TYPE {(pub ? "public" : "internal")} {Kind(t)} {t.FullName} : {t.BaseType?.Name}  [{an}]");
                    if (!pub && !t.Name.Contains("Window") && !t.Name.Contains("Item") && !t.Name.Contains("Column")) continue;

                    const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance |
                                               BindingFlags.Static | BindingFlags.DeclaredOnly;
                    foreach (var m in t.GetMembers(flags))
                    {
                        if (m.Name.StartsWith("<") || m.Name.StartsWith("get_") || m.Name.StartsWith("set_") ||
                            m.Name.StartsWith("add_") || m.Name.StartsWith("remove_")) continue;
                        bool mPub = IsPublic(m);
                        if (!pub && !mPub && m.MemberType != MemberTypes.Event && m.MemberType != MemberTypes.Field) continue;
                        Line(sb, $"    {(mPub ? "+" : "-")} {Describe(m)}");
                    }
                }
            }

            var dir = Path.GetFullPath("Logs/EmpathyHierarchy");
            Directory.CreateDirectory(dir);
            var path = Path.Combine(dir, "hierarchy_diag.txt");
            File.WriteAllText(path, sb.ToString());
            Debug.Log("[Empathy Hierarchy] Diagnostics written to " + path);
            EditorUtility.RevealInFinder(path);
        }

        static void Line(StringBuilder sb, string s)
        {
            if (_lines++ < MaxLines) sb.AppendLine(s);
        }

        static void Dump(VisualElement ve, int depth, StringBuilder sb)
        {
            if (ve == null || depth > 40) return;
            var cls = string.Join(".", ve.GetClasses());
            var extra = ve is TextElement te && !string.IsNullOrEmpty(te.text) ? $" text='{Trunc(te.text)}'" : "";
            var bind = ve.userData != null ? $" userData={ve.userData.GetType().Name}" : "";
            Line(sb, $"{new string(' ', depth * 2)}{ve.GetType().FullName} #{ve.name} .{cls} children={ve.childCount}{extra}{bind}");
            // rows repeat a lot: only expand the first few children of large lists fully
            int i = 0;
            foreach (var child in ve.Children())
            {
                if (i++ >= 6 && ve.childCount > 8) { Line(sb, new string(' ', (depth + 1) * 2) + $"... {ve.childCount - 6} more"); break; }
                Dump(child, depth + 1, sb);
            }
        }

        static string Trunc(string s) => s.Length > 40 ? s.Substring(0, 40) + "…" : s;

        static string Kind(Type t) => t.IsEnum ? "enum" : t.IsInterface ? "interface" : t.IsValueType ? "struct" :
            t.IsAbstract && t.IsSealed ? "static class" : "class";

        static bool IsPublic(MemberInfo m)
        {
            switch (m)
            {
                case MethodBase mb: return mb.IsPublic;
                case FieldInfo f: return f.IsPublic;
                case PropertyInfo p: return (p.GetMethod?.IsPublic ?? false) || (p.SetMethod?.IsPublic ?? false);
                case EventInfo e: return e.AddMethod?.IsPublic ?? false;
                case Type nt: return nt.IsNestedPublic;
                default: return false;
            }
        }

        static string Describe(MemberInfo m)
        {
            switch (m)
            {
                case MethodInfo mi:
                    return $"method {(mi.IsStatic ? "static " : "")}{mi.ReturnType.Name} {mi.Name}(" +
                           string.Join(", ", mi.GetParameters().Select(p => p.ParameterType.Name + " " + p.Name)) + ")";
                case ConstructorInfo ci:
                    return "ctor (" + string.Join(", ", ci.GetParameters().Select(p => p.ParameterType.Name)) + ")";
                case PropertyInfo pi: return $"prop {pi.PropertyType.Name} {pi.Name}";
                case FieldInfo fi: return $"field {(fi.IsStatic ? "static " : "")}{fi.FieldType.Name} {fi.Name}";
                case EventInfo ei: return $"event {ei.EventHandlerType?.Name} {ei.Name}";
                case Type nt: return $"nested {nt.Name}";
                default: return m.MemberType + " " + m.Name;
            }
        }
    }
}
