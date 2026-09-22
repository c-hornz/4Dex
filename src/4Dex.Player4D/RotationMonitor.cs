using System;
using FourDex.Math4D;

namespace FourDex.Player
{
    /// <summary>
    /// SPEC §20's rotation monitor: the player's orientation as six plane angles, and how fast each plane is
    /// turning. Rates are measured from successive orientations, not from input, so they report what actually
    /// happened whatever drove it: keyboard today, weighted head mappings in Milestone 4.
    /// </summary>
    public sealed class RotationMonitor
    {
        private Rotor4? _previous;
        private Bivector4 _rate = Bivector4.Zero;

        /// <param name="smoothing">
        /// Seconds for the displayed rate to settle: an exponential moving average, so a single frame's jitter
        /// does not make the numbers unreadable. 0 shows the raw per-sample rate.
        /// </param>
        public RotationMonitor(float smoothing = 0.15f) => Smoothing = MathF.Max(smoothing, 0f);

        public float Smoothing { get; }

        /// <summary>Orientation as simultaneous world-plane angles, in radians (the rotor's log).</summary>
        public Bivector4 Orientation { get; private set; } = Bivector4.Zero;

        /// <summary>
        /// Rotation rate in radians per second, in the player's own planes: the planes the rotation controls act in.
        /// </summary>
        public Bivector4 Rate => _rate;

        public void Sample(Rotor4 orientation, float dt)
        {
            Orientation = orientation.Log();
            if (_previous is Rotor4 previous && dt > 0f)
            {
                // RotatedLocal composes current = delta.Then(previous), so delta = current.Then(previous^-1): the
                // step expressed in the player's own planes.
                Bivector4 step = orientation.Then(previous.Inverse()).Log();
                Bivector4 raw = step * (1f / dt);
                float blend = Smoothing > 0f ? 1f - MathF.Exp(-dt / Smoothing) : 1f;
                _rate = _rate + (raw + _rate * -1f) * blend;
            }
            _previous = orientation;
        }

        public void Reset()
        {
            _previous = null;
            _rate = Bivector4.Zero;
            Orientation = Bivector4.Zero;
        }
    }
}
