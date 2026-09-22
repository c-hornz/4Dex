using FourDex.Math4D;
using FourDex.World;
using UnityEngine;

namespace FourDex.Runtime
{
    /// <summary>
    /// Each frame, projects every registered 4D object into the player's 3D slice and places its GameObject there.
    /// Also records each object's signed <c>DeltaW</c> for the perception channels.
    /// </summary>
    /// <remarks>
    /// Minimal Milestone 0 version of ARCHITECTURE §8's ProjectionDriver: placement and DeltaW only, no shaders.
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

        private readonly IProjection4 _projection = new SliceProjection();

        private void LateUpdate() => ProjectAll();

        public void ProjectAll()
        {
            if (_world == null || _player == null)
                return;

            Transform4 viewer = _player.State.Pose;
            float focusW = _player.State.FocusW;
            Rotor4 toViewer = viewer.Rotation.Inverse();

            foreach (Object4DBehaviour member in _world.Members)
            {
                Object4D? body = member.Body;
                if (body == null)
                    continue;

                Projected4 p = _projection.Project(body.Transform.Position, viewer);
                member.LastProjection = p;
                member.DeltaW = p.DeltaW(focusW);

                Vector3 position = new Vector3(p.X, p.Y, p.Z);
                // Fallback keeps last frame's footprint, expressed in view space like the new one would be.
                Quaternion rotation = _viewSpace != null
                    ? Quaternion.Inverse(_viewSpace.rotation) * member.transform.rotation
                    : member.transform.rotation;
                if (TryFootprintRotation(body.Transform.Rotation.Then(toViewer), out Quaternion footprint))
                    rotation = footprint;

                if (_viewSpace != null)
                    member.transform.SetPositionAndRotation(_viewSpace.TransformPoint(position), _viewSpace.rotation * rotation);
                else
                    member.transform.SetPositionAndRotation(position, rotation);

                // Size comes from the 4D extent too, for unit-sized meshes (cube, sphere) under an unscaled parent.
                member.transform.localScale = 2f * body.HalfExtents.Xyz();
            }
        }

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
