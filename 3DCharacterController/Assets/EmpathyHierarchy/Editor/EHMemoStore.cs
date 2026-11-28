#if UNITY_6000_0_OR_NEWER
using System.Collections.Generic;
using UnityEditor;

namespace EmpathyHierarchy
{
    /// <summary>
    /// Memos live in ProjectSettings so the whole team shares them through version control.
    /// (Own file: Unity links the saved data to the script by its file, which must carry the class name.)
    /// </summary>
    [FilePath("ProjectSettings/EmpathyHierarchy/Memos.asset", FilePathAttribute.Location.ProjectFolder)]
    public class EHMemoStore : ScriptableSingleton<EHMemoStore>
    {
        public List<MemoEntry> entries = new List<MemoEntry>();

        /// <summary>Checks the saved file (backup, script link) before it is read.</summary>
        public new static EHMemoStore instance
        {
            get { EHDataGuard.Ensure(); return ScriptableSingleton<EHMemoStore>.instance; }
        }

        public void SaveNow()
        {
            System.IO.Directory.CreateDirectory("ProjectSettings/EmpathyHierarchy");
            Save(true);
        }
    }
}
#endif
