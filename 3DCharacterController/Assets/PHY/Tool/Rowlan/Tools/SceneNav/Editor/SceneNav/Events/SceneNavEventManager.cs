namespace Rowlan.SceneNav
{
    public class SceneNavEventManager
    {

        // delegate
        public delegate void SceneNavZoomDelegate(ZoomEvent zoomEvent);

        // events
        public static event SceneNavZoomDelegate OnZoom;

        public static void FireZoomEvent(ZoomEvent zoomEvent)
        {
            if (OnZoom == null)
                return;

            OnZoom(zoomEvent);
        }
    }
}