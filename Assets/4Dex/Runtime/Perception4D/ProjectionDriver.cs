using System;
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

        public PerceptionSettings? Perception => _perception;

        public World4DRegistry? World => _world;

        private void LateUpdate() => ProjectAll();

        public void ProjectAll()
        {
            if (_world == null || _player == null)
                return;

            Transform4 viewer = _player.State.Pose;
            float focusW = _player.State.FocusW;
            Rotor4 toViewer = viewer.Rotation.Inverse();

            // Validated once per frame, so inspector edits apply live and a bad value degrades instead of breaking.
            PerceptionParameters? parameters = _perception != null ? _perception.Parameters.Validated() : null;
            if (_perception != null)
                _perception.PushGlobals();

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
                    member.Channels = PerceptionModel.Evaluate(member.WGap, parameters);
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
                member.transform.localScale = FootprintScale(rotation, relative, body.HalfExtents);
            }
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
