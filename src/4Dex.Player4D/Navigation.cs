using System;
using FourDex.Math4D;

namespace FourDex.Player
{
    /// <summary>Which W the ana/kata control moves along.</summary>
    public enum WMoveFrame
    {
        /// <summary>
        /// The viewer's own W: ana is always what is currently shown as ana, whatever the orientation. With walking,
        /// which is already viewer-relative, this makes all four of the player's own directions movable.
        /// </summary>
        View,

        /// <summary>
        /// World W: always changes <c>position.w</c> and nothing else. After a turn through W this no longer matches
        /// what is shown as ana; at 180 degrees it is reversed.
        /// </summary>
        World,
    }

    /// <summary>Whether XW/YW/ZW turning sweeps or steps.</summary>
    public enum RotationMode
    {
        Smooth,

        /// <summary>
        /// Fixed steps per press, played as a short turn rather than a jump so the change stays followable. With
        /// 90 degree steps every orientation reached is axis-aligned, and so nameable.
        /// </summary>
        Snap,
    }

    /// <summary>
    /// Navigation aids against getting lost between rotations: stepped W turns, align-to-axes, and settling into
    /// the nearest W slice after ana/kata movement. Each is an option, so the unaided behaviour stays testable.
    /// </summary>
    [Serializable]
    public sealed class NavigationParameters
    {
        public WMoveFrame AnaKataFrame = WMoveFrame.View;

        public RotationMode WRotationMode = RotationMode.Snap;

        /// <summary>Degrees per press for stepped XW/YW/ZW turns.</summary>
        public float WSnapDegrees = 90f;

        /// <summary>How long a stepped turn or an align takes to play out. 0 jumps.</summary>
        public float TurnSeconds = 0.3f;

        /// <summary>After ana/kata movement stops, ease to the centre of the nearest W slice or cell.</summary>
        public bool SettleToSlices = true;

        /// <summary>Slice spacing in W units when the scene has no maze; a maze uses its cell size instead.</summary>
        public float SliceSpacing = 1f;

        /// <summary>Time constant of the settle, in seconds.</summary>
        public float SettleSeconds = 0.15f;

        /// <summary>
        /// The behaviour before these aids existed: smooth turning, world-W movement, no settling. What a caller
        /// gets when it supplies no settings.
        /// </summary>
        public static NavigationParameters Unassisted => new NavigationParameters
        {
            AnaKataFrame = WMoveFrame.World,
            WRotationMode = RotationMode.Smooth,
            SettleToSlices = false,
        };

        public NavigationParameters Validated()
        {
            var p = (NavigationParameters)MemberwiseClone();
            p.WSnapDegrees = MathF.Max(p.WSnapDegrees, 1f);
            p.TurnSeconds = MathF.Max(p.TurnSeconds, 0f);
            p.SliceSpacing = MathF.Max(p.SliceSpacing, 0f);
            p.SettleSeconds = MathF.Max(p.SettleSeconds, 0f);
            return p;
        }
    }

    /// <summary>
    /// Plays out stepped turns and aligns, and settles position after ana/kata movement. One per player.
    /// </summary>
    /// <remarks>
    /// Every turn here is a local rotation by a fixed bivector, applied in fractions over time. Rotations about the
    /// same bivector commute, so the fractions compose exactly to the whole turn however the frames divide it.
    /// Turns still go through <see cref="Player4DState.RotateLocal(Rotor4)"/> and moves through
    /// <see cref="Player4DState.MoveWorld"/>, so the §7 separation and wall collision hold as for any other input.
    /// </remarks>
    public sealed class NavigationAssist
    {
        private Bivector4 _turn;
        private float _turnDone = 1f;
        private float _turnSeconds;

        // The world axis the last ana/kata movement went along, while a settle is pending; -1 when none is.
        private int _settleAxis = -1;

        public bool IsTurning => _turnDone < 1f;

        public bool IsSettling => _settleAxis >= 0;

        /// <summary>Start a stepped turn in one of the player's own planes. <paramref name="direction"/> is +1 or -1.</summary>
        public void StepTurn(Player4DState player, Plane4 plane, int direction, NavigationParameters p)
        {
            if (direction == 0)
                return;
            float angle = Math.Sign(direction) * p.WSnapDegrees * (MathF.PI / 180f);
            StartTurn(player, Bivector4.FromPlane(plane, angle), p.TurnSeconds);
        }

        /// <summary>Turn to the nearest axis-aligned orientation (<see cref="AxisAlignment"/>).</summary>
        public void Align(Player4DState player, NavigationParameters p)
        {
            Finish(player);
            Rotor4 target = AxisAlignment.Nearest(player.Orientation);
            StartTurn(player, AxisAlignment.LocalTurn(player.Orientation, target), p.TurnSeconds);
        }

        private void StartTurn(Player4DState player, Bivector4 turn, float seconds)
        {
            // A press during a turn completes the previous one first, so steps never land between axes.
            Finish(player);
            _turn = turn;
            _turnDone = 0f;
            _turnSeconds = seconds;
            if (seconds <= 0f)
                Finish(player);
        }

        /// <summary>Apply whatever is left of the current turn now.</summary>
        public void Finish(Player4DState player)
        {
            if (IsTurning)
                Advance(player, 1f);
        }

        /// <summary>Drop any turn or settle in progress without applying the rest, e.g. on a view reset.</summary>
        public void Cancel()
        {
            _turnDone = 1f;
            _settleAxis = -1;
        }

        /// <summary>
        /// Call once per frame after input has been applied.
        /// </summary>
        /// <param name="anaKataDirection">
        /// World direction of this frame's ana/kata movement, or zero when there was none. Settling starts when it
        /// returns to zero.
        /// </param>
        /// <param name="cellSize">The maze's cell size, or 0 when the scene has no maze.</param>
        public void Update(Player4DState player, Vec4 anaKataDirection, float cellSize, NavigationParameters p, float dt)
        {
            if (IsTurning)
                Advance(player, _turnSeconds > 0f ? _turnDone + MathF.Max(dt, 0f) / _turnSeconds : 1f);

            if (anaKataDirection.LengthSquared > 0f)
            {
                // Remember which way the movement went, and settle along that once it stops.
                _settleAxis = p.SettleToSlices ? DominantAxis(anaKataDirection) : -1;
                return;
            }
            if (_settleAxis >= 0)
                Settle(player, cellSize, p, dt);
        }

        private void Advance(Player4DState player, float to)
        {
            float target = MathF.Min(to, 1f);
            float step = target - _turnDone;
            if (step > 0f)
                player.RotateLocal(Rotor4.FromBivector(_turn * step));
            _turnDone = target;
        }

        private void Settle(Player4DState player, float cellSize, NavigationParameters p, float dt)
        {
            // In a maze every axis has cell centres to settle to. Without one only W has slices: settling sideways
            // in an open lab would be a move nobody asked for.
            float spacing = cellSize > 0f ? cellSize : _settleAxis == 3 ? p.SliceSpacing : 0f;
            if (spacing <= 0f)
            {
                _settleAxis = -1;
                return;
            }

            float current = player.Position[_settleAxis];
            float gap = MathF.Round(current / spacing, MidpointRounding.AwayFromZero) * spacing - current;
            float blend = p.SettleSeconds > 0f ? 1f - MathF.Exp(-MathF.Max(dt, 0f) / p.SettleSeconds) : 1f;
            // The last millimetre is closed in one step, so the settle ends instead of approaching forever.
            float step = MathF.Abs(gap) < 1e-3f ? gap : gap * blend;
            player.MoveWorld(Vec4.Axis(_settleAxis) * step);
            if (MathF.Abs(gap - step) < 1e-6f || player.Position[_settleAxis] == current)
                _settleAxis = -1; // arrived, or a wall is in the way
        }

        private static int DominantAxis(Vec4 v)
        {
            int axis = 0;
            for (int i = 1; i < 4; i++)
            {
                if (MathF.Abs(v[i]) > MathF.Abs(v[axis]))
                    axis = i;
            }
            return axis;
        }
    }
}
