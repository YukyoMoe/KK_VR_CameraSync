using System;
using System.Reflection;
using HarmonyLib;
using Studio;

namespace KK_VR_CameraSync
{
    /// <summary>
    /// The companion plugin deliberately resolves KK_VR types by name. This keeps
    /// the build independent from KKCharaStudioVRPlugin.dll and allows a clear
    /// warning when a future KK_VR release renames a target.
    /// </summary>
    internal static class PatchTargets
    {
        internal static MethodBase FindCameraHelperMethod(string methodName)
        {
            Type helper = null;
            Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
            for (int i = 0; i < assemblies.Length && helper == null; i++)
            {
                try
                {
                    helper = assemblies[i].GetType(
                        "KKCharaStudioVR.VRCameraMoveHelper",
                        false);
                }
                catch
                {
                    // A partially loaded optional assembly should not prevent
                    // discovery in the remaining assemblies.
                }
            }

            if (helper == null)
                return null;

            return helper.GetMethod(
                methodName,
                BindingFlags.Instance |
                BindingFlags.Public |
                BindingFlags.NonPublic,
                null,
                Type.EmptyTypes,
                null);
        }
    }

    [HarmonyPatch]
    internal static class NativeMoveToCurrentPatch
    {
        private static MethodBase TargetMethod()
        {
            return PatchTargets.FindCameraHelperMethod("MoveToCurrent");
        }

        [HarmonyPostfix]
        private static void Postfix()
        {
            Plugin plugin = Plugin.Instance;
            if (plugin != null && plugin.Driver != null)
                plugin.Driver.CompleteNativeCameraReset();
        }
    }

    [HarmonyPatch]
    internal static class NativeCurrentToCameraCtrlPatch
    {
        private static MethodBase TargetMethod()
        {
            return PatchTargets.FindCameraHelperMethod("CurrentToCameraCtrl");
        }

        [HarmonyPrefix]
        private static void Prefix()
        {
            Plugin plugin = Plugin.Instance;
            if (plugin != null && plugin.Driver != null)
                plugin.Driver.Suspend();
        }

        [HarmonyPostfix]
        private static void Postfix()
        {
            Plugin plugin = Plugin.Instance;
            if (plugin != null && plugin.Driver != null)
                plugin.Driver.ResumeAndReset();
        }
    }

    [HarmonyPatch(
        typeof(Studio.Studio),
        "LoadScene",
        new Type[] { typeof(string) },
        null)]
    internal static class NativeLoadScenePatch
    {
        [HarmonyPrefix]
        private static void Prefix()
        {
            Plugin plugin = Plugin.Instance;
            if (plugin != null && plugin.Driver != null)
                plugin.Driver.BeginNativeSceneLoad();
        }

        [HarmonyPostfix]
        private static void Postfix(bool __result)
        {
            Plugin plugin = Plugin.Instance;
            if (plugin != null && plugin.Driver != null)
                plugin.Driver.CompleteNativeSceneLoad(__result);
        }
    }

    [HarmonyPatch(
        typeof(Studio.Studio),
        "ImportScene",
        new Type[] { typeof(string) },
        null)]
    internal static class NativeImportScenePatch
    {
        [HarmonyPrefix]
        private static void Prefix()
        {
            Plugin plugin = Plugin.Instance;
            if (plugin != null && plugin.Driver != null)
                plugin.Driver.BeginNativeSceneLoad();
        }

        [HarmonyPostfix]
        private static void Postfix(bool __result)
        {
            Plugin plugin = Plugin.Instance;
            if (plugin != null && plugin.Driver != null)
                plugin.Driver.CompleteNativeSceneLoad(__result);
        }
    }
}
