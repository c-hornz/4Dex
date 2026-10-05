using System;
using FourDex.Math4D;

namespace FourDex.Player
{
    /// <summary>
    /// The player's 4D pose and W focus: the state behind SPEC §7's three W interactions.
    /// </summary>
    /// <remarks>
    /// Each operation touches exactly one piece of state, which is what keeps them independent:
    /// <list type="bullet">
    /// <item><b>Move</b> (§7A) changes <c>Pose.Position</c> only.</item>
    /// <item><b>Rotate</b> (§7B) changes <c>Pose.Rotation</c> only.</item>
    /// <item><b>Focus</b> (§7C) changes <see cref="FocusW"/> only.</item>
    /// </list>
    /// <see cref="FocusW"/> is measured along the viewer's own W axis, relative to the player, because the slice
    /// projection computes <c>deltaW_eff = dw - focusW</c> with <c>dw</c> already viewer-relative
    /// (ARCHITECTURE §5). So moving ana/kata leaves FocusW's value untouched, and focus travels with the player.
    /// Engine-free so the SPEC §25 invariants are tested without Unity; the Unity components wrap this.
    /// </remarks>
    public sealed class Player4DState
    {
        public Player4DState(Transform4 pose, float focusW = 0f)
        {
            RequireFinite(pose.Position, nameof(pose));
            RequireFinite(focusW, nameof(focusW));
            Pose = pose;
            FocusW = focusW;
        }

        public Player4DState() : this(Transform4.Identity) { }

        public Transform4 Pose { get; private set; }

        /// <summary>Viewer-relative W distance the perception system emphasises. Positive is ana.</summary>
        public float FocusW { get; private set; }

        public Vec4 Position => Pose.Position;

        public Rotor4 Orientation => Pose.Rotation;

        // ---- §7A: move ----

        /// <summary>
        /// Optional constraint on every move: given where the player is and where the move would put them, it returns
        /// where they actually end up. The maze's wall collision plugs in here, so walking, ana/kata and head-driven
        /// movement are all constrained the same way, and this class still knows nothing about mazes.
        /// </summary>
        public Func<Vec4, Vec4, Vec4>? MovementFilter { get; set; }

        /// <summary>Move by a world-space offset, including through world W.</summary>
        public void MoveWorld(Vec4 worldDelta)
        {
            RequireFinite(worldDelta, nameof(worldDelta));
            MoveTo(Pose.Position + worldDelta);
        }

        private void MoveTo(Vec4 target)
        {
            Vec4 allowed = MovementFilter != null ? MovementFilter(Pose.Position, target) : target;
            RequireFinite(allowed, nameof(allowed));
            Pose = Pose.WithPosition(allowed);
        }

        /// <summary>
        /// Move along the player's own axes. Once the view is turned through W, "forward" has a world-W
        /// component, so this can change <c>Position.W</c>: that is real movement, not a coupling to rotation.
        /// </summary>
        public void MoveLocal(Vec4 localDelta)
        {
            RequireFinite(localDelta, nameof(localDelta));
            MoveTo(Pose.Position + Pose.Rotation.Apply(localDelta));
        }

        /// <summary>Move straight ana (<c>+</c>) or kata (<c>-</c>) along world W.</summary>
        public void MoveAnaKata(float worldDeltaW) => MoveWorld(new Vec4(0f, 0f, 0f, worldDeltaW));

        /// <summary>
        /// Move ana (<c>+</c>) or kata (<c>-</c>) along the player's own W: toward what is currently shown as ana.
        /// </summary>
        /// <remarks>
        /// Perception measures W gaps along the viewer's W (ARCHITECTURE §5), so this is the ana/kata a player sees.
        /// <see cref="MoveAnaKata(float)"/>'s world W agrees with it only while the view is not turned through W; at
        /// 180 degrees of XW the two are opposite. Still §7A: a move, which leaves orientation and focus alone.
        /// </remarks>
        public void MoveViewAnaKata(float deltaW) => MoveLocal(new Vec4(0f, 0f, 0f, deltaW));

        /// <summary>Move ana/kata in the given frame (<see cref="WMoveFrame"/>).</summary>
        public void MoveAnaKata(float deltaW, WMoveFrame frame)
        {
            if (frame == WMoveFrame.View)
                MoveViewAnaKata(deltaW);
            else
                MoveAnaKata(deltaW);
        }

        // ---- §7B: rotate ----

        /// <summary>Turn in the player's own planes. Position and focus are unchanged.</summary>
        public void RotateLocal(Rotor4 delta) => Pose = Pose.RotatedLocal(delta);

        /// <summary>
        /// Turn in the player's own planes about a point other than the origin, e.g. the eye (given in the player's
        /// local frame). The pivot's 4D position is exactly unchanged; the origin moves to keep it so.
        /// </summary>
        /// <remarks>
        /// Still §7B, not §7A: this is a rotation about a different centre, not a move. Head-driven rotations use the
        /// eye as pivot so that "the world follows the head" turns about the viewpoint instead of swinging the scene
        /// around the feet. Note a pivot raised off the floor does move <c>Position.W</c> under YW or ZW rotation:
        /// the eye stays put, so the floor origin must swing through W. The pivot shift bypasses
        /// <see cref="MovementFilter"/> on purpose: it is what keeps the pivot fixed, not a move through the world.
        /// </remarks>
        public void RotateLocalAbout(Rotor4 delta, Vec4 localPivot)
        {
            RequireFinite(localPivot, nameof(localPivot));
            // In local coordinates the step is x -> delta(x - pivot) + pivot. Its offset (pivot - delta(pivot)) is
            // local to the frame before the turn, so move first, then rotate.
            Pose = Pose.TranslatedLocal(localPivot - delta.Apply(localPivot)).RotatedLocal(delta);
        }

        public void RotateLocal(Plane4 plane, float angle)
        {
            RequireFinite(angle, nameof(angle));
            RotateLocal(Rotor4.FromPlane(plane, angle));
        }

        /// <summary>
        /// Instant return to the unrotated view, keeping position and focus (SPEC §22 comfort reset).
        /// </summary>
        public void ResetOrientation() => Pose = Pose.WithRotation(Rotor4.Identity);

        // ---- §7C: focus ----

        public void SetFocusW(float focusW)
        {
            RequireFinite(focusW, nameof(focusW));
            FocusW = focusW;
        }

        public void AdjustFocusW(float delta) => SetFocusW(FocusW + delta);

        // A NaN or infinity here would quietly poison every later projection, which reads as a rendering bug.
        private static void RequireFinite(float value, string name)
        {
            if (float.IsNaN(value) || float.IsInfinity(value))
                throw new ArgumentOutOfRangeException(name, value, "Must be finite.");
        }

        private static void RequireFinite(Vec4 v, string name)
        {
            RequireFinite(v.X, name);
            RequireFinite(v.Y, name);
            RequireFinite(v.Z, name);
            RequireFinite(v.W, name);
        }

        public override string ToString() => $"{Pose}, FocusW {FocusW}";
    }
}
