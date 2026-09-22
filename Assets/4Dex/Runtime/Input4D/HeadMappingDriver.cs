using System.Collections.Generic;
using FourDex.InputMapping;
using FourDex.Math4D;
using FourDex.XR;
using UnityEngine;

namespace FourDex.Runtime
{
    /// <summary>
    /// The second consumer of the head pose (ARCHITECTURE §3): turns the physical HMD pose into 4D motion through the
    /// active mapping profile. It never touches the camera; <see cref="XRRig"/> has already placed it, unmodified.
    /// </summary>
    /// <remarks>
    /// Offsets are measured from a neutral pose, captured on the first sample and on <see cref="Recenter"/>, in the
    /// neutral facing direction, so "lean forward" means forward from where you faced at recentre. Rotations turn about
    /// the eye (<see cref="FourDex.Player.Player4DState.RotateLocalAbout"/>), so "the world follows the head" turns
    /// about the viewpoint.
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class HeadMappingDriver : MonoBehaviour
    {
        [SerializeField] private XRRig? _rig;
        [SerializeField] private Player4D? _player;

        [Tooltip("Selectable profiles, in cycling order. The one named CLASSIC is the conventional fallback (SPEC §22).")]
        [SerializeField] private List<InputMappingProfile> _profiles = new List<InputMappingProfile>();

        [SerializeField] private int _activeIndex;

        [Tooltip("SPEC §22: stop head input from changing the 4D view, without switching profile.")]
        [SerializeField] private bool _frozen;

        [Tooltip("SPEC §22: disable every HMD-derived mapping at once.")]
        [SerializeField] private bool _mappingEnabled = true;

        private readonly MappingEngine _engine = new MappingEngine();
        private bool _hasNeutral;
        private Vector3 _neutralPosition;
        private Quaternion _neutralRotation = Quaternion.identity;
        private Quaternion _neutralYaw = Quaternion.identity;

        public int ProfileCount => _profiles.Count;

        public InputMappingProfile? ActiveAsset =>
            _profiles.Count == 0 ? null : _profiles[Mathf.Clamp(_activeIndex, 0, _profiles.Count - 1)];

        public MappingProfile? Active => ActiveAsset != null ? ActiveAsset.Profile : null;

        public string ActiveName => Active?.Name ?? "(none)";

        public int ActiveIndex => Mathf.Clamp(_activeIndex, 0, Mathf.Max(_profiles.Count - 1, 0));

        public bool Frozen
        {
            get => _frozen;
            set => _frozen = value;
        }

        public bool MappingEnabled
        {
            get => _mappingEnabled;
            set => _mappingEnabled = value;
        }

        public IReadOnlyList<Contribution> Contributions => _engine.Contributions;

        private void OnEnable()
        {
            if (_rig != null)
                _rig.PoseSampled += OnPoseSampled;
        }

        private void OnDisable()
        {
            if (_rig != null)
                _rig.PoseSampled -= OnPoseSampled;
        }

        private void OnPoseSampled(HmdPose pose) => Apply(pose, Time.deltaTime);

        /// <summary>One frame of mapping. Public so tests (and later, recorded-input playback) can drive it.</summary>
        public void Apply(HmdPose pose, float dt)
        {
            if (!_hasNeutral)
                SetNeutral(pose);

            MappingProfile? profile = Active;
            if (profile == null || !_mappingEnabled || _player == null)
                return;

            // Stepped even while frozen, so Absolute bindings track the head and unfreezing does not jump.
            MappingStep step = _engine.Step(profile, Offsets(pose), dt);
            if (_frozen || step.IsZero)
                return;

            var state = _player.State;
            if (step.Translation != Vec4.Zero)
            {
                // XYZ come out in the neutral-facing frame; turn them back into tracking (player) axes. W needs nothing.
                Vector3 xyz = _neutralYaw * new Vector3(step.Translation.X, step.Translation.Y, step.Translation.Z);
                state.MoveLocal(new Vec4(xyz.x, xyz.y, xyz.z, step.Translation.W));
            }
            Bivector4 r = step.Rotation;
            if (r.XY != 0f || r.XZ != 0f || r.YZ != 0f || r.XW != 0f || r.YW != 0f || r.ZW != 0f)
                state.RotateLocalAbout(Rotor4.FromBivector(r), new Vec4(pose.Position.x, pose.Position.y, pose.Position.z, 0f));
        }

        /// <summary>Head offsets from neutral: angles in degrees, positions in metres along the neutral facing.</summary>
        public HeadOffsets Offsets(HmdPose pose)
        {
            Vector3 e = (Quaternion.Inverse(_neutralRotation) * pose.Rotation).eulerAngles;
            Vector3 d = Quaternion.Inverse(_neutralYaw) * (pose.Position - _neutralPosition);
            return new HeadOffsets
            {
                Yaw = Mathf.DeltaAngle(0f, e.y),
                // Unity pitch is positive looking down; the signal is positive looking up.
                Pitch = -Mathf.DeltaAngle(0f, e.x),
                Roll = Mathf.DeltaAngle(0f, e.z),
                X = d.x,
                Y = d.y,
                Z = d.z,
            };
        }

        /// <summary>The current head pose becomes neutral on the next sample.</summary>
        public void Recenter() => _hasNeutral = false;

        public void SetProfile(int index)
        {
            if (_profiles.Count == 0)
                return;
            _activeIndex = ((index % _profiles.Count) + _profiles.Count) % _profiles.Count;
            _engine.Reset();
        }

        /// <summary>SPEC §22's instant return to conventional mapping: CLASSIC, unfrozen, recentred.</summary>
        public void ReturnToConventional()
        {
            int classic = _profiles.FindIndex(p => p != null && p.Profile.Name == MappingPresets.Classic);
            SetProfile(classic >= 0 ? classic : 0);
            _frozen = false;
            Recenter();
        }

        private void SetNeutral(HmdPose pose)
        {
            _neutralPosition = pose.Position;
            _neutralRotation = pose.Rotation;
            _neutralYaw = Quaternion.Euler(0f, pose.Rotation.eulerAngles.y, 0f);
            _hasNeutral = true;
            _engine.Reset();
        }
    }
}
