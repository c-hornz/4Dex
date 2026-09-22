using UnityEngine;

namespace FourDex.XR
{
    /// <summary>
    /// A stand-in head for working without a headset: standing eye height, with yaw and pitch driven by
    /// the desktop test mode. It goes through the same <see cref="IHmdPoseSource"/> seam as the real HMD,
    /// so the camera path under test is the one the headset uses.
    /// </summary>
    public sealed class SimulatedHmdPoseSource : IHmdPoseSource
    {
        private const float MaxPitch = 89f;

        public SimulatedHmdPoseSource(float eyeHeight = 1.6f) => EyeHeight = eyeHeight;

        public float EyeHeight { get; set; }

        /// <summary>Degrees; positive turns right (Unity yaw).</summary>
        public float Yaw { get; private set; }

        /// <summary>Degrees; positive looks down (Unity pitch). Clamped short of straight up/down.</summary>
        public float Pitch { get; private set; }

        public Quaternion Rotation => Quaternion.Euler(Pitch, Yaw, 0f);

        /// <summary>Head yaw only, for head-relative walking that does not dip when looking down.</summary>
        public Quaternion YawRotation => Quaternion.Euler(0f, Yaw, 0f);

        public void Look(float deltaYaw, float deltaPitch)
        {
            Yaw = Mathf.Repeat(Yaw + deltaYaw + 180f, 360f) - 180f;
            Pitch = Mathf.Clamp(Pitch + deltaPitch, -MaxPitch, MaxPitch);
        }

        public void Recenter()
        {
            Yaw = 0f;
            Pitch = 0f;
        }

        public bool TryGetPose(out HmdPose pose)
        {
            // Always "tracked": the simulation never loses the head. The HUD labels desktop mode separately.
            pose = new HmdPose(new Vector3(0f, EyeHeight, 0f), Rotation, true);
            return true;
        }
    }
}
