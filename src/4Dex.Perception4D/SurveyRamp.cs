using System;

namespace FourDex.Perception
{
    /// <summary>
    /// The survey amount over time (SPEC §10: a <i>temporary</i> mode). Engaging ramps it toward 1 and releasing ramps it
    /// back to 0, eased, so neighbouring W geometry fades in and out instead of popping.
    /// </summary>
    public sealed class SurveyRamp
    {
        private float _linear;

        /// <summary>Eased amount, 0 normal to 1 full survey: what the perception model is given.</summary>
        public float Amount => _linear * _linear * (3f - 2f * _linear);

        public bool Engaged { get; set; }

        public void Update(float dt, float rampSeconds)
        {
            float target = Engaged ? 1f : 0f;
            if (rampSeconds <= 0f)
            {
                _linear = target;
                return;
            }
            float step = MathF.Max(dt, 0f) / rampSeconds;
            _linear = _linear < target ? MathF.Min(_linear + step, target) : MathF.Max(_linear - step, target);
        }

        /// <summary>Jump straight to an amount, for snapshots and tests.</summary>
        public void Force(float amount)
        {
            _linear = amount < 0f ? 0f : amount > 1f ? 1f : amount;
            Engaged = _linear > 0f;
        }
    }
}
