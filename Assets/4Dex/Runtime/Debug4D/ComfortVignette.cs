using FourDex.Math4D;
using FourDex.Player;
using UnityEngine;

namespace FourDex.Runtime
{
    /// <summary>
    /// Draws the comfort vignette (SPEC §22) and measures the motion that drives it. Put it on the XR camera; it
    /// builds its own disc mesh, so it needs no asset.
    /// </summary>
    /// <remarks>
    /// Motion is measured from the player's 4D state, frame to frame, so it counts the three things that actually
    /// provoke discomfort here: walking, turning the view, and travelling ana/kata. Head motion is deliberately not
    /// counted: your own head moving is what your inner ear expects, and vignetting it would be wrong.
    /// </remarks>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Camera))]
    [DefaultExecutionOrder(200)] // after the player has moved this frame
    public sealed class ComfortVignette : MonoBehaviour
    {
        private static readonly int ApertureId = Shader.PropertyToID("_Aperture");
        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static readonly int SoftnessId = Shader.PropertyToID("_Softness");

        [SerializeField] private Player4D? _player;
        [SerializeField] private ComfortSettings? _settings;

        [Tooltip("How far in front of the eyes the disc sits, in metres. Close enough to be out of focus, far enough not to clip.")]
        [SerializeField] private float _distance = 0.35f;

        [SerializeField] private Color _color = Color.black;

        [Range(0.02f, 1f), Tooltip("Width of the soft edge, as a fraction of the disc's radius.")]
        [SerializeField] private float _softness = 0.22f;

        private readonly VignetteModel _model = new VignetteModel();
        private MeshRenderer? _renderer;
        private MaterialPropertyBlock? _block;
        private float _builtFov;
        private float _builtAspect;
        private Vec4 _previousPosition;
        private Rotor4 _previousOrientation = Rotor4.Identity;
        private bool _hasPrevious;

        /// <summary>1 is fully open; below that the view is closing in.</summary>
        public float Aperture => _model.Aperture;

        /// <summary>How much motion is being registered, 0..1. Shown in the HUD.</summary>
        public float Motion => _model.Motion;

        private void OnDisable()
        {
            _hasPrevious = false;
            _model.Reset();
        }

        private void LateUpdate() => Tick(Time.deltaTime);

        /// <summary>Show a given aperture without any motion to drive it: for snapshots and tuning by eye.</summary>
        public void Preview(float aperture)
        {
            if (_settings == null)
                return;
            _model.Force(aperture);
            Apply(_settings.Parameters.Validated());
        }

        /// <summary>One frame of vignette: measure the player's motion, ease the aperture, draw it. Public for tests.</summary>
        public void Tick(float dt)
        {
            if (_player == null || _settings == null)
                return;

            ComfortParameters parameters = _settings.Parameters.Validated();
            _model.Update(Measure(_player.State, dt), parameters, dt);
            Apply(parameters);
        }

        private MotionSample Measure(Player4DState state, float dt)
        {
            Vec4 position = state.Position;
            Rotor4 orientation = state.Orientation;

            if (!_hasPrevious || dt <= 0f)
            {
                _previousPosition = position;
                _previousOrientation = orientation;
                _hasPrevious = true;
                return new MotionSample(0f, 0f, 0f);
            }

            Vec4 delta = position - _previousPosition;
            // The rotation actually applied this frame, as one bivector: its length is how much turning happened,
            // whatever mix of planes it was spread across.
            Bivector4 turn = orientation.Then(_previousOrientation.Inverse()).Log();
            float turnRadians = Mathf.Sqrt(
                turn.XY * turn.XY + turn.XZ * turn.XZ + turn.YZ * turn.YZ +
                turn.XW * turn.XW + turn.YW * turn.YW + turn.ZW * turn.ZW);

            _previousPosition = position;
            _previousOrientation = orientation;

            return new MotionSample(
                new Vector3(delta.X, delta.Y, delta.Z).magnitude / dt,
                turnRadians * Mathf.Rad2Deg / dt,
                Mathf.Abs(delta.W) / dt);
        }

        private void Apply(ComfortParameters parameters)
        {
            MeshRenderer target = EnsureDisc();
            // Fully open: nothing to draw at all, so the overlay costs nothing while standing still.
            target.enabled = parameters.VignetteEnabled && _model.Aperture < 0.999f;
            if (!target.enabled)
                return;

            _block ??= new MaterialPropertyBlock();
            target.GetPropertyBlock(_block);
            _block.SetFloat(ApertureId, _model.Aperture);
            _block.SetColor(ColorId, _color);
            _block.SetFloat(SoftnessId, _softness);
            target.SetPropertyBlock(_block);
        }

        private MeshRenderer EnsureDisc()
        {
            // The fade radius is measured against the corner of the view, so aperture 1 means "dark only past the
            // corners", i.e. nothing visible. Field of view differs between the desktop and the headset, so the mesh
            // is rebuilt whenever it changes.
            var camera = GetComponent<Camera>();
            float fov = camera != null ? camera.fieldOfView : 60f;
            float aspect = camera != null && camera.aspect > 0f ? camera.aspect : 1.78f;
            if (_renderer != null && Mathf.Abs(fov - _builtFov) < 0.5f && Mathf.Abs(aspect - _builtAspect) < 0.02f)
                return _renderer;

            float corner = _distance * Mathf.Tan(0.5f * fov * Mathf.Deg2Rad) * Mathf.Sqrt(1f + aspect * aspect);
            _builtFov = fov;
            _builtAspect = aspect;

            if (_renderer != null)
            {
                _renderer.GetComponent<MeshFilter>().sharedMesh = BuildDisc(corner);
                return _renderer;
            }

            var go = new GameObject("Comfort Vignette");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = new Vector3(0f, 0f, _distance);

            var filter = go.AddComponent<MeshFilter>();
            filter.sharedMesh = BuildDisc(corner);
            _renderer = go.AddComponent<MeshRenderer>();
            _renderer.sharedMaterial = new Material(Shader.Find("4Dex/Vignette"));
            _renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _renderer.receiveShadows = false;
            return _renderer;
        }

        /// <summary>
        /// A disc as a triangle fan, with u holding each vertex's distance from the centre in units of
        /// <paramref name="cornerRadius"/>: u = 1 is the corner of the view, so the shader's aperture is a fraction
        /// of the visible field. The mesh itself extends well past that, to cover the whole view when nearly closed.
        /// </summary>
        private static Mesh BuildDisc(float cornerRadius)
        {
            const int segments = 96;
            float radius = cornerRadius * 2.5f;
            var vertices = new Vector3[segments + 1];
            var uv = new Vector2[segments + 1];
            var triangles = new int[segments * 3];

            vertices[0] = Vector3.zero;
            uv[0] = Vector2.zero;
            for (int i = 0; i < segments; i++)
            {
                float angle = 2f * Mathf.PI * i / segments;
                vertices[i + 1] = new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius, 0f);
                uv[i + 1] = new Vector2(radius / Mathf.Max(cornerRadius, 1e-4f), 0f);
                triangles[i * 3] = 0;
                triangles[i * 3 + 1] = i + 1;
                triangles[i * 3 + 2] = (i + 1) % segments + 1;
            }

            var mesh = new Mesh { name = "Comfort Vignette Disc" };
            mesh.SetVertices(vertices);
            mesh.SetUVs(0, uv);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
