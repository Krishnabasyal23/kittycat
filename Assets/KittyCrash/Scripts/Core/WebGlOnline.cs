#if UNITY_WEBGL && !UNITY_EDITOR
using UnityEngine;

namespace KittyCrash
{
    /// <summary>
    /// The web build is served by the Catino server itself (/play), so it plays online from the start,
    /// against the server that sent the page. The scene ships offline for the editor.
    /// </summary>
    static class WebGlOnline
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void GoOnline()
        {
            var rm = Object.FindFirstObjectByType<RoundManager>();
            if (rm != null && !rm.useApi) rm.SetUseApi(true);
        }
    }
}
#endif
