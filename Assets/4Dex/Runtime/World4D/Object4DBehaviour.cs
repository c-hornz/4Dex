using FourDex.Math4D;
using FourDex.Perception;
using FourDex.World;
using UnityEngine;

namespace FourDex.Runtime
{
    /// <summary>
    /// Scene authoring for one 4D object. The inspector holds its 4D pose and extent. At runtime the
    /// <see cref="Object4D"/> in the world is the truth, and this GameObject's Unity transform is <i>output</i>:
    /// <c>ProjectionDriver</c> overwrites it every frame. Don't move it with the Unity gizmo.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class Object4DBehaviour : MonoBehaviour
    {
        [Tooltip("Registry to join. Empty: the nearest World4DRegistry among this object's parents.")]
        [SerializeField] private World4DRegistry? _registry;

        [Tooltip("4D world position (x, y, z, w). +W is ana, -W is kata.")]
        [SerializeField] private Vector4 _position;

        [Tooltip("Orientation as simultaneous plane angles, in degrees.")]
        [SerializeField] private PlaneAngles _rotationDegrees;

        [Tooltip("Half-size along the object's own axes. W is its thickness through ana/kata.")]
        [SerializeField] private Vector4 _halfExtents = new Vector4(0.5f, 0.5f, 0.5f, 0.5f);

        /// <summary>The live world object, while registered.</summary>
        public Object4D? Body { get; private set; }

        public World4DRegistry? Registry { get; private set; }

        /// <summary>This frame's projection into the player's frame. Written by <c>ProjectionDriver</c>.</summary>
        public Projected4 LastProjection { get; internal set; }

        /// <summary>
        /// Signed distance from the focus slice, <c>LastProjection.W - focusW</c>. Every perception channel reads this;
        /// the sign is what keeps ana and kata apart (SPEC §8).
        /// </summary>
        public float DeltaW { get; internal set; }

        /// <summary>
        /// Signed W distance from the focus slice to the nearest part of the object, accounting for its W extent as the
        /// viewer sees it. Zero while the slice passes through it. What the perception channels are computed from.
        /// </summary>
        public float WGap { get; internal set; }

        /// <summary>Half the object's extent along the viewer's W axis this frame; rotation through W changes it.</summary>
        public float ViewerHalfExtentW { get; internal set; }

        /// <summary>This frame's perception channels. Written by <c>ProjectionDriver</c> when it has PerceptionSettings.</summary>
        public PerceptionChannels Channels { get; internal set; }

        /// <summary>This frame's shadow channels: what this object casts, whether or not it is itself visible (SPEC §13).</summary>
        public ShadowChannels Shadow { get; internal set; }

        private PhaseRenderer? _phase;
        private bool _phaseLookedUp;

        internal PhaseRenderer? Phase
        {
            get
            {
                // Looked up once, so objects without one don't pay GetComponent every frame.
                if (!_phaseLookedUp)
                {
                    _phase = GetComponent<PhaseRenderer>();
                    _phaseLookedUp = true;
                }
                return _phase;
            }
        }

        public Transform4 AuthoredTransform => new Transform4(_position.ToVec4(), _rotationDegrees.ToRotor());

        public Vec4 AuthoredHalfExtents => _halfExtents.ToVec4();

        private void OnEnable()
        {
            World4DRegistry? registry = _registry != null ? _registry : GetComponentInParent<World4DRegistry>();
            if (registry != null)
                registry.Register(this);
            else
                Debug.LogWarning($"[4Dex] {name} has no World4DRegistry and will not exist in 4D.", this);
        }

        private void OnDisable()
        {
            if (Registry != null)
                Registry.Unregister(this);
        }

        // Inspector edits during play go straight into the live world, for runtime experiments (SPEC §21).
        private void OnValidate()
        {
            if (Body == null)
                return;
            Body.Transform = AuthoredTransform;
            Body.HalfExtents = Vector4.Max(_halfExtents, Vector4.zero).ToVec4();
        }

        internal void Bind(World4DRegistry registry, Object4D body)
        {
            Registry = registry;
            Body = body;
        }

        internal void Unbind()
        {
            Registry = null;
            Body = null;
        }
    }
}
