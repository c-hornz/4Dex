using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;

namespace FourDex.XR
{
    /// <summary>
    /// Reads the head pose and fans it out to two independent consumers (ARCHITECTURE §3):
    /// the stereo camera, which gets the pose unmodified, and <see cref="PoseSampled"/>, which the 4D mapping
    /// system subscribes to. The mapping may change how geometry is projected; it never touches the camera.
    /// </summary>
    /// <remarks>
    /// Put this on the tracking-space root with the camera as a child. Do not add a TrackedPoseDriver:
    /// this component replaces it, and two writers to one camera transform would fight.
    /// </remarks>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-100)] // Sample before gameplay and perception scripts read LastPose this frame.
    public sealed class XRRig : MonoBehaviour
    {
        [SerializeField] private Camera? _camera;

        [Tooltip("Floor puts tracking-space y = 0 on the physical floor, so standing height is real.")]
        [SerializeField] private TrackingOriginModeFlags _trackingOrigin = TrackingOriginModeFlags.Floor;

        private bool _originApplied;

        /// <summary>
        /// Raised once per frame with that frame's pose, after the camera has already been updated.
        /// For the 4D mapping system and debug displays. Consumers must not move the camera.
        /// </summary>
        public event Action<HmdPose>? PoseSampled;

        public Camera? Camera => ResolveCamera();

        /// <summary>Defaults to the XR input subsystem. Replace to inject poses, e.g. in tests.</summary>
        public IHmdPoseSource PoseSource { get; set; } = new XRNodeHmdPoseSource();

        public HmdPose LastPose { get; private set; } = new HmdPose(Vector3.zero, Quaternion.identity, false);

        public bool HasPose { get; private set; }

        private void Awake()
        {
            if (ResolveCamera() == null)
                Debug.LogError("[4Dex] XRRig needs a child Camera.", this);
        }

        private void OnEnable() => Application.onBeforeRender += RefreshCameraLate;

        private void OnDisable() => Application.onBeforeRender -= RefreshCameraLate;

        private void Update()
        {
            if (!_originApplied)
                _originApplied = TryApplyTrackingOrigin(_trackingOrigin);
            SampleAndApply();
        }

        /// <summary>
        /// Sample the pose, move the camera, then notify consumers, in that order. Because the camera is
        /// written first, a consumer that throws cannot cost head tracking (SPEC §25).
        /// </summary>
        public void SampleAndApply()
        {
            // No pose at all (no headset, runtime not ready): hold the last camera pose rather than snapping to origin.
            if (!PoseSource.TryGetPose(out HmdPose pose))
                return;

            LastPose = pose;
            HasPose = true;
            ApplyToCamera(pose);
            PoseSampled?.Invoke(pose);
        }

        // Re-sample just before rendering, as TrackedPoseDriver does: it cuts motion-to-photon latency.
        // Camera only; the 4D mapping keeps this frame's Update sample, so it stays one consistent value per frame.
        private void RefreshCameraLate()
        {
            if (PoseSource.TryGetPose(out HmdPose pose))
                ApplyToCamera(pose);
        }

        private void ApplyToCamera(HmdPose pose)
        {
            Camera? camera = ResolveCamera();
            if (camera != null)
                camera.transform.SetLocalPositionAndRotation(pose.Position, pose.Rotation);
        }

        // Lazy, not only in Awake: Awake does not run in edit mode, where the rig's tests run.
        private Camera? ResolveCamera()
        {
            if (_camera == null)
                _camera = GetComponentInChildren<Camera>();
            return _camera;
        }

        private static bool TryApplyTrackingOrigin(TrackingOriginModeFlags mode)
        {
            var subsystems = new List<XRInputSubsystem>();
            SubsystemManager.GetSubsystems(subsystems);
            bool applied = false;
            foreach (XRInputSubsystem subsystem in subsystems)
            {
                if (subsystem.running && subsystem.TrySetTrackingOriginMode(mode))
                    applied = true;
            }
            return applied;
        }
    }
}
