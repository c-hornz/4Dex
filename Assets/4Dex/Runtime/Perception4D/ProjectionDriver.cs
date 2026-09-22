using System;
using System.Collections.Generic;
using FourDex.Math4D;
using FourDex.Perception;
using FourDex.World;
using UnityEngine;

namespace FourDex.Runtime
{
    /// <summary>
    /// Each frame, projects every registered 4D object into the player's 3D slice and places its GameObject there,
    /// then evaluates its perception channels from its signed W gap and hands them to its <see cref="PhaseRenderer"/>.
    /// </summary>
    /// <remarks>
    /// One loop, in a fixed order, instead of an Update per object. Without <see cref="PerceptionSettings"/> it only places objects.
    /// Objects are rigid (ARCHITECTURE §9, risk 1). A rotation through W turns the object's 3D footprint but does
    /// not re-slice its shape; true cross-sections come later.
    /// </remarks>
    [DefaultExecutionOrder(100)] // After input and gameplay have changed the player state this frame.
    public sealed class ProjectionDriver : MonoBehaviour
    {
        [SerializeField] private World4DRegistry? _world;
        [SerializeField] private Player4D? _player;

        [Tooltip("The tracking-space root, i.e. the XRRig. Projected coordinates are in the player's frame, which is this space. Empty: Unity world space.")]
        [SerializeField] private Transform? _viewSpace;

        [Tooltip("Perception tuning. Empty: objects are placed but not perception-rendered.")]
        [SerializeField] private PerceptionSettings? _perception;

        private readonly IProjection4 _projection = new SliceProjection();
        private readonly SurveyRamp _survey = new SurveyRamp();

        /// <summary>One occluder in the 4D shadow field: where it is in the scene, how far it reaches, and its W channels.</summary>
        public readonly struct ShadowOccluder
        {
            public ShadowOccluder(string name, Vector3 center, float radius, ShadowChannels channels, Vector3 echoDirection)
            {
                Name = name;
                Center = center;
                Radius = radius;
                Channels = channels;
                EchoDirection = echoDirection;
            }

            public readonly string Name;

            /// <summary>Shadow centre, already sheared by the view's W lean (SPEC §16).</summary>
            public readonly Vector3 Center;
            public readonly float Radius;
            public readonly ShadowChannels Channels;

            /// <summary>Unit direction the echoes spread along (SPEC §17); their distance apart is in the channels.</summary>
            public readonly Vector3 EchoDirection;
        }

        // Bounded on purpose: the shadow field costs occluders x receiver pixels (ARCHITECTURE §9, risk 4).
        private const int MaxOccluders = 16;

        private static readonly int OccluderSphereId = Shader.PropertyToID("_FourDexOccluderSphere");
        private static readonly int OccluderWId = Shader.PropertyToID("_FourDexOccluderW");
        private static readonly int OccluderCountId = Shader.PropertyToID("_FourDexOccluderCount");
        private static readonly int OccluderEchoId = Shader.PropertyToID("_FourDexOccluderEcho");
        private static readonly int OccluderRippleId = Shader.PropertyToID("_FourDexOccluderRipple");

        private readonly List<ShadowOccluder> _occluders = new List<ShadowOccluder>();
        private readonly Vector4[] _occluderSpheres = new Vector4[MaxOccluders];
        private readonly Vector4[] _occluderChannels = new Vector4[MaxOccluders];
        private readonly Vector4[] _occluderEchoes = new Vector4[MaxOccluders];
        private readonly Vector4[] _occluderRipples = new Vector4[MaxOccluders];

        /// <summary>This frame's shadow casters, strongest first. Includes objects too far in W to be rendered (SPEC §13).</summary>
        public IReadOnlyList<ShadowOccluder> Occluders => _occluders;

        /// <summary>SPEC §10: hold to survey. Ramps in and out over PerceptionParameters.SurveyRampSeconds.</summary>
        public bool SurveyEngaged
        {
            get => _survey.Engaged;
            set => _survey.Engaged = value;
        }

        /// <summary>Current survey amount, 0 normal to 1 full, as given to the perception model.</summary>
        public float SurveyAmount => _survey.Amount;

        /// <summary>Set the survey amount immediately, bypassing the ramp: for snapshots and tests.</summary>
        public void ForceSurvey(float amount) => _survey.Force(amount);

        public PerceptionSettings? Perception => _perception;

        public World4DRegistry? World => _world;

        private void LateUpdate()
        {
            if (_perception != null)
                _survey.Update(Time.deltaTime, _perception.Parameters.SurveyRampSeconds);
            ProjectAll();
        }

        public void ProjectAll()
        {
            if (_world == null || _player == null)
                return;

            Transform4 viewer = _player.State.Pose;
            float focusW = _player.State.FocusW;
            Rotor4 toViewer = viewer.Rotation.Inverse();

            // Validated once per frame, so inspector edits apply live and a bad value degrades instead of breaking.
            PerceptionParameters? parameters = _perception != null ? _perception.Parameters.Validated() : null;
            ShadowParameters? shadow = _perception != null ? _perception.Shadow.Validated() : null;
            if (_perception != null)
                _perception.PushGlobals();
            _occluders.Clear();

            // How far world W leans into the 3-space being viewed: zero in a conventional view, growing as the view
            // rotates through XW/YW/ZW. It is the direction shadows shear and echoes spread along (SPEC §16, §17).
            Vector3 wLean = toViewer.Apply(Vec4.UnitW).Xyz();
            if (_viewSpace != null)
                wLean = _viewSpace.rotation * wLean;
            // With no lean there is no 4D direction to use, so echoes spread across the view instead of nowhere.
            Vector3 echoDirection = wLean.sqrMagnitude > 1e-4f
                ? wLean.normalized
                : (_viewSpace != null ? _viewSpace.right : Vector3.right);

            foreach (Object4DBehaviour member in _world.Members)
            {
                Object4D? body = member.Body;
                if (body == null)
                    continue;

                Projected4 p = _projection.Project(body.Transform.Position, viewer);
                member.LastProjection = p;
                member.DeltaW = p.DeltaW(focusW);

                Rotor4 relative = body.Transform.Rotation.Then(toViewer);
                float halfExtentW = ViewerWHalfExtent(relative, body.HalfExtents);
                member.ViewerHalfExtentW = halfExtentW;
                member.WGap = PerceptionModel.SignedGap(p.W, halfExtentW, focusW);
                if (parameters != null)
                {
                    member.Channels = PerceptionModel.Evaluate(member.WGap, parameters, _survey.Amount);
                    if (member.Phase != null)
                        member.Phase.Apply(member.Channels, _perception!.MaxShimmerHz);
                }

                Vector3 position = new Vector3(p.X, p.Y, p.Z);
                // Fallback keeps last frame's footprint, expressed in view space like the new one would be.
                Quaternion rotation = _viewSpace != null
                    ? Quaternion.Inverse(_viewSpace.rotation) * member.transform.rotation
                    : member.transform.rotation;
                if (TryFootprintRotation(relative, out Quaternion footprint))
                    rotation = footprint;

                if (_viewSpace != null)
                    member.transform.SetPositionAndRotation(_viewSpace.TransformPoint(position), _viewSpace.rotation * rotation);
                else
                    member.transform.SetPositionAndRotation(position, rotation);

                // Size comes from the 4D extent too, for unit-sized meshes (cube, sphere) under an unscaled parent.
                Vector3 scale = FootprintScale(rotation, relative, body.HalfExtents);
                member.transform.localScale = scale;

                // Shadow casting is deliberately not gated on the object being rendered: an occluder too far in W to
                // be seen still darkens what it lies above (SPEC §13).
                if (shadow != null)
                {
                    ShadowChannels channels = ShadowModel.Evaluate(member.WGap, shadow, halfExtentW);
                    member.Shadow = channels;
                    if (channels.Casts)
                    {
                        float radius = 0.5f * Mathf.Max(scale.x, Mathf.Max(scale.y, scale.z)) * shadow.RadiusScale;
                        // SPEC §16: shift the shadow along the direction world W leans into the viewed 3-space. That
                        // lean is zero until the view is rotated through W, so a conventional view shears not at all.
                        Vector3 center = member.transform.position + wLean * channels.ShearPerLean;
                        _occluders.Add(new ShadowOccluder(member.name, center, radius, channels, echoDirection));
                    }
                }
            }

            UploadShadowField();
        }

        /// <summary>
        /// Uploads the shadow field for the shader to evaluate per pixel. Strongest casters win the bounded slots, so
        /// crowding the scene degrades the weakest shadows rather than flickering arbitrary ones.
        /// </summary>
        private void UploadShadowField()
        {
            int echoCount = _perception != null ? Mathf.Clamp(_perception.Shadow.EchoCount, 1, 5) : 1;
            if (_occluders.Count > MaxOccluders)
            {
                _occluders.Sort((a, b) => b.Channels.Strength.CompareTo(a.Channels.Strength));
                _occluders.RemoveRange(MaxOccluders, _occluders.Count - MaxOccluders);
            }

            for (int i = 0; i < MaxOccluders; i++)
            {
                if (i < _occluders.Count)
                {
                    ShadowOccluder o = _occluders[i];
                    ShadowChannels c = o.Channels;
                    _occluderSpheres[i] = new Vector4(o.Center.x, o.Center.y, o.Center.z, o.Radius);
                    _occluderChannels[i] = new Vector4(c.Strength, c.Tint, c.ShimmerHz, c.Softness);
                    _occluderEchoes[i] = new Vector4(o.EchoDirection.x, o.EchoDirection.y, o.EchoDirection.z, c.EchoSpread);
                    _occluderRipples[i] = new Vector4(c.RippleFrequency, c.RippleSpeed, c.RippleAmplitude, echoCount);
                }
                else
                {
                    _occluderSpheres[i] = Vector4.zero;
                    _occluderChannels[i] = Vector4.zero;
                    _occluderEchoes[i] = Vector4.zero;
                    _occluderRipples[i] = Vector4.zero;
                }
            }

            Shader.SetGlobalVectorArray(OccluderSphereId, _occluderSpheres);
            Shader.SetGlobalVectorArray(OccluderWId, _occluderChannels);
            Shader.SetGlobalVectorArray(OccluderEchoId, _occluderEchoes);
            Shader.SetGlobalVectorArray(OccluderRippleId, _occluderRipples);
            Shader.SetGlobalFloat(OccluderCountId, _occluders.Count);
        }

        /// <summary>
        /// Full size along each of the footprint's three axes: the 4D box's extent along that axis. Exact for ordinary
        /// rotations, where the axes are the box's own. Through W it is the rigid object's whole 3D footprint, so W
        /// extent turned toward X shows as width (ARCHITECTURE §9, risk 1: a projection, not a true cross-section).
        /// </summary>
        private static Vector3 FootprintScale(Quaternion footprint, Rotor4 relative, Vec4 h)
        {
            Vector3 bx = relative.Apply(Vec4.UnitX).Xyz();
            Vector3 by = relative.Apply(Vec4.UnitY).Xyz();
            Vector3 bz = relative.Apply(Vec4.UnitZ).Xyz();
            Vector3 bw = relative.Apply(Vec4.UnitW).Xyz();
            float Extent(Vector3 axis) => 2f * (
                Mathf.Abs(Vector3.Dot(axis, bx)) * h.X + Mathf.Abs(Vector3.Dot(axis, by)) * h.Y +
                Mathf.Abs(Vector3.Dot(axis, bz)) * h.Z + Mathf.Abs(Vector3.Dot(axis, bw)) * h.W);
            return new Vector3(Extent(footprint * Vector3.right), Extent(footprint * Vector3.up), Extent(footprint * Vector3.forward));
        }

        /// <summary>
        /// Half the object's extent along the viewer's W axis: how far it reaches ana and kata of its centre as this
        /// viewer sees it. Turning an object through W moves its X/Y/Z extent into W, and this follows.
        /// </summary>
        private static float ViewerWHalfExtent(Rotor4 relative, Vec4 halfExtents) =>
            MathF.Abs(relative.Apply(Vec4.UnitX).W) * halfExtents.X +
            MathF.Abs(relative.Apply(Vec4.UnitY).W) * halfExtents.Y +
            MathF.Abs(relative.Apply(Vec4.UnitZ).W) * halfExtents.Z +
            MathF.Abs(relative.Apply(Vec4.UnitW).W) * halfExtents.W;

        /// <summary>
        /// Unity orientation from the 3D parts of the object's local Y and Z axes, as seen by the viewer. Exact for
        /// ordinary 3D rotations. For rotations through W it is the closest rigid 3D orientation.
        /// </summary>
        /// <returns>False when an axis has turned almost entirely into W and there is no meaningful 3D direction left.</returns>
        private static bool TryFootprintRotation(Rotor4 relative, out Quaternion rotation)
        {
            Vector3 forward = relative.Apply(Vec4.UnitZ).Xyz();
            Vector3 up = relative.Apply(Vec4.UnitY).Xyz();
            const float minSquared = 1e-6f;
            if (forward.sqrMagnitude < minSquared || up.sqrMagnitude < minSquared ||
                Vector3.Cross(forward, up).sqrMagnitude < minSquared)
            {
                rotation = Quaternion.identity;
                return false;
            }
            rotation = Quaternion.LookRotation(forward, up);
            return true;
        }
    }
}
