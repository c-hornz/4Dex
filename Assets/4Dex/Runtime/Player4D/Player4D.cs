using FourDex.Math4D;
using FourDex.Player;
using UnityEngine;

namespace FourDex.Runtime
{
    /// <summary>
    /// Scene home of the player's <see cref="Player4DState"/>. The state is engine-free and owns the SPEC §7 / §25
    /// rules; this component only supplies starting values and lets other components find it.
    /// </summary>
    /// <remarks>
    /// The 4D pose is the origin and axes of tracking space. The physical head moves inside that space and is
    /// never folded into the 4D pose; it reaches the 4D system only through a mapping (ARCHITECTURE §3).
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class Player4D : MonoBehaviour
    {
        [Tooltip("Starting 4D position of tracking-space origin (x, y, z, w).")]
        [SerializeField] private Vector4 _startPosition;

        [SerializeField] private PlaneAngles _startRotationDegrees;

        [Tooltip("Starting focus, as a W distance from the player along their own W axis. 0 = their own slice.")]
        [SerializeField] private float _startFocusW;

        private Player4DState? _state;

        // Lazy rather than in Awake, which does not run in edit mode.
        public Player4DState State =>
            _state ??= new Player4DState(new Transform4(_startPosition.ToVec4(), _startRotationDegrees.ToRotor()), _startFocusW);

        /// <summary>Back to the authored start pose and focus.</summary>
        public void ResetToStart() => _state = null;
    }
}
