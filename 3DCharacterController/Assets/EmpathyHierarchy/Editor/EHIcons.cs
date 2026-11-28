using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace EmpathyHierarchy
{
    /// <summary>Built-in editor icon catalog and component-aware icon lookup.</summary>
    public static class EHIcons
    {
        public static readonly string[] General =
        {
            "GameObject Icon", "Prefab Icon", "PrefabVariant Icon", "Folder Icon", "FolderOpened Icon",
            "SceneAsset Icon", "cs Script Icon", "TextAsset Icon", "ScriptableObject Icon", "Settings",
            "Favorite", "FilterByLabel", "console.infoicon", "console.warnicon", "console.erroricon",
            "Grid Icon", "Font Icon", "Material Icon", "Shader Icon", "Texture Icon"
        };

        public static readonly string[] Scene =
        {
            "Camera Icon", "Light Icon", "DirectionalLight Icon", "ReflectionProbe Icon", "LightProbeGroup Icon",
            "Terrain Icon", "MeshRenderer Icon", "SkinnedMeshRenderer Icon", "MeshFilter Icon", "SpriteRenderer Icon",
            "ParticleSystem Icon", "Tilemap Icon", "Volume Icon", "WindZone Icon", "LODGroup Icon",
            "Canvas Icon", "RectTransform Icon", "EventSystem Icon", "VideoPlayer Icon", "Skybox Icon"
        };

        public static readonly string[] Gameplay =
        {
            "Animator Icon", "Animation Icon", "AnimationClip Icon", "Rigidbody Icon", "Rigidbody2D Icon",
            "BoxCollider Icon", "SphereCollider Icon", "CapsuleCollider Icon", "MeshCollider Icon", "CharacterController Icon",
            "NavMeshAgent Icon", "NavMeshObstacle Icon", "AudioSource Icon", "AudioListener Icon", "AudioClip Icon",
            "AudioMixerController Icon", "PlayableDirector Icon", "Joystick", "BuildSettings.Standalone", "UnityEditor.GameView"
        };

        static readonly Dictionary<string, Texture> Cache = new Dictionary<string, Texture>();

        public const string ResPrefix = "res:";     // built-in editor resource path
        public const string AssetPrefix = "asset:"; // project texture by GUID (user-added icons)

        /// <summary>
        /// Resolves an icon key without logging errors: a built-in icon name ("Camera Icon"),
        /// "res:&lt;editor resource path&gt;" or "asset:&lt;guid&gt;". Prefers the dark-skin variant for names.
        /// </summary>
        public static Texture Find(string key)
        {
            if (string.IsNullOrEmpty(key)) return null;
            if (Cache.TryGetValue(key, out var tex) && tex != null) return tex;

            tex = null;
            try
            {
                if (key.StartsWith(ResPrefix, StringComparison.Ordinal))
                    tex = EditorGUIUtility.Load(key.Substring(ResPrefix.Length)) as Texture2D;
                else if (key.StartsWith(AssetPrefix, StringComparison.Ordinal))
                {
                    var path = AssetDatabase.GUIDToAssetPath(key.Substring(AssetPrefix.Length));
                    if (!string.IsNullOrEmpty(path)) tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                }
                else
                {
                    if (EditorGUIUtility.isProSkin) tex = EditorGUIUtility.FindTexture("d_" + key);
                    if (tex == null) tex = EditorGUIUtility.FindTexture(key);
                }
            }
            catch { tex = null; }
            Cache[key] = tex;
            return tex;
        }

        /// <summary>Readable name for tooltips and search.</summary>
        public static string DisplayName(string key)
        {
            if (string.IsNullOrEmpty(key)) return "";
            if (key.StartsWith(ResPrefix, StringComparison.Ordinal))
            {
                var n = Path.GetFileNameWithoutExtension(key.Substring(ResPrefix.Length));
                return n.StartsWith("d_") ? n.Substring(2) : n;
            }
            if (key.StartsWith(AssetPrefix, StringComparison.Ordinal))
                return Path.GetFileNameWithoutExtension(AssetDatabase.GUIDToAssetPath(key.Substring(AssetPrefix.Length)));
            return key.Replace(" Icon", "");
        }

        static readonly Dictionary<string, string> Tooltips = new Dictionary<string, string>();
        static readonly Dictionary<string, string> CustomTooltips = new Dictionary<string, string>();

        /// <summary>Tooltip for an icon cell (cached; the grid asks for it every GUI event).</summary>
        public static string Tooltip(string key, bool isCustom)
        {
            var cache = isCustom ? CustomTooltips : Tooltips;
            if (!cache.TryGetValue(key, out var t))
            {
                t = DisplayName(key) + (isCustom ? "  (right-click to remove)" : "  (right-click: recommend / hide)");
                cache[key] = t;
            }
            return t;
        }

        /// <summary>
        /// Hand-picked built-in icons shown first in the All tab (by name, so it survives Unity versions):
        /// recognizable at a glance, fit for objects, clean at 16 px, plus uniform color sets
        /// (circles, diamonds, pills, dots, labels) for color coding. Names missing in a version are skipped.
        /// </summary>
        public static readonly string[] RecommendedNames =
        {
            "avatar icon", "avatarmask icon", "bodysilhouette", "humantemplate icon", "headzoomsilhouette",
            "halo icon", "navmeshagent icon", "offmeshlink icon", "softlockinline", "softlockprojectbrowser icon",
            "networkidentity icon", "networkmanager icon", "hololensinputmodule icon", "touchinputmodule icon", "viewtoolmove",
            "tree icon", "speedtreeimporter icon", "tree_icon", "tree_icon_leaf", "tree_icon_frond",
            "terraininspector.terraintooltrees", "terrain icon", "terraininspector.terraintoolsculpt", "terraininspector.terraintoolplants", "windzone icon",
            "speedtreewindasset icon", "fog on@64", "skybox icon", "directionallight gizmo", "main light gizmo",
            "orangelight", "cloudconnect", "services", "light icon", "lightmapping",
            "spotlight icon", "arealight icon", "greenlight", "redlight", "particlesystem icon",
            "particle effect", "particlesystemforcefield icon", "visualeffectasset icon", "visualeffect icon", "videoeffect icon",
            "flare icon", "lensflare icon", "lensflare gizmo",
            "particlesystem gizmo", "lightprobegroup gizmo", "lightprobegroup icon", "lightprobes icon",
            "lightprobeproxyvolume gizmo", "lightprobeproxyvolume icon", "welcomescreen.assetstorelogo", "camera gizmo", "camera icon", "framecapture",
            "projector icon", "videoplayer icon", "videoclip icon", "movietexture icon", "image icon",
            "audiosource icon", "audiolistener icon", "audioclip icon", "microphone icon", "buildsettings.standalone",
            "unityeditor.devicesimulation.simulatorwindow", "unityeditor.gameview", "standaloneinputmodule icon", "keyboard", "mouseleft",
            "signalemitter icon", "serverclient", "profiler.memory", "cacheserverconnected", "eventsystem icon",
            "buildsettings.web", "networkproximitychecker icon", "lightmap icon", "gameobject on icon", "prefab icon",
            "prefabvariant icon", "prematcube", "prematcylinder", "prematsphere", "premattorus",
            "modelimporter icon", "boxcollider icon", "spherecollider icon", "capsulecollider icon", "charactercontroller icon",
            "rigidbody2d icon", "physicsmaterial icon", "wheelcollider icon", "springjoint icon", "fixedjoint icon",
            "constantforce icon", "pointeffector2d icon", "cloth icon", "logic", "audiorandomcontainer icon",
            "playabledirector icon", "assetstore icon", "navmeshobstacle icon", "assembly icon", "assetlabeliconsquare",
            "filterbylabel", "locked", "link", "pin", "favorite_colored",
            "favorite on icon", "folderfavorite icon", "folder icon", "clipboard", "textasset icon",
            "cs script icon", "debug", "settings icon",
            "gamemanager icon", "editorsettings icon", "bigicons.gear", "tagmanager icon", "lightingsettings icon",
            "audiomixercontroller icon", "preset icon", "scriptableobject icon", "renderpipelineasset icon",
            "timelineasset icon", "animatorcontroller icon", "eventtrigger icon", "customtool", "customized", "profiler.custom", "search icon",
            "math", "distancejoint2d icon", "teststopwatch", "treeeditor.trash", "verified",
            "alertdialog", "console.erroricon", "console.warnicon", "console.infoicon", "greencheckmark",
            "grid icon", "grid.erasertool", "grid.filltool", "clothinspector.painttool", "clothinspector.viewvalue",
            "aimconstraint icon", "aisparkle icon", "2d-badge-scene-template", "3d-badge-scene-template", "circleblue",
            "circlegray", "circlegreen", "circleorange", "circlepurple", "circlered",
            "circleteal", "circleyellow", "diamondblue", "diamondgray", "diamondgreen",
            "diamondorange", "diamondpurple", "diamondred", "diamondteal", "diamondyellow",
            "pillblue", "pillgray", "pillgreen", "pillorange", "pillpurple",
            "pillred", "pillteal", "pillyellow", "sv_icon_dot0_pix16_gizmo", "sv_icon_dot1_pix16_gizmo",
            "sv_icon_dot2_pix16_gizmo", "sv_icon_dot3_pix16_gizmo", "sv_icon_dot4_pix16_gizmo", "sv_icon_dot5_pix16_gizmo", "sv_icon_dot6_pix16_gizmo",
            "sv_icon_dot7_pix16_gizmo", "sv_icon_dot8_pix16_gizmo", "sv_icon_dot9_pix16_gizmo", "sv_icon_dot10_pix16_gizmo", "sv_icon_dot11_pix16_gizmo",
            "sv_icon_dot12_pix16_gizmo", "sv_icon_dot13_pix16_gizmo", "sv_icon_dot14_pix16_gizmo", "sv_icon_dot15_pix16_gizmo", "sv_label_0",
            "sv_label_1", "sv_label_2", "sv_label_3", "sv_label_4", "sv_label_5",
            "sv_label_6", "sv_label_7", "sv_icon_name0", "sv_icon_name1", "sv_icon_name2",
            "sv_icon_name3", "sv_icon_name4", "sv_icon_name5", "sv_icon_name6", "sv_icon_name7",
            // added during curation
            "debug on", "debuggerattached", "debuggerdisabled", "debuggerenabled",
            "debug_frame_d", "avatarblendright", "avatarblendrighta", "avatarblendleft",
            "avatarblendlefta", "blendtree icon", "dotframedotted", "ihvimageformatimporter icon",
            "networkdiscovery icon", "networkview icon", "networkanimator icon", "networkstartposition icon",
            "networktransform icon", "networktransformchild icon", "pretexturearrayfirstslice", "pretexturearraylastslice",
            "pretexturergb", "pretexturemipmaphigh", "pretexturemipmaplow", "projector gizmo",
            "sceneviewrgb", "tree_icon_branch_frond", "windzone gizmo", "visualeffectsubgraphblock icon",
            "visualeffectsubgraphblock on icon", "visualeffectsubgraphoperator icon", "visualeffectsubgraphoperator on icon", "treeeditor.addleaves"
        };

        static List<string> _recommended;
        static HashSet<string> _recommendedSet;

        /// <summary>Recommended icon keys that exist in this Unity version, in the hand-picked order.</summary>
        public static IReadOnlyList<string> Recommended
        {
            get
            {
                if (_recommended == null) BuildRecommended();
                return _recommended;
            }
        }

        public static bool IsRecommended(string key)
        {
            if (_recommended == null) BuildRecommended();
            return _recommendedSet.Contains(key);
        }

        /// <summary>Call after the user adds/removes recommended icons.</summary>
        public static void InvalidateRecommended() => _recommended = null;

        /// <summary>
        /// Built-in picks minus the ones the user removed, then the ones the user added
        /// (EHSettings.recommendedRemoved / recommendedAdded, stored by name).
        /// </summary>
        static void BuildRecommended()
        {
            _recommended = new List<string>();
            _recommendedSet = new HashSet<string>();
            var byName = new Dictionary<string, string>();
            foreach (var key in All)
            {
                var n = DisplayName(key);
                if (!byName.ContainsKey(n)) byName[n] = key;
            }
            var s = EHSettings.instance;
            var removed = new HashSet<string>(s.recommendedRemoved);
            foreach (var n in RecommendedNames)
                if (!removed.Contains(n) && byName.TryGetValue(n, out var key) && _recommendedSet.Add(key)) _recommended.Add(key);
            foreach (var n in s.recommendedAdded)
                if (byName.TryGetValue(n, out var key) && _recommendedSet.Add(key)) _recommended.Add(key);
        }

        static HashSet<string> _builtinRecommended;

        /// <summary>True if the name is one of the shipped picks (used to decide add vs. un-remove).</summary>
        public static bool IsBuiltinRecommended(string name)
        {
            if (_builtinRecommended == null) _builtinRecommended = new HashSet<string>(RecommendedNames);
            return _builtinRecommended.Contains(name);
        }

        /// <summary>
        /// Built-in icons left out of the All tab entirely (editor UI parts, size/state duplicates,
        /// platform logos, blurry small versions...). Curated by hand; by name so it survives versions.
        /// Tools > Empathy Hierarchy > Developer > Show Excluded Icons brings them back for re-curation.
        /// </summary>
        static readonly string[] ExcludedNames =
        {
            "085_spriteshape_batching_multi_material", "aboutwindow.mainheader", "ageialogo", "align_vertically_bottom",
            "align_vertically_bottom_active", "align_vertically_center", "align_vertically_center_active", "align_vertically_top",
            "align_vertically_top_active", "anchortransformtool", "anchortransformtool on", "anglesnap on",
            "animationanimated", "animationvisibilitytoggleoff", "animationvisibilitytoggleon", "animationwrapmodemenu",
            "anystatetransition", "anystatetransition on@128", "anystatetransition on@16", "anystatetransition on@256",
            "anystatetransition on@32", "anystatetransition on@64", "anystatetransition@128", "anystatetransition@16",
            "anystatetransition@256", "anystatetransition@32", "anystatetransition@64", "assemblydefinitionasset icon",
            "assemblydefinitionreferenceasset icon", "asset store", "avatarblendbackground", "avatarselector",
            "blendkey", "blendkeyoverlay", "blendkeyselected", "blendsampler",
            "buildprofilewindow", "buildsettings.android on", "buildsettings.android.small", "buildsettings.broadcom",
            "buildsettings.dedicatedserver", "buildsettings.dedicatedserver.small", "buildsettings.editor.small", "buildsettings.embeddedlinux",
            "buildsettings.embeddedlinux.small", "buildsettings.facebook", "buildsettings.facebook.small", "buildsettings.gamecorescarlett",
            "buildsettings.gamecorescarlett.small", "buildsettings.gamecorexboxone", "buildsettings.gamecorexboxone on", "buildsettings.gamecorexboxone.small",
            "buildsettings.iphone", "buildsettings.iphone on", "buildsettings.iphone.small", "buildsettings.linux",
            "buildsettings.linux.small", "buildsettings.linuxheadlesssimulation", "buildsettings.linuxheadlesssimulation.small", "buildsettings.lumin",
            "buildsettings.lumin.small", "buildsettings.meta on", "buildsettings.meta.small", "buildsettings.metro on",
            "buildsettings.metro.small", "buildsettings.n3ds on", "buildsettings.n3ds.small", "buildsettings.osx.small",
            "buildsettings.ps4", "buildsettings.ps4 on", "buildsettings.ps4.small", "buildsettings.ps5",
            "buildsettings.ps5 on", "buildsettings.ps5.small", "buildsettings.psm.small", "buildsettings.psp2",
            "buildsettings.qnx", "buildsettings.qnx on", "buildsettings.qnx.small", "buildsettings.stadia on",
            "buildsettings.stadia.small", "buildsettings.standalone on", "buildsettings.standalone.small", "buildsettings.standalonebroadcom.small",
            "buildsettings.standalonegles20emu.small", "buildsettings.standaloneglesemu", "buildsettings.standaloneglesemu.small", "buildsettings.switch",
            "buildsettings.switch.small", "buildsettings.switch2 on", "buildsettings.switch2.small", "buildsettings.tvos",
            "buildsettings.tvos on", "buildsettings.tvos.small", "buildsettings.unityplay.small", "buildsettings.visionos",
            "buildsettings.visionos on.small", "buildsettings.visionos.small", "buildsettings.web.small", "buildsettings.webgl",
            "buildsettings.webgl.small", "buildsettings.windows.small", "buildsettings.wp8", "buildsettings.xbox360.small",
            "buildsettings.xboxone on", "buildsettings.xboxone.small", "builtin-selected-focused", "collabconflict",
            "collaberror", "collabprogress", "color", "datamode.authoring.sticky",
            "datamode.mixed", "datamode.mixed.sticky", "decalprojector", "decalprojector on",
            "decalprojector on@16", "decalprojector on@256", "decalprojector on@32", "decalprojector on@64",
            "decalprojector@16", "decalprojector@256", "decalprojector@32", "decalprojector@64",
            "disclight gizmo", "disclight icon", "editshape", "editshape on",
            "enter_subgraph", "enter_subgraph@128", "enter_subgraph@16", "enter_subgraph@32",
            "enter_subgraph@64", "eyedropper.sml", "false", "favorite",
            "favorite icon", "favorite_hover", "fog", "fog on",
            "fog on@128", "fog on@16", "fog on@256", "fog on@32",
            "fog@128", "fog@16", "fog@256", "fog@32",
            "fog@64", "font icon", "gameobject icon", "gizmostoggle",
            "head", "headik", "headzoom", "hidden@16",
            "hidden@32", "icon dropdown open", "import-available-selected-focused", "import-button",
            "importlogtype_error", "importlogtype_inactive", "importlogtype_warning", "inproject-selected-focused",
            "knobcshape", "knobcshapemini", "leftarm", "leftbracket",
            "leftfeetik", "leftfingers", "leftfingersik", "lefthandzoom",
            "lefthandzoomsilhouette", "leftleg", "lightingpreviewmode", "lightingproxy",
            "lightingproxy on", "lightingproxy on@16", "lightingproxy on@256", "lightingproxy on@32",
            "lightingproxy on@64", "lightingproxy@16", "lightingproxy@256", "lightingproxy@32",
            "lightingproxy@64", "lightingvisualization", "lightingvisualizationcolors", "loading",
            "loading@16", "loading@32", "loading_failed@32", "loading_failed@64",
            "locked-selected-focused", "lockicon", "lockicon-on", "materialvariant icon",
            "meshfilter icon", "metafile icon", "missing", "movetool",
            "myregistries", "networklobbymanager icon", "normalblenddistance", "onentertransition on@128",
            "onentertransition on@16", "onentertransition on@256", "onentertransition on@32", "onentertransition on@64",
            "onentertransition@128", "onentertransition@16", "onentertransition@256", "onentertransition@32",
            "onentertransition@64", "overlaymenu", "package", "packageerror",
            "packageoption", "packagewarning", "physx-logo", "planarreflections",
            "planarreflections on", "planarreflections on@128", "planarreflections on@16", "planarreflections on@256",
            "planarreflections on@32", "planarreflections on@64", "planarreflections@128", "planarreflections@16",
            "planarreflections@256", "planarreflections@32", "planarreflections@64", "playloopoff",
            "playloopon", "profiler.globalillumination", "registryerror-large", "restrictederror",
            "rightarm", "rightbracket", "rightfeetik", "rightfingers",
            "rightfingersik", "righthandzoom", "rigidbody icon", "rotatetool",
            "sample", "scaletool", "scenepicking_notpickable", "scenepicking_notpickable-mixed",
            "scenepicking_notpickable_hover", "scenepicking_pickable", "scenepicking_pickable-mixed", "scenepicking_pickable_hover",
            "sceneview2d", "scenevis_hidden", "scenevis_hidden-mixed", "scenevis_hidden-mixed_hover",
            "scenevis_hidden_hover", "scenevis_scene_hover", "scenevis_visible", "scenevis_visible-mixed",
            "scenevis_visible-mixed_hover", "scenevis_visible_hover", "scrollshadow", "search on icon",
            "searchdatabase icon", "selftransition", "selftransition on", "selftransition on@16",
            "selftransition on@256", "selftransition on@32", "selftransition on@64", "selftransition@128",
            "selftransition@16", "selftransition@256", "selftransition@32", "selftransition@64",
            "settings", "settingsicon", "shaded", "shadedwireframe",
            "shadedwireframe on", "socialnetworks.facebookshare", "socialnetworks.linkedinshare", "socialnetworks.tweet",
            "socialnetworks.udnopen", "statemachineasset@128", "statemachineasset@16", "statemachineasset@256",
            "statemachineasset@32", "statemachineeditor.state", "statemachineeditor.statehover", "statemachineeditor.stateselected",
            "statemachineeditor.statesub", "statemachineeditor.statesubhover", "statemachineeditor.statesubselected", "sv_icon_dot0_sml",
            "sv_icon_dot10_sml", "sv_icon_dot11_sml", "sv_icon_dot12_sml", "sv_icon_dot13_sml",
            "sv_icon_dot14_sml", "sv_icon_dot15_sml", "sv_icon_dot1_sml", "sv_icon_dot2_sml",
            "sv_icon_dot3_sml", "sv_icon_dot4_sml", "sv_icon_dot5_sml", "sv_icon_dot6_sml",
            "sv_icon_dot7_sml", "sv_icon_dot8_sml", "sv_icon_dot9_sml", "syncsearch",
            "tab_next", "tab_prev", "tableview", "tabtofilter",
            "terraininspector.terraintoolsplatalt", "toggleuvoverlay", "torso", "tranp",
            "tree_icon_branch", "treeeditor.branch", "treeeditor.branch on", "treeeditor.branchfreehand",
            "treeeditor.branchfreehand on", "treeeditor.branchrotate", "treeeditor.branchrotate on", "treeeditor.branchscale",
            "treeeditor.branchscale on", "treeeditor.branchtranslate", "treeeditor.branchtranslate on", "treeeditor.leafscale",
            "treeeditor.leafscale on", "treeeditor.leaftranslate on", "unity-assetstore-originals-logo-white", "unity.hierarchy.editor.hierarchywindow",
            "unityaboutlogo", "unityeditor.hierarchywindow", "unityeditor.historywindow", "unityplaylogo",
            "unityplaylogosmall", "unityregistry", "unlinked", "update-available",
            "variantarrow_active", "variantarrow_idle", "videoclipimporter icon", "viewtoolmove on",
            "viewtoolorbit", "viewtoolzoom", "visible@16", "visible@32",
            "waitspin01", "waitspin02", "waitspin03", "waitspin04",
            "waitspin05", "waitspin06", "waitspin07", "waitspin08",
            "waitspin09", "waitspin10", "waitspin11", "warning",
            "warning on", "warning on@128", "warning on@16", "warning on@256",
            "warning on@32", "warning@128", "warning@16", "warning@256",
            "warning@32"
        };

        /// <summary>
        /// Developer: show the excluded icons too (set by the Developer tools, which are not shipped;
        /// without them this stays off).
        /// </summary>
        internal static bool ShowExcluded;

        internal static void ResetIconLists()
        {
            _all = null;
            _recommended = null;
        }

        static List<string> _all;
        static HashSet<string> _allSet;

        /// <summary>True if the key is currently listed in All (excluded icons are not).</summary>
        public static bool InAll(string key)
        {
            if (_all == null) BuildAll();
            return _allSet.Contains(key);
        }

        /// <summary>
        /// Every built-in editor icon Unity ships (read from the editor resource bundle), one per name,
        /// matching the current skin, 1x only. Falls back to the curated lists if the bundle can't be read.
        /// </summary>
        public static IReadOnlyList<string> All
        {
            get
            {
                if (_all == null) BuildAll();
                return _all;
            }
        }

        static void BuildAll()
        {
            _all = new List<string>();
            var excluded = ShowExcluded ? null : new HashSet<string>(ExcludedNames);
            try
            {
                var m = typeof(EditorGUIUtility).GetMethod("GetEditorAssetBundle", BindingFlags.NonPublic | BindingFlags.Static);
                if (m?.Invoke(null, null) is AssetBundle bundle)
                {
                    bool pro = EditorGUIUtility.isProSkin;
                    var chosen = new Dictionary<string, string>(); // base name -> path
                    foreach (var path in bundle.GetAllAssetNames())
                    {
                        if (!path.StartsWith("icons/")) continue;
                        if (!(path.EndsWith(".png") || path.EndsWith(".asset"))) continue;
                        if (path.Contains("@2x") || path.Contains("@4x")) continue;
                        var file = Path.GetFileNameWithoutExtension(path);
                        bool dark = file.StartsWith("d_");
                        var baseName = dark ? file.Substring(2) : file;
                        if (!chosen.TryGetValue(baseName, out var existing)) chosen[baseName] = path;
                        else
                        {
                            bool existingDark = Path.GetFileNameWithoutExtension(existing).StartsWith("d_");
                            if (dark == pro && existingDark != pro) chosen[baseName] = path;
                        }
                    }
                    var names = new List<string>(chosen.Keys);
                    names.Sort(StringComparer.OrdinalIgnoreCase);
                    foreach (var n in names)
                        if (excluded == null || !excluded.Contains(n)) _all.Add(ResPrefix + chosen[n]);
                }
            }
            catch { _all.Clear(); }

            if (_all.Count == 0)
            {
                _all.AddRange(General);
                _all.AddRange(Scene);
                _all.AddRange(Gameplay);
            }
            _allSet = new HashSet<string>(_all);
        }

        static readonly Dictionary<GameObject, Texture> ComponentIconCache = new Dictionary<GameObject, Texture>();
        static readonly List<Component> TempComponents = new List<Component>();

        static EHIcons()
        {
            EditorApplication.hierarchyChanged += ComponentIconCache.Clear;
        }

        /// <summary>Icon of the most meaningful component (skips Transform / CanvasRenderer).</summary>
        public static Texture ComponentIcon(GameObject go)
        {
            if (ComponentIconCache.TryGetValue(go, out var tex)) return tex;
            tex = null;
            go.GetComponents(TempComponents);
            foreach (var c in TempComponents)
            {
                if (c == null || c is Transform || c is CanvasRenderer) continue;
                tex = EditorGUIUtility.ObjectContent(c, c.GetType()).image;
                if (tex != null) break;
            }
            TempComponents.Clear();
            ComponentIconCache[go] = tex;
            return tex;
        }

        public static Texture DefaultIcon(GameObject go) => EHCache.DefaultIcon(go);

        public static Texture Resolve(GameObject go, ObjectStyle style, bool componentAware)
        {
            Texture tex = null;
            if (style != null && style.iconMode == IconMode.Custom)
            {
                tex = Find(style.icon);
                if (tex == null) tex = ComponentIcon(go); // icon not in this Unity version: the component's icon instead
            }
            else if ((style != null && style.iconMode == IconMode.Auto) ||
                     (componentAware && (style == null || style.iconMode == IconMode.Default)))
                tex = ComponentIcon(go);
            return tex != null ? tex : DefaultIcon(go);
        }

        public static bool HasCustomIcon(ObjectStyle style, bool componentAware) =>
            componentAware || (style != null && (style.iconMode != IconMode.Default || style.iconDarken > 0f));
    }
}
