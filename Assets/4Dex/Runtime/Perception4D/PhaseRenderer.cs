using FourDex.Perception;
using UnityEngine;

namespace FourDex.Runtime
{
    /// <summary>
    /// Hands one object's perception channels to its Phase4D material through a MaterialPropertyBlock, so objects
    /// share one material and each still shows its own W state. Hides the renderer in the OUTSIDE band (SPEC §11).
    /// </summary>
    /// <remarks>Driven by ProjectionDriver after projection; has no Update of its own.</remarks>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Object4DBehaviour))]
    public sealed class PhaseRenderer : MonoBehaviour
    {
        private static readonly int DensityId = Shader.PropertyToID("_Density");
        private static readonly int CoherenceId = Shader.PropertyToID("_Coherence");
        private static readonly int PhaseFrequencyId = Shader.PropertyToID("_PhaseFrequency");
        private static readonly int PhaseVelocityId = Shader.PropertyToID("_PhaseVelocity");
        private static readonly int PhaseAmplitudeId = Shader.PropertyToID("_PhaseAmplitude");
        private static readonly int TintId = Shader.PropertyToID("_Tint");
        private static readonly int EdgeWeightId = Shader.PropertyToID("_EdgeWeight");
        private static readonly int ShimmerRateId = Shader.PropertyToID("_ShimmerRate");

        [Tooltip("Empty: the Renderer on this GameObject.")]
        [SerializeField] private Renderer? _renderer;

        private MaterialPropertyBlock? _block;

        public Renderer? Target => _renderer != null ? _renderer : _renderer = GetComponent<Renderer>();

        public void Apply(in PerceptionChannels channels, float maxShimmerHz)
        {
            Renderer? target = Target;
            if (target == null)
                return;

            target.enabled = channels.Band != OcclusionBand.Outside;
            if (!target.enabled)
                return;

            _block ??= new MaterialPropertyBlock();
            target.GetPropertyBlock(_block);
            _block.SetFloat(DensityId, channels.Density);
            _block.SetFloat(CoherenceId, channels.Coherence);
            _block.SetFloat(PhaseFrequencyId, channels.PhaseFrequency);
            _block.SetFloat(PhaseVelocityId, channels.PhaseVelocity);
            _block.SetFloat(PhaseAmplitudeId, channels.PhaseAmplitude);
            _block.SetFloat(TintId, channels.Tint);
            _block.SetFloat(EdgeWeightId, channels.EdgeWeight);
            _block.SetFloat(ShimmerRateId, (1f - channels.Coherence) * maxShimmerHz);
            target.SetPropertyBlock(_block);
        }
    }
}
