using FourDex.Perception;
using UnityEngine;

namespace FourDex.Runtime
{
    /// <summary>
    /// Every perceptual mapping's tuning, as an asset. Edit it in the inspector during play: changes apply the same
    /// frame, and because it is an asset they persist, so a tuned asset doubles as a saved preset (SPEC §21).
    /// </summary>
    /// <remarks>
    /// The mapping itself lives in the engine-free <see cref="PerceptionModel"/>; this holds its parameters plus the
    /// render-only values the model does not need (palette, dither scale).
    /// </remarks>
    [CreateAssetMenu(fileName = "PerceptionSettings", menuName = "4Dex/Perception Settings")]
    public sealed class PerceptionSettings : ScriptableObject
    {
        private static readonly int AnaColorId = Shader.PropertyToID("_FourDexAnaColor");
        private static readonly int KataColorId = Shader.PropertyToID("_FourDexKataColor");
        private static readonly int TintStrengthId = Shader.PropertyToID("_FourDexTintStrength");
        private static readonly int DitherCellsId = Shader.PropertyToID("_FourDexDitherCellsPerMetre");
        private static readonly int FarFaceFillId = Shader.PropertyToID("_FourDexFarFaceFill");
        private static readonly int ShadowDarknessId = Shader.PropertyToID("_FourDexShadowDarkness");
        private static readonly int ShadowTintId = Shader.PropertyToID("_FourDexShadowTint");
        private static readonly int ShadowReachId = Shader.PropertyToID("_FourDexShadowReach");

        [Tooltip("W gap to channels: focus width, response curve, SPEC §11 band thresholds A/B/C, phase and density mappings.")]
        public PerceptionParameters Parameters = new PerceptionParameters();

        [Tooltip("4D shadow field (SPEC §12-§14): unseen occluders still cast, and focus sharpens their shadows.")]
        public ShadowParameters Shadow = new ShadowParameters();

        [Header("Palette (one redundant cue; never the only one)")]
        [Tooltip("Tint toward this for ana (+W). A configurable default, not a rule (SPEC §9).")]
        public Color AnaColor = new Color(0.35f, 0.72f, 1f);

        [Tooltip("Tint toward this for kata (-W).")]
        public Color KataColor = new Color(1f, 0.55f, 0.25f);

        [Range(0f, 1f), Tooltip("How far the palette pulls surface colour at full tint. 0 turns colour off as a cue.")]
        public float TintStrength = 0.6f;

        [Header("Rendering")]
        [Tooltip("Dither lattice cells per metre of surface. Finer reads as translucency; coarser as visible grain.")]
        public float DitherCellsPerMetre = 90f;

        [Range(0f, 1f), Tooltip("In FAR_W, fill kept on faces pointing at the viewer, relative to the silhouette. Above 0 so a flat face seen head-on does not vanish.")]
        public float FarFaceFill = 0.35f;

        [Tooltip("Dither re-seeds per second at zero coherence: how fast focus mismatch shimmers.")]
        public float MaxShimmerHz = 12f;

        public void PushGlobals()
        {
            Shader.SetGlobalColor(AnaColorId, AnaColor);
            Shader.SetGlobalColor(KataColorId, KataColor);
            Shader.SetGlobalFloat(TintStrengthId, TintStrength);
            Shader.SetGlobalFloat(DitherCellsId, Mathf.Max(DitherCellsPerMetre, 1f));
            Shader.SetGlobalFloat(FarFaceFillId, Mathf.Clamp01(FarFaceFill));
            ShadowParameters s = Shadow.Validated();
            Shader.SetGlobalFloat(ShadowDarknessId, s.Darkness);
            Shader.SetGlobalFloat(ShadowTintId, s.TintStrength);
            Shader.SetGlobalFloat(ShadowReachId, s.ReachMetres);
        }

        private void OnValidate() => PushGlobals();
    }
}
