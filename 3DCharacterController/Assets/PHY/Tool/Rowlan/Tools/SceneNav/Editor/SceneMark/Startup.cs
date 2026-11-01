using UnityEditor;

namespace Rowlan.SceneMark
{
    /// <summary>
    /// Indicate whether startup action has been performed or not
    /// 
    /// Using session state which survives a domain reload.
    /// </summary>
    public class Startup
    {
        private static Startup _instance;

        public static Startup Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = new Startup();
                }

                return _instance;
            }
        }

        const string k_InitializedKey = "Rowlan.SceneMark.Startup.Initialized";

        public bool Initialized
        {
            get
            {
                return SessionState.GetBool(k_InitializedKey, false);
            }

            set
            {
                SessionState.SetBool(k_InitializedKey, value);
            }

        }

    }
}