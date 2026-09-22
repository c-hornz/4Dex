using System;
using System.IO;
using System.Linq;
using FourDex.Math4D;
using FourDex.Runtime;
using FourDex.XR;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace FourDex.EditorTools
{
    /// <summary>
    /// Generates <c>Assets/Scenes/Lab4D.unity</c>, the SPEC §23 laboratory, from code, so the scene is
    /// reproducible and reviewable as source rather than hand-edited YAML. Rerunning replaces the scene.
    /// Batch: <c>-executeMethod FourDex.EditorTools.Lab4DSceneBuilder.Build</c>.
    /// </summary>
    /// <remarks>
    /// Milestone 0 has no perception shaders, so A, B and C are spread along X, and are identifiable by shape and
    /// position alone. Materials are neutral: colour is reserved for the ana/kata palette (SPEC §9, §27).
    /// </remarks>
    public static class Lab4DSceneBuilder
    {
        public const string ScenePath = "Assets/Scenes/Lab4D.unity";
        private const string MaterialFolder = "Assets/4Dex/Materials";
        private const string PerceptionSettingsPath = "Assets/4Dex/Settings/PerceptionSettings.asset";

        private static readonly Color Background = new Color(0.08f, 0.09f, 0.11f);

        [MenuItem("4Dex/Rebuild Lab4D Scene")]
        public static void Build()
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            RenderSettings.ambientMode = AmbientMode.Flat;
            // Linear colour space: this reads much darker than the number suggests, so it is set high for an evenly readable lab.
            RenderSettings.ambientLight = new Color(0.55f, 0.55f, 0.58f);
            RenderSettings.skybox = null;

            var light = new GameObject("Directional Light").AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.2f;
            // From behind and above the viewer, so the faces the player sees are the lit ones.
            light.transform.rotation = Quaternion.Euler(35f, 25f, 0f);

            Material objectMaterial = GetOrCreateMaterial("LabObject", new Color(0.82f, 0.82f, 0.82f), "4Dex/Phase4D");
            Material floorMaterial = GetOrCreateMaterial("LabFloor", new Color(0.22f, 0.23f, 0.25f), "Universal Render Pipeline/Lit");

            // Plain 3D, not a 4D object: a stable conventional reference for comfort and for judging head tracking.
            GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floor.name = "Floor (3D reference, not 4D)";
            floor.transform.localScale = new Vector3(0.6f, 1f, 0.6f); // 6 m x 6 m
            floor.GetComponent<Renderer>().sharedMaterial = floorMaterial;

            // Tracking space. The camera's pose is written by XRRig; 1.6 m is only the pre-tracking placeholder.
            var rigGo = new GameObject("XRRig");
            var rig = rigGo.AddComponent<XRRig>();
            var head = new GameObject("Head (XR camera)");
            head.tag = "MainCamera";
            head.transform.SetParent(rigGo.transform, false);
            head.transform.localPosition = new Vector3(0f, 1.6f, 0f);
            var camera = head.AddComponent<Camera>();
            camera.nearClipPlane = 0.05f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Background;
            head.AddComponent<AudioListener>();
            Set(rig, "_camera", camera);

            var worldGo = new GameObject("World4D");
            var registry = worldGo.AddComponent<World4DRegistry>();
            var player = worldGo.AddComponent<Player4D>();
            var driver = worldGo.AddComponent<ProjectionDriver>();
            Set(driver, "_world", registry);
            Set(driver, "_player", player);
            Set(driver, "_viewSpace", rigGo.transform);
            PerceptionSettings perception = GetOrCreatePerceptionSettings();
            Set(driver, "_perception", perception);

            var hud = head.AddComponent<CoordinateHUD>();
            Set(hud, "_player", player);
            Set(hud, "_rig", rig);
            Set(hud, "_projection", driver);

            // SPEC §20 W slice viewer (left of the HUD) and §21 parameter panel (right; Tab shows it).
            var sliceViewer = head.AddComponent<WSliceViewer>();
            Set(sliceViewer, "_projection", driver);
            Set(sliceViewer, "_player", player);
            var panel = head.AddComponent<RuntimeParameterPanel>();
            Set(panel, "_settings", perception);

            // Keyboard 4D controls always; mouse-look head only when no headset is active.
            var desktop = worldGo.AddComponent<DesktopTestMode>();
            Set(desktop, "_rig", rig);
            Set(desktop, "_player", player);
            Set(desktop, "_hud", hud);
            Set(desktop, "_panel", panel);
            Set(desktop, "_sliceViewer", sliceViewer);

            // SPEC §23: A, B and C at w = 0, +1, -1. B sits to the right and C to the left, echoing +W / -W.
            var objects = new[]
            {
                AddObject(worldGo, "A  w=0  (cube)", PrimitiveType.Cube, objectMaterial,
                    new Vector4(0f, 1.45f, 2f, 0f), new Vector4(0.18f, 0.18f, 0.18f, 0.2f), default),
                AddObject(worldGo, "B  w=+1 ana  (sphere)", PrimitiveType.Sphere, objectMaterial,
                    new Vector4(0.6f, 1.45f, 2f, 1f), new Vector4(0.18f, 0.18f, 0.18f, 0.2f), default),
                AddObject(worldGo, "C  w=-1 kata  (diamond)", PrimitiveType.Cube, objectMaterial,
                    new Vector4(-0.6f, 1.45f, 2f, -1f), new Vector4(0.15f, 0.15f, 0.15f, 0.2f), new PlaneAngles { XY = 45f }),
                // Spans W from -1.5 to +1.5, so it stays in any slice the player reaches while exploring ana/kata.
                AddObject(worldGo, "D  spans W -1.5..+1.5  (slab)", PrimitiveType.Cube, objectMaterial,
                    new Vector4(0f, 1.05f, 2.4f, 0f), new Vector4(0.9f, 0.04f, 0.25f, 1.5f), default),
            };

            // Place everything once now so the scene view shows the real layout; play mode re-registers on enable.
            foreach (Object4DBehaviour o in objects)
                registry.Register(o);
            driver.ProjectAll();
            foreach (Object4DBehaviour o in objects)
                registry.Unregister(o);

            EditorSceneManager.SaveScene(scene, ScenePath);
            PutFirstInBuildSettings(ScenePath);
            Debug.Log($"[4Dex] Built {ScenePath}.");
        }

        /// <summary>
        /// Renders the lab from the XR camera's placeholder pose to a PNG, HUD included, without saving the scene.
        /// Optional <c>-playerW</c> and <c>-focusW</c> set the 4D state first, for checking perception headlessly;
        /// <c>-rotXW 90</c> (and the other planes) turns the player; <c>-panel</c> shows the parameter panel;
        /// <c>-defaultPerception</c> renders with default tuning instead of the project's settings asset.
        /// Batch (graphics needed, so no -nographics): <c>-executeMethod FourDex.EditorTools.Lab4DSceneBuilder.Snapshot -snapshotPath out.png</c>.
        /// </summary>
        public static void Snapshot()
        {
            string[] args = Environment.GetCommandLineArgs();
            int i = Array.IndexOf(args, "-snapshotPath");
            string path = i >= 0 && i + 1 < args.Length ? args[i + 1] : Path.Combine("Temp", "Lab4D.png");

            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            // Edit mode runs no OnEnable or LateUpdate: register and project by hand, at the requested 4D state.
            var registry = Object.FindAnyObjectByType<World4DRegistry>();
            var player = Object.FindAnyObjectByType<Player4D>();
            var driver = Object.FindAnyObjectByType<ProjectionDriver>();
            if (Array.IndexOf(args, "-defaultPerception") >= 0)
            {
                // Render with factory-default tuning, in memory only: reference images stay reproducible whatever
                // has been tuned into the project's PerceptionSettings asset, and that asset is never touched.
                Set(driver, "_perception", ScriptableObject.CreateInstance<PerceptionSettings>());
            }
            foreach (Object4DBehaviour o in Object.FindObjectsByType<Object4DBehaviour>().OrderBy(o => o.name))
                registry.Register(o);
            player.State.MoveAnaKata(FloatArg(args, "-playerW", 0f));
            player.State.SetFocusW(FloatArg(args, "-focusW", 0f));
            // -rotXW 90 etc.: turn the player in its own planes, in degrees, applied in the listed order.
            foreach (Plane4 plane in Plane4Extensions.All)
            {
                float degrees = FloatArg(args, "-rot" + plane, 0f);
                if (degrees != 0f)
                    player.State.RotateLocal(plane, degrees * Mathf.Deg2Rad);
            }
            driver.ProjectAll();

            var hud = Object.FindAnyObjectByType<CoordinateHUD>();
            // OnEnable does not run in edit mode, so show what desktop mode would put in the footer.
            if (Object.FindAnyObjectByType<DesktopTestMode>() != null)
                hud.Footer = () => DesktopTestMode.Legend;
            hud.Refresh();
            var sliceViewer = Object.FindAnyObjectByType<WSliceViewer>();
            if (sliceViewer != null)
                sliceViewer.Refresh();
            var panel = Object.FindAnyObjectByType<RuntimeParameterPanel>();
            if (panel != null && Array.IndexOf(args, "-panel") >= 0)
                panel.Visible = true;
            Canvas.ForceUpdateCanvases();

            var camera = hud.GetComponent<Camera>();
            var target = new RenderTexture(1600, 900, 24);
            camera.targetTexture = target;
            camera.Render();

            RenderTexture.active = target;
            var image = new Texture2D(target.width, target.height, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
            image.Apply();
            File.WriteAllBytes(path, image.EncodeToPNG());

            RenderTexture.active = null;
            camera.targetTexture = null;
            Object.DestroyImmediate(target);
            Object.DestroyImmediate(image);
            Debug.Log($"[4Dex] Snapshot written to {Path.GetFullPath(path)}.");
        }

        private static float FloatArg(string[] args, string name, float fallback)
        {
            int i = Array.IndexOf(args, name);
            return i >= 0 && i + 1 < args.Length &&
                   float.TryParse(args[i + 1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float v)
                ? v
                : fallback;
        }

        private static Object4DBehaviour AddObject(GameObject parent, string name, PrimitiveType shape, Material material,
            Vector4 position, Vector4 halfExtents, PlaneAngles rotationDegrees)
        {
            GameObject go = GameObject.CreatePrimitive(shape);
            go.name = name;
            go.transform.SetParent(parent.transform, false);
            go.GetComponent<Renderer>().sharedMaterial = material;
            // Unity physics knows nothing about W; a 3D collider here would collide across slices.
            Object.DestroyImmediate(go.GetComponent<Collider>());

            var obj = go.AddComponent<Object4DBehaviour>();
            go.AddComponent<PhaseRenderer>();
            var so = new SerializedObject(obj);
            so.FindProperty("_position").vector4Value = position;
            so.FindProperty("_halfExtents").vector4Value = halfExtents;
            SerializedProperty rotation = so.FindProperty("_rotationDegrees");
            rotation.FindPropertyRelative("XY").floatValue = rotationDegrees.XY;
            rotation.FindPropertyRelative("XZ").floatValue = rotationDegrees.XZ;
            rotation.FindPropertyRelative("YZ").floatValue = rotationDegrees.YZ;
            rotation.FindPropertyRelative("XW").floatValue = rotationDegrees.XW;
            rotation.FindPropertyRelative("YW").floatValue = rotationDegrees.YW;
            rotation.FindPropertyRelative("ZW").floatValue = rotationDegrees.ZW;
            so.ApplyModifiedPropertiesWithoutUndo();
            return obj;
        }

        private static Material GetOrCreateMaterial(string name, Color color, string shaderName)
        {
            Shader shader = Shader.Find(shaderName);
            if (shader == null)
                throw new InvalidOperationException($"[4Dex] Shader '{shaderName}' not found.");

            string path = $"{MaterialFolder}/{name}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null)
            {
                // Keep the asset (and its GUID); only switch the shader if it changed.
                if (material.shader != shader)
                {
                    material.shader = shader;
                    material.SetColor("_BaseColor", color);
                    EditorUtility.SetDirty(material);
                }
                return material;
            }

            Directory.CreateDirectory(MaterialFolder);
            material = new Material(shader) { name = name };
            material.SetColor("_BaseColor", color);
            material.SetFloat("_Smoothness", 0.2f);
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        private static PerceptionSettings GetOrCreatePerceptionSettings()
        {
            var settings = AssetDatabase.LoadAssetAtPath<PerceptionSettings>(PerceptionSettingsPath);
            if (settings != null)
                return settings;
            Directory.CreateDirectory(Path.GetDirectoryName(PerceptionSettingsPath)!);
            settings = ScriptableObject.CreateInstance<PerceptionSettings>();
            AssetDatabase.CreateAsset(settings, PerceptionSettingsPath);
            AssetDatabase.SaveAssets();
            return settings;
        }

        private static void PutFirstInBuildSettings(string scenePath)
        {
            var scenes = EditorBuildSettings.scenes.Where(s => s.path != scenePath).ToList();
            scenes.Insert(0, new EditorBuildSettingsScene(scenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }

        private static void Set(Object target, string field, Object value)
        {
            var so = new SerializedObject(target);
            so.FindProperty(field).objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
