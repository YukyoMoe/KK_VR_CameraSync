using System;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;

namespace KK_VR_CameraSync
{
    [BepInProcess("CharaStudio")]
    [BepInPlugin(Guid, Name, Version)]
    [BepInDependency(
        "KKCharaStudioVRPlugin.KKCharaStudioVRPlugin",
        BepInDependency.DependencyFlags.SoftDependency)]
    public sealed class Plugin : BaseUnityPlugin
    {
        public const string Guid = "yukyo.kkvr.camerasync";
        public const string Name = "KK VR Camera Sync";
        public const string Version = "0.1.5";

        internal static Plugin Instance { get; private set; }
        internal static ManualLogSource Log { get; private set; }

        internal ConfigEntry<bool> SyncEnabled { get; private set; }
        internal ConfigEntry<bool> PreserveHeadTracking { get; private set; }
        internal ConfigEntry<bool> AlignInitialStudioCamera { get; private set; }
        internal ConfigEntry<CameraRotationMode> InitialAlignmentRotationMode { get; private set; }
        internal ConfigEntry<CameraRotationMode> RotationMode { get; private set; }
        internal ConfigEntry<PositionFollowMode> PositionMode { get; private set; }
        internal ConfigEntry<float> CutPositionThreshold { get; private set; }
        internal ConfigEntry<bool> ReadObjectCamera { get; private set; }
        internal ConfigEntry<KeyboardShortcut> ToggleShortcut { get; private set; }
        internal ConfigEntry<int> ConfigRevision { get; private set; }

        internal CameraSyncDriver Driver { get; private set; }

        private Harmony _harmony;
        private bool _cameraHelperPatchesInstalled;
        private float _nextCameraHelperPatchAttemptTime;
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

            AlignInitialStudioCamera = Config.Bind(
                "General",
                "Align initial Studio camera",
                true,
                "Align the headset once to the scene card's initial Studio camera after loading.");

            InitialAlignmentRotationMode = Config.Bind(
                "General",
                "Initial alignment rotation mode",
                CameraRotationMode.YawOnly,
                "YawOnly matches Ermin KK_VR's upright origin; Full may be normalized by KK_VR; None aligns position only.");

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

            ConfigRevision = Config.Bind(
                "Internal",
                "Config revision",
                0,
                "Internal migration marker. Do not edit.");

            if (ConfigRevision.Value < 2)
            {
                // v0.1.2 defaulted to Full, but Ermin KK_VR normalizes the
                // tracking origin back to yaw-only after our alignment. That
                // second normalization changes both the HMD angle and position.
                if (InitialAlignmentRotationMode.Value == CameraRotationMode.Full)
                    InitialAlignmentRotationMode.Value = CameraRotationMode.YawOnly;

                ConfigRevision.Value = 2;
            }

            Driver = gameObject.AddComponent<CameraSyncDriver>();

            try
            {
                _harmony = new Harmony(Guid);
                _harmony.PatchAll(typeof(NativeLoadScenePatch));
                _harmony.PatchAll(typeof(NativeImportScenePatch));
                TryInstallCameraHelperPatches();
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
            if (!_cameraHelperPatchesInstalled &&
                Time.unscaledTime >= _nextCameraHelperPatchAttemptTime)
            {
                _nextCameraHelperPatchAttemptTime = Time.unscaledTime + 1f;
                TryInstallCameraHelperPatches();
            }

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

        private void TryInstallCameraHelperPatches()
        {
            if (_harmony == null || _cameraHelperPatchesInstalled)
                return;

            if (PatchTargets.FindCameraHelperMethod("MoveToCurrent") == null ||
                PatchTargets.FindCameraHelperMethod("CurrentToCameraCtrl") == null)
            {
                return;
            }

            try
            {
                _harmony.PatchAll(typeof(NativeMoveToCurrentPatch));
                _harmony.PatchAll(typeof(NativeCurrentToCameraCtrlPatch));
                _cameraHelperPatchesInstalled = true;
                Logger.LogInfo(
                    "Ermin KKCharaStudioVR camera helper compatibility patches installed.");
            }
            catch (Exception exception)
            {
                Logger.LogWarning(
                    "Ermin KKCharaStudioVR camera helper patches are not ready yet: " +
                    exception.Message);
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
                    _harmony.UnpatchSelf();
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
