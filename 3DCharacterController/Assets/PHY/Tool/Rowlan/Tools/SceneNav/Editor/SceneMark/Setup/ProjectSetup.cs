namespace Rowlan.SceneMark
{
    /// <summary>
    /// Setup of the project. Contains constants for paths and folders.
    /// </summary>
    public static class ProjectSetup
    {
        /// <summary>
        /// Menu. In case you need a shortcut, use eg "%h" like ".../Quick Nav %h"
        /// </summary>
        public const string MENU = "Tools/Rowlan/SceneNav/SceneMark";
        public const int MENU_ORDER = 0;

        public const string WINDOW_TITLE = "SceneMark";

        public const string SETTINGS_FOLDER = "Assets/Rowlan/Tools/SceneNav Data";
        public const string SETTINGS_FILENAME = "SceneMarks.asset";

        public const string SETTINGS_SNAPSHOTS_FOLDER = SETTINGS_FOLDER + "/Snapshots";
    }
}