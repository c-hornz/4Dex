using UnityEngine;
using UnityEngine.XR;

namespace FourDex.XR
{
    /// <summary>
    /// Head pose from Unity's XR input subsystem, which OpenXR feeds. Runtime-agnostic: nothing here is Oculus-specific.
    /// </summary>
    public sealed class XRNodeHmdPoseSource : IHmdPoseSource
    {
        private InputDevice _head;

        public bool TryGetPose(out HmdPose pose)
        {
            // The device handle goes stale when the headset reconnects or the runtime restarts, so re-acquire lazily.
            if (!_head.isValid)
                _head = InputDevices.GetDeviceAtXRNode(XRNode.Head);

            // Centre-eye rather than device pose: it is the point the stereo eye offsets are measured from.
            if (_head.isValid &&
                _head.TryGetFeatureValue(CommonUsages.centerEyePosition, out Vector3 position) &&
                _head.TryGetFeatureValue(CommonUsages.centerEyeRotation, out Quaternion rotation))
            {
                _head.TryGetFeatureValue(CommonUsages.isTracked, out bool tracked);
                pose = new HmdPose(position, rotation, tracked);
                return true;
            }

            pose = default;
            return false;
        }
    }
}
