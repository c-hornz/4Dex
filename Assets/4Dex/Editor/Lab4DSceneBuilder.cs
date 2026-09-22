using System;
using System.IO;
using System.Linq;
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

            Material objectMaterial = GetOrCreateMaterial("LabObject", new Color(0.82f, 0.82f, 0.82f));
            Material floorMaterial = GetOrCreateMaterial("LabFloor", new Color(0.22f, 0.23f, 0.25f));

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

            var hud = head.AddComponent<CoordinateHUD>();
            Set(hud, "_player", player);
            Set(hud, "_rig", rig);

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
        /// Batch (graphics needed, so no -nographics): <c>-executeMethod FourDex.EditorTools.Lab4DSceneBuilder.Snapshot -snapshotPath out.png</c>.
        /// </summary>
        public static void Snapshot()
        {
            string[] args = Environment.GetCommandLineArgs();
            int i = Array.IndexOf(args, "-snapshotPath");
            string path = i >= 0 && i + 1 < args.Length ? args[i + 1] : Path.Combine("Temp", "Lab4D.png");

            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var hud = Object.FindAnyObjectByType<CoordinateHUD>();
            hud.Refresh();
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

        private static Material GetOrCreateMaterial(string name, Color color)
        {
            string path = $"{MaterialFolder}/{name}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null)
                return material;

            Directory.CreateDirectory(MaterialFolder);
            material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = name };
            material.SetColor("_BaseColor", color);
            material.SetFloat("_Smoothness", 0.2f);
            AssetDatabase.CreateAsset(material, path);
            return material;
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
