namespace FourDex.Math4D
{
    /// <summary>
    /// Position and orientation of a 4D object or viewer. Local to world:
    /// <c>world = Rotation.Apply(local) + Position</c>.
    /// </summary>
    /// <remarks>
    /// Position and rotation are separate fields so SPEC §7's operations stay separate: rotating toward
    /// W (§7B) never touches <c>Position.W</c>, and moving through W (§7A) never touches orientation.
    /// Focus (§7C) is not here at all; it belongs to the perception layer.
    /// </remarks>
    public readonly struct Transform4
    {
        public readonly Vec4 Position;
        public readonly Rotor4 Rotation;

        public Transform4(Vec4 position, Rotor4 rotation)
        {
            Position = position;
            Rotation = rotation;
        }

        public static readonly Transform4 Identity = new Transform4(Vec4.Zero, Rotor4.Identity);

        public Basis4 Basis => Rotation.ToBasis();

        public Vec4 TransformPoint(Vec4 local) => Rotation.Apply(local) + Position;

        public Vec4 InverseTransformPoint(Vec4 world) => Rotation.Inverse().Apply(world - Position);

        public Vec4 TransformDirection(Vec4 local) => Rotation.Apply(local);

        public Vec4 InverseTransformDirection(Vec4 world) => Rotation.Inverse().Apply(world);

        public Transform4 WithPosition(Vec4 position) => new Transform4(position, Rotation);

        public Transform4 WithRotation(Rotor4 rotation) => new Transform4(Position, rotation);

        /// <summary>Move by a world-space offset. Orientation is unchanged.</summary>
        public Transform4 Translated(Vec4 worldDelta) => new Transform4(Position + worldDelta, Rotation);

        /// <summary>Move along this frame's own axes, e.g. "forward" or "ana" as the viewer currently sees them.</summary>
        public Transform4 TranslatedLocal(Vec4 localDelta) => Translated(Rotation.Apply(localDelta));

        /// <summary>
        /// Turn in place in this frame's own planes, the way a viewer turns. Position is unchanged.
        /// </summary>
        /// <remarks><c>delta</c> is expressed in local axes, so it applies before the current orientation.</remarks>
        public Transform4 RotatedLocal(Rotor4 delta) => new Transform4(Position, delta.Then(Rotation));

        /// <summary>Turn in place in the world's coordinate planes. Position is unchanged.</summary>
        public Transform4 RotatedWorld(Rotor4 delta) => new Transform4(Position, Rotation.Then(delta));

        public override string ToString() => $"Position {Position}, Rotation {Rotation}";
    }
}
