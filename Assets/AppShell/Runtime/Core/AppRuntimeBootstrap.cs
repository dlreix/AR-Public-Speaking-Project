using UnityEngine;

namespace VRPublicSpeaking.AppShell.Core
{
    public static class AppRuntimeBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void EnsureRuntimeState()
        {
            AppRuntimeState.GetOrCreate();
        }
    }
}
