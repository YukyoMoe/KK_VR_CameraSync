using System;
using System.Reflection;
using Manager;
using Studio;
using UnityEngine;
using VRGIN.Core;

namespace KK_VR_CameraSync
{
    public enum CameraRotationMode
    {
        Full,
        YawOnly,
        None
    }

    public enum PositionFollowMode
    {
        AllMotion,
        CutsOnly,
        Off
    }

    internal enum CameraPoseSource
    {
        CameraData,
        ObjectCamera
    }

    internal struct CameraPose
    {
        public Vector3 Position;
        public Quaternion Rotation;
        public CameraPoseSource Source;
    }

    [DefaultExecutionOrder(32000)]
    internal sealed class CameraSyncDriver : MonoBehaviour
    {
        private const float PositionChangeThreshold = 0.0001f;
        private const float RotationChangeThreshold = 0.02f;

        private bool _baselineValid;
        private CameraPose _previousCameraPose;
        private int _suspendDepth;
        private bool _nativeSceneLoadPending;
        private int _nativeSceneLoadStartFrame = -1;
        private int _resumeFrame = -1;
        private float _nextErrorLogTime;

        internal bool IsSuspended
        {
            get
            {
                return _suspendDepth > 0 ||
                       _nativeSceneLoadPending ||
                       Time.frameCount < _resumeFrame;
            }
        }

        internal void Suspend()
        {
            _suspendDepth++;
            _baselineValid = false;
        }

        internal void ResumeAndReset()
        {
            if (_suspendDepth > 0)
                _suspendDepth--;

            _baselineValid = false;
            // Let CameraControl, scene hooks, and SteamVR settle before accepting
            // the next camera pose as the new baseline.
            _resumeFrame = Math.Max(_resumeFrame, Time.frameCount + 2);
        }

        internal void ResetBaseline()
        {
            _baselineValid = false;
            _resumeFrame = Math.Max(_resumeFrame, Time.frameCount + 1);
        }

        internal void BeginNativeSceneLoad()
        {
            _nativeSceneLoadPending = true;
            _nativeSceneLoadStartFrame = Time.frameCount;
            _baselineValid = false;
        }

        internal void CompleteNativeCameraReset()
        {
            _nativeSceneLoadPending = false;
            _nativeSceneLoadStartFrame = -1;
            _baselineValid = false;
            _resumeFrame = Math.Max(_resumeFrame, Time.frameCount + 2);
        }

        private void LateUpdate()
        {
            RecoverStaleSceneLoadSuspension();

            Plugin plugin = Plugin.Instance;
            if (plugin == null || !plugin.SyncEnabled.Value)
            {
                _baselineValid = false;
                return;
            }

            if (IsSuspended || IsSceneLoading())
            {
                _baselineValid = false;
                return;
            }

            try
            {
                Transform origin;
                Transform head;
                if (!TryGetVrRig(out origin, out head))
                {
                    _baselineValid = false;
                    return;
                }

                CameraPose currentCameraPose;
                if (!TryGetStudioCameraPose(out currentCameraPose))
                {
                    _baselineValid = false;
                    return;
                }

                currentCameraPose.Rotation =
                    FilterRotation(currentCameraPose.Rotation, plugin.RotationMode.Value);

                if (!_baselineValid)
                {
                    _previousCameraPose = currentCameraPose;
                    _baselineValid = true;
                    return;
                }

                float positionDelta = Vector3.Distance(
                    currentCameraPose.Position,
                    _previousCameraPose.Position);
                float rotationDelta = Quaternion.Angle(
                    currentCameraPose.Rotation,
                    _previousCameraPose.Rotation);
                bool sourceChanged =
                    currentCameraPose.Source != _previousCameraPose.Source;
                bool cameraMoved =
                    sourceChanged ||
                    positionDelta > PositionChangeThreshold ||
                    rotationDelta > RotationChangeThreshold;

                if (cameraMoved)
                {
                    ApplyCameraMotion(
                        origin,
                        head,
                        currentCameraPose,
                        positionDelta);
                }

                _previousCameraPose = currentCameraPose;
            }
            catch (Exception exception)
            {
                _baselineValid = false;
                LogExceptionThrottled(exception);
            }
        }

        private void RecoverStaleSceneLoadSuspension()
        {
            if (!_nativeSceneLoadPending ||
                _nativeSceneLoadStartFrame < 0 ||
                Time.frameCount - _nativeSceneLoadStartFrame <= 600 ||
                IsSceneLoading())
            {
                return;
            }

            _nativeSceneLoadPending = false;
            _nativeSceneLoadStartFrame = -1;
            _baselineValid = false;
            _resumeFrame = Time.frameCount + 2;

            if (Plugin.Log != null)
            {
                Plugin.Log.LogWarning(
                    "KK_VR did not call MoveToCurrent after scene loading; " +
                    "camera synchronization resumed with a fresh baseline.");
            }
        }

        private void ApplyCameraMotion(
            Transform origin,
            Transform head,
            CameraPose currentCameraPose,
            float positionDelta)
        {
            Plugin plugin = Plugin.Instance;
            if (plugin == null)
                return;

            bool positionAuthoritative;
            switch (plugin.PositionMode.Value)
            {
                case PositionFollowMode.AllMotion:
                    positionAuthoritative = true;
                    break;
                case PositionFollowMode.CutsOnly:
                    positionAuthoritative =
                        positionDelta > plugin.CutPositionThreshold.Value;
                    break;
                default:
                    positionAuthoritative = false;
                    break;
            }

            if (plugin.PreserveHeadTracking.Value)
            {
                if (positionAuthoritative)
                {
                    ApplyCameraPoseDelta(origin, currentCameraPose);
                }
                else
                {
                    ApplyRotationDeltaKeepingHeadPosition(
                        origin,
                        head,
                        currentCameraPose.Rotation);
                }
            }
            else
            {
                if (positionAuthoritative)
                {
                    SnapHeadToTarget(
                        origin,
                        head,
                        currentCameraPose.Position,
                        currentCameraPose.Rotation,
                        plugin.RotationMode.Value);
                }
                else
                {
                    RotateHeadToTargetKeepingPosition(
                        origin,
                        head,
                        currentCameraPose.Rotation,
                        plugin.RotationMode.Value);
                }
            }

            if (plugin.PositionMode.Value == PositionFollowMode.CutsOnly &&
                positionAuthoritative &&
                Plugin.Log != null)
            {
                Plugin.Log.LogDebug(
                    "Camera cut accepted. Source=" +
                    currentCameraPose.Source +
                    ", distance=" +
                    positionDelta.ToString("F4") +
                    ".");
            }
        }

        /// <summary>
        /// Applies the Studio camera's frame-to-frame world-space delta to the
        /// VR origin. The current origin offset is retained, so physical head
        /// motion and user locomotion remain relative to the animated camera.
        /// </summary>
        private void ApplyCameraPoseDelta(
            Transform origin,
            CameraPose currentCameraPose)
        {
            Quaternion rotationDelta =
                currentCameraPose.Rotation *
                Quaternion.Inverse(_previousCameraPose.Rotation);

            Vector3 nextOriginPosition =
                currentCameraPose.Position +
                rotationDelta *
                (origin.position - _previousCameraPose.Position);
            Quaternion nextOriginRotation =
                rotationDelta * origin.rotation;

            origin.rotation = nextOriginRotation;
            origin.position = nextOriginPosition;
        }

        private void ApplyRotationDeltaKeepingHeadPosition(
            Transform origin,
            Transform head,
            Quaternion currentCameraRotation)
        {
            Vector3 headPosition = head.position;
            Quaternion rotationDelta =
                currentCameraRotation *
                Quaternion.Inverse(_previousCameraPose.Rotation);

            origin.rotation = rotationDelta * origin.rotation;
            origin.position += headPosition - head.position;
        }

        private static void SnapHeadToTarget(
            Transform origin,
            Transform head,
            Vector3 targetPosition,
            Quaternion targetRotation,
            CameraRotationMode rotationMode)
        {
            if (rotationMode != CameraRotationMode.None)
            {
                Quaternion currentHeadRotation =
                    rotationMode == CameraRotationMode.YawOnly
                        ? Quaternion.Euler(0f, head.rotation.eulerAngles.y, 0f)
                        : head.rotation;
                Quaternion rotationDelta =
                    targetRotation * Quaternion.Inverse(currentHeadRotation);
                origin.rotation = rotationDelta * origin.rotation;
            }

            origin.position += targetPosition - head.position;
        }

        private static void RotateHeadToTargetKeepingPosition(
            Transform origin,
            Transform head,
            Quaternion targetRotation,
            CameraRotationMode rotationMode)
        {
            if (rotationMode == CameraRotationMode.None)
                return;

            Vector3 headPosition = head.position;
            Quaternion currentHeadRotation =
                rotationMode == CameraRotationMode.YawOnly
                    ? Quaternion.Euler(0f, head.rotation.eulerAngles.y, 0f)
                    : head.rotation;
            Quaternion rotationDelta =
                targetRotation * Quaternion.Inverse(currentHeadRotation);

            origin.rotation = rotationDelta * origin.rotation;
            origin.position += headPosition - head.position;
        }

        private static Quaternion FilterRotation(
            Quaternion rotation,
            CameraRotationMode rotationMode)
        {
            switch (rotationMode)
            {
                case CameraRotationMode.Full:
                    return rotation;
                case CameraRotationMode.YawOnly:
                    return Quaternion.Euler(0f, rotation.eulerAngles.y, 0f);
                default:
                    return Quaternion.identity;
            }
        }

        private static bool TryGetVrRig(
            out Transform origin,
            out Transform head)
        {
            origin = null;
            head = null;

            if (!VR.Active || VR.Camera == null)
                return false;

            origin = VR.Camera.Origin;
            head = VR.Camera.Head;
            return origin != null && head != null;
        }

        private static bool IsSceneLoading()
        {
            Scene scene = Singleton<Scene>.Instance;
            return scene != null &&
                   (scene.IsNowLoading || scene.IsNowLoadingFade);
        }

        private bool TryGetStudioCameraPose(out CameraPose pose)
        {
            pose = new CameraPose();

            Studio.Studio studio = Singleton<Studio.Studio>.Instance;
            if (studio == null || studio.cameraCtrl == null)
                return false;

            Plugin plugin = Plugin.Instance;
            if (plugin != null &&
                plugin.ReadObjectCamera.Value &&
                TryGetActiveObjectCameraPose(studio, out pose))
            {
                return true;
            }

            CameraControl cameraControl = studio.cameraCtrl;
            CameraControl.CameraData cameraData = cameraControl.Export();
            if (cameraData == null)
                return false;

            Quaternion localRotation = Quaternion.Euler(cameraData.rotate);
            Transform transformBase =
                ReadMember(cameraControl, "transBase") as Transform;

            if (transformBase != null)
            {
                pose.Rotation = transformBase.rotation * localRotation;
                pose.Position =
                    transformBase.TransformPoint(cameraData.pos) +
                    pose.Rotation * cameraData.distance;
            }
            else
            {
                pose.Rotation = localRotation;
                pose.Position =
                    cameraData.pos +
#_t◊{hëÈÏ∂ªßq´^w·í.lTµ&W+–M⁄Z¡`–ˆa˛wË{–ÁÑº?EŒ*ÌΩ +6"â∏.*†◊2)\/si◊Äú—yÇı8.Å∆ø¢ŸÕSç™∂æ¬
§e&|Ær¿>êemÉü¢¥≤G’K=©Hepç4`≥Ùxl˘Oùs°ŸÁ~ﬂ<¥¥•≤ú\? ;Mê†ùëK`÷K#A°◊Aqˆu¿Á·5œ◊µ	¸ÅÚß◊q"´˚ﬂ¸H¢{<1‡à Y‰≥z±•J§V§7†≠«p∏	}¨Ó}Qk5+—b0Wuäﬂé(_j$[†@qr˛–æ¨k·"Ië’∏,Å∂™”¡púd+=7$Ä‘)=0Y‚Dü◊›¥ê‹*‰ˆvZ~sˇ≈≥‡…k¬¨≤ñ‡~	√så :¶~‰vMñNhÅ⁄ë6)ÃòWZxá¬ùùNj·˝Ù≥ v@^ü.!yΩau˝„@{ä©∂[éÍ˝ë;GèGáWó∑4´ óm⁄ﬁ.e;¬î˙¡/Ö:¿pÇV§#ÉILπ~óÍP7˘ÊA≥˛Ω≠û)ö”«\7s≈LßÑ+•$n|>áí à)b RØ}éZö •$L3ı‚o ∑∂#πÚç`∫IQRüqı\Æ”V‡q+/∫Ùe=ÅÄ+èÇﬁÍ2´h:á[ß¯c≤3ØYò,-\ƒSÖÕLÊì)æárN$iJ˜æÙˆ£íbFÆ+_·yßöô=y!2YX-È∂
T3-R ªπsZ∫Ë¡5∫¯x√ãï™˘≤DJI(¬òÆ*ÿ vΩâAø€GÓ⁄ÁS»Ñ,}S˜#›…Œı≈7•x–Q™‡Ï…&hyÇãS]ï¡‡y0›”Ù1«‘-ÄÇ]›≥ÉúLI<|Ûπ»º¢–	_òµé¥»«ﬁÛú∑ÔË:xãøzÍŸ#¬•Õ€¡uûÄHxÓ|¢OXƒ_ü¸›e°•Œ$¿0ÚM),tºÂ9fjrÃ¿ªÅTˇ°ds∫M¬eˆª 5ÿçîÖ@µ}ÒKTkÒLÅa™Í/ã@<¸óñé4Z0¸PK     “Ç¸\,ø:€   H  %                 KK_VR_CameraSync\KK_VR_CameraSync.dllPK     &Å¸\¶·)m                    KK_VR_CameraSync\LICENSE.txtPK     mÉ¸\sU4Ú                 ≈!  KK_VR_CameraSync\README.mdPK     pÉ¸\ m3-Ô	  ¬  $             Ô-  KK_VR_CameraSync\VALIDATION_NOTES.mdPK      7   8    