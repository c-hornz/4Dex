using System;

namespace FourDex.Player
{
    /// <summary>
    /// Frame times for the HUD: a smoothed figure to read, and the worst frame in a rolling window, which is what a
    /// person actually feels. A mean of 11 ms with a 40 ms spike every second is not a smooth 90 Hz.
    /// </summary>
    /// <remarks>
    /// In a headset the editor's own profiler windows are not visible, so the frame cost has to be readable from
    /// inside the experience, beside the rest of the SPEC §20 instrumentation.
    /// </remarks>
    public sealed class FrameMonitor
    {
        private readonly float _windowSeconds;
        private float _smoothedMs;
        private float _worstMs;
        private float _windowWorstMs;
        private float _windowElapsed;

        public FrameMonitor(float windowSeconds = 1f) => _windowSeconds = MathF.Max(windowSeconds, 0.1f);

        /// <summary>Exponentially smoothed frame time, in milliseconds.</summary>
        public float MeanMs => _smoothedMs;

        /// <summary>Worst frame in the last completed window, in milliseconds.</summary>
        public float WorstMs => _worstMs;

        public float Fps => _smoothedMs > 1e-4f ? 1000f / _smoothedMs : 0f;

        public void Sample(float deltaSeconds)
        {
            if (!(deltaSeconds > 0f) || float.IsInfinity(deltaSeconds))
                return;

            float ms = deltaSeconds * 1000f;
            // Roughly a quarter-second of history: steady enough to read, quick enough to respond to a change.
            _smoothedMs = _smoothedMs <= 0f ? ms : _smoothedMs + (ms - _smoothedMs) * 0.1f;

            _windowWorstMs = MathF.Max(_windowWorstMs, ms);
            _windowElapsed += deltaSeconds;
            if (_windowElapsed < _windowSeconds)
                return;

            // Report the window just finished, so the worst figure is recent rather than all-time: an old spike
            // should not keep accusing a frame rate that has since recovered.
            _worstMs = _windowWorstMs;
            _windowWorstMs = 0f;
            _windowElapsed = 0f;
        }

        public void Reset()
        {
            _smoothedMs = 0f;
            _worstMs = 0f;
            _windowWorstMs = 0f;
            _windowElapsed = 0f;
        }
    }
}
