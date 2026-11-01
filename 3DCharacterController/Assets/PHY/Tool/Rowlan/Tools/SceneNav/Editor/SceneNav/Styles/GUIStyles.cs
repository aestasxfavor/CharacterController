using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Rowlan.SceneNav
{
    public class GUIStyles
    {
        private static GUIStyle _appTitleBoxStyle;
        public static GUIStyle AppTitleBoxStyle
        {
            get
            {
                if (_appTitleBoxStyle == null)
                {
                    _appTitleBoxStyle = new GUIStyle("helpBox");
                    _appTitleBoxStyle.fontStyle = FontStyle.Bold;
                    _appTitleBoxStyle.fontSize = 16;
                    _appTitleBoxStyle.alignment = TextAnchor.MiddleCenter;
                }
                return _appTitleBoxStyle;
            }
        }

        private static GUIStyle _boxTitleStyle;
        public static GUIStyle BoxTitleStyle
        {
            get
            {
                if (_boxTitleStyle == null)
                {
                    _boxTitleStyle = new GUIStyle("Label");
                    _boxTitleStyle.fontStyle = FontStyle.BoldAndItalic;
                }
                return _boxTitleStyle;
            }
        }

        private static GUIStyle _helpBoxStyle;
        public static GUIStyle HelpBoxStyle
        {
            get
            {
                if (_helpBoxStyle == null)
                {
                    _helpBoxStyle = new GUIStyle("helpBox");
                    _helpBoxStyle.fontStyle = FontStyle.Bold;
                }
                return _helpBoxStyle;
            }
        }

        private static GUIStyle _groupTitleStyle;
        public static GUIStyle GroupTitleStyle
        {
            get
            {
                if (_groupTitleStyle == null)
                {
                    _groupTitleStyle = new GUIStyle("Label");
                    _groupTitleStyle.fontStyle = FontStyle.Bold;
                }
                return _groupTitleStyle;
            }
        }

        private static GUIStyle _groupBoxStyle;
        public static GUIStyle GroupBoxStyle
        {
            get
            {
                if (_groupBoxStyle == null)
                {
                    _groupBoxStyle = new GUIStyle("helpBox");
                }
                return _groupBoxStyle;
            }
        }

        private static GUIStyle _previewLabelStyle;
        public static GUIStyle PreviewLabelStyle
        {
            get
            {
                if (_previewLabelStyle == null)
                {
                    _previewLabelStyle = new GUIStyle(GUI.skin.label);
                    _previewLabelStyle.alignment = TextAnchor.LowerCenter;
                    _previewLabelStyle.fontStyle = FontStyle.Bold;
                }
                return _previewLabelStyle;
            }
        }

    }
}