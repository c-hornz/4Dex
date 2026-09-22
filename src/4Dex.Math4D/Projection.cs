using System.Globalization;

namespace FourDex.Math4D
{
    /// <summary>A 4D world point mapped into a viewer's frame.</summary>
    public readonly struct Projected4
    {
        /// <summary>Viewer-frame 3D coordinates, handed to Unity as an ordinary 3D position.</summary>
        public readonly float X;
        public readonly float Y;
        public readonly float Z;

        /// <summary>
        /// Signed offset along the viewer's W axis: <c>+</c> is ana of the viewer, <c>-</c> is kata.
        /// Every perception channel is driven from this value.
        /// </summary>
        public readonly float W;

        public Projected4(float x, float y, float z, float w)
        {
            X = x;
            Y = y;
            Z = z;
            W = w;
        }

        /// <summary>
        /// Signed distance from the focus slice, <c>deltaW_eff = W - focusW</c> (ARCHITECTURE.md §5).
        /// The sign is kept on purpose: ana and kata must stay distinguishable (SPEC §8).
        /// </summary>
        public float DeltaW(float focusW) => W - focusW;

        public override string ToString() => string.Format(CultureInfo.InvariantCulture,
            "({0:F4}, {1:F4}, {2:F4} | w {3:F4})", X, Y, Z, W);
    }

    /// <summary>
    /// Maps 4D world points into a viewer's frame. Swappable, so that a later 4D-perspective projection
    /// can sit next to the slice model instead of replacing it (ARCHITECTURE.md §5).
    /// </summary>
    public interface IProjection4
    {
        Projected4 Project(Vec4 worldPoint, in Transform4 viewer);
    }

    /// <summary>
    /// The default projection: the viewer inhabits a 3-space slice, and W comes along as a signed
    /// perception channel instead of being flattened away.
    /// </summary>
    /// <remarks>
    /// <c>d = P - viewer.Position</c>, then dot <c>d</c> with each viewer axis. That is exactly the
    /// viewer's inverse transform, since the axes are orthonormal.
    /// </remarks>
    public sealed class SliceProjection : IProjection4
    {
        public Projected4 Project(Vec4 worldPoint, in Transform4 viewer)
        {
            Vec4 local = viewer.InverseTransformPoint(worldPoint);
            return new Projected4(local.X, local.Y, local.Z, local.W);
        }
    }
}
