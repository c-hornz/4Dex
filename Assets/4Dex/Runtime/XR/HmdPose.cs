using UnityEngine;

namespace FourDex.XR
{
    /// <summary>
    /// One sample of the physical head pose, in tracking space (floor origin). Pure 3D and physical:
    /// nothing 4D has been applied to it, and nothing ever should be before it reaches the camera.
    /// </summary>
    public readonly struct HmdPose
    {
        public readonly Vector3 Position;
        public readonly Quaternion Rotation;

        /// <summary>False while the runtime is only predicting, e.g. the headset is out of sensor view.</summary>
        public readonly bool IsTracked;

        public HmdPose(Vector3 position, Quaternion rotation, bool isTracked)
        {
            Position = position;
            Rotation = rotation;
            IsTracked = isTracked;
        }

        public override string ToString() => $"{Position} {Rotation.eulerAngles} tracked={IsTracked}";
    }

    /// <summary>
    /// Where head poses come from. The rig depends on this rather than on an XR API so the device layer
    /// stays swappable (SPEC §2) and tests can inject poses.
    /// </summary>
    public interface IHmdPoseSource
    {
        /// <summary>False when no headset pose is available at all; the rig then leaves the camera where it was.</summary>
        bool TryGetPose(out HmdPose pose);
    }
}
