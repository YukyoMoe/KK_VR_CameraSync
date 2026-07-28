using System;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using Harmony;
using UnityEngine;

namespace KK_VR_CameraSync
{
    [BepInProcess("CharaStudio")]
    [BepInPlugin(Guid, Name, Version)]
    [BepInDependency(
        "KKCharaStudioVRPlugin.KKCharaStudioVRPlugin",
        BepInDependency.DependencyFlags.HardDependency)]
    public sealed class Plugin : BaseUnityPlugin
    {
        public const string Guid = "yukyo.kkvr.camerasync";
        public const string Name = "KK VR Camera Sync";
        public const string Version = "0.1.0";

        internal static Plugin Instance { get; private set; }
        internal static ManualLogSource Log { get; private set; }

        internal ConfigEntry<bool> SyncEnabled { get; private set; }
        internal ConfigEntry<bool> PreserveHeadTracking { get; private set; }
        internal ConfigEntry<CameraRotationMode> RotationMode { get; private set; }
        internal ConfigEntry<PositionFollowMode> PositionMode { get; private set; }
        internal ConfigEntry<float> CutPositionThreshold { get; private set; }
        internal ConfigEntry<bool> ReadObjectCamera { get; private set; }
        internal ConfigEntry<KeyboardShortcut> ToggleShortcut { get; private set; }

        internal CameraSyncDriver Driver { get; private set; }

        private HarmonyInstance _harmony;
        private bool _applicationQuitting;

        private void Awake()
        {
            Instance = this;
            Log = Logger;

            SyncEnabled = Config.Bind(
                "General",
                "Enabled",
                true,
                "Follow final Studio camera motion. No Timeline dependency is required.");

            PreserveHeadTracking = Config.Bind(
                "General",
                "Preserve head tracking",
                true,
                "Apply camera deltas to the VR origin while preserving the headset's relative pose.");

            RotationMode = Config.Bind(
                "General",
                "Rotation follow mode",
                CameraRotationMode.YawOnly,
                "Full follows pitch/yaw/roll; YawOnly follows horizontal rotation; None disables rotation following.");

            PositionMode = Config.Bind(
                "General",
                "Position follow mode",
                PositionFollowMode.AllMotion,
                "AllMotion follows all translation; CutsOnly follows only adjacent-frame cuts; Off keeps the headset position.");

            CutPositionThreshold = Config.Bind(
                "Cut detection",
                "Position threshold",
                2f,
                new ConfigDescription(
                    "Adjacent-frame world-space distance required by CutsOnly.",
                    new AcceptableValueRange<float>(0.01f, 100f)));

            ReadObjectCamera = Config.Bind(
                "Compatibility",
                "Read active OCICamera",
                true,
                "Prefer the active Studio camera object when the current KK Assembly-CSharp exposes one.");

            ToggleShortcut = Config.Bind(
                "Keyboard",
                "Toggle sync",
                new KeyboardShortcut(KeyCode.None),
                "Optional shortcut for enabling or disabling camera synchronization.");

            Driver = gameObject.AddComponent<CameraSyncDriver>();

            try
            {
                _harmony = HarmonyInstance.Create(Guid);
                _harmony.PatchAll(typeof(NativeMoveToCurrentPatch));
                _harmony.PatchAll(typeof(NativeCurrentToCameraCtrlPatch));
                _harmony.PatchAll(typeof(NativeLoadScenePatch));
                _harmony.PatchAll(typeof(NativeImportScenePatch));
            }
            catch (Exception exception)
            {
                Logger.LogWarning(
                    "Compatibility patches could not be installed. " +
                    "Continuous following remains available, but native camera resets may require toggling sync. " +
                    exception);
            }

            Logger.LogInfo(
                "Loaded v" + Version +
                ". Generic Studio camera observation is enabled; Timeline is not a dependency.");
        }

        private void Update()
        {
            if (ToggleShortcut.Value.IsDown())
            {
                SyncEnabled.Value = !SyncEnabled.Value;
                if (Driver != null)
                    Driver.ResetBaseline();

                Logger.LogInfo(
                    "Camera synchronization " +
                    (SyncEnabled.Value ? "enabled." : "disabled."));
            }
        }

        private void OnApplicationQuit()
        {
            _applicationQuitting = true;
        }

        private void OnDestroy()
        {
            // Rebuilding Harmony wrappers while the process is shutting down can
            // stall old Mono runtimes. The process will discard patches naturally.
            if (!_applicationQuitting && _harmony != null)
            {
                try
                {
                    _harmony.UnpatchAll(Guid);
                }
                catch
                {
                    // Best-effort cleanup only.
                }
            }

            if (ReferenceEquals(Instance, this))
                Instance = null;
        }
    }
}
