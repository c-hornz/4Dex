using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using FourDex.InputMapping;
using FourDex.Math4D;
using FourDex.Runtime;
using FourDex.World;
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
        public const string CellsScenePath = "Assets/Scenes/Cells4D.unity";
        public const string MazeScenePath = "Assets/Scenes/Maze4D.unity";
        public const string BoxScenePath = "Assets/Scenes/Box4D.unity";
        public const string RoomsScenePath = "Assets/Scenes/Rooms4D.unity";
        private const string MaterialFolder = "Assets/4Dex/Materials";
        private const string PerceptionSettingsPath = "Assets/4Dex/Settings/PerceptionSettings.asset";
        private const string ComfortSettingsPath = "Assets/4Dex/Settings/ComfortSettings.asset";
        private const string MappingFolder = "Assets/4Dex/Settings/Mapping";

        private static readonly Color Background = new Color(0.08f, 0.09f, 0.11f);

        /// <summary>The parts every 4Dex scene has: the rig, the 4D world root and its drivers, and the debug panels.</summary>
        private readonly struct Common
        {
            public Common(GameObject head, GameObject worldRoot, XRRig rig, Player4D player, World4DRegistry registry,
                ProjectionDriver driver, CoordinateHUD hud, PerceptionSettings perception, Material objectMaterial)
            {
                Head = head;
                WorldRoot = worldRoot;
                Rig = rig;
                Player = player;
                Registry = registry;
                Driver = driver;
                Hud = hud;
                Perception = perception;
                ObjectMaterial = objectMaterial;
            }

            public readonly GameObject Head;
            public readonly GameObject WorldRoot;
            public readonly XRRig Rig;
            public readonly Player4D Player;
            public readonly World4DRegistry Registry;
            public readonly ProjectionDriver Driver;
            public readonly CoordinateHUD Hud;
            public readonly PerceptionSettings Perception;
            public readonly Material ObjectMaterial;
        }

        [MenuItem("4Dex/Rebuild Lab4D Scene")]
        public static void Build()
        {
            (Scene scene, Common common) = CreateCommon();
            Material objectMaterial = common.ObjectMaterial;
            GameObject worldGo = common.WorldRoot;
            World4DRegistry registry = common.Registry;
            ProjectionDriver driver = common.Driver;

            Material floorMaterial = GetOrCreateMaterial("LabFloor", new Color(0.30f, 0.31f, 0.33f), "4Dex/Phase4D");
            // Plain 3D, not a 4D object: a stable conventional reference for comfort and for judging head tracking.
            GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floor.name = "Floor (3D reference, not 4D)";
            floor.transform.localScale = new Vector3(1f, 1f, 1f); // 10 m x 10 m: room for the shadow field
            floor.GetComponent<Renderer>().sharedMaterial = floorMaterial;

            BuildLabObjects(worldGo, objectMaterial, registry, driver);

            EditorSceneManager.SaveScene(scene, ScenePath);
            PutFirstInBuildSettings(ScenePath);
            Debug.Log($"[4Dex] Built {ScenePath}.");
        }

        private static (Scene Scene, Common Common) CreateCommon()
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
            light.transform.rotation = Quaternion.Euler(55f, 25f, 0f); // steep enough that shadows land on the floor, not past it

            // Receivers and casters share one Phase4D material: with no PhaseRenderer it keeps the shader's in-focus
            // defaults, i.e. ordinary solid geometry that still receives the 4D shadow field.
            Material objectMaterial = GetOrCreateMaterial("LabObject", new Color(0.82f, 0.82f, 0.82f), "4Dex/Phase4D");

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

            // SPEC §5/§6 head mapping: the rig's second pose consumer. Starts on CLASSIC, i.e. purely conventional.
            var mapping = worldGo.AddComponent<HeadMappingDriver>();
            Set(mapping, "_rig", rig);
            Set(mapping, "_player", player);
            SetList(mapping, "_profiles", GetOrCreateMappingProfiles());

            var hud = head.AddComponent<CoordinateHUD>();
            Set(hud, "_player", player);
            Set(hud, "_rig", rig);
            Set(hud, "_projection", driver);
            Set(hud, "_mapping", mapping);

            // SPEC §20 W slice viewer (left of the HUD) and §21 parameter panel (right; Tab shows it).
            var sliceViewer = head.AddComponent<WSliceViewer>();
            Set(sliceViewer, "_projection", driver);
            Set(sliceViewer, "_player", player);
            var panel = head.AddComponent<RuntimeParameterPanel>();
            Set(panel, "_settings", perception);
            Set(panel, "_mapping", mapping);

            // SPEC §22 comfort: the vignette rides on the camera, and its settings are shared with the controls.
            ComfortSettings comfort = GetOrCreateComfortSettings();
            Set(panel, "_comfort", comfort);
            var vignette = head.AddComponent<ComfortVignette>();
            Set(vignette, "_player", player);
            Set(vignette, "_settings", comfort);

            // Keyboard 4D controls always; mouse-look head only when no headset is active.
            var desktop = worldGo.AddComponent<DesktopTestMode>();
            Set(desktop, "_rig", rig);
            Set(desktop, "_player", player);
            Set(desktop, "_hud", hud);
            Set(desktop, "_panel", panel);
            Set(desktop, "_sliceViewer", sliceViewer);
            Set(desktop, "_mapping", mapping);
            Set(desktop, "_projection", driver);
            Set(desktop, "_comfort", comfort);

            return (scene, new Common(head, worldGo, rig, player, registry, driver, hud, perception, objectMaterial));
        }

        /// <summary>SPEC §23: A, B and C at w = 0, +1, -1. B sits to the right and C to the left, echoing +W / -W.</summary>
        private static void BuildLabObjects(GameObject worldGo, Material objectMaterial, World4DRegistry registry, ProjectionDriver driver)
        {
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
                // Beyond band C from w = 0: OUTSIDE normally, so they are what survey mode (SPEC §10) reveals.
                AddObject(worldGo, "E  w=+2 ana, survey  (sphere)", PrimitiveType.Sphere, objectMaterial,
                    new Vector4(0.35f, 1.9f, 2.3f, 2f), new Vector4(0.12f, 0.12f, 0.12f, 0.2f), default),
                AddObject(worldGo, "F  w=-2 kata, survey  (cube)", PrimitiveType.Cube, objectMaterial,
                    new Vector4(-0.35f, 1.9f, 2.3f, -2f), new Vector4(0.1f, 0.1f, 0.1f, 0.2f), default),
            };

            // Place everything once now so the scene view shows the real layout; play mode re-registers on enable.
            foreach (Object4DBehaviour o in objects)
                registry.Register(o);
            driver.ProjectAll();
            foreach (Object4DBehaviour o in objects)
                registry.Unregister(o);
        }

        /// <summary>
        /// Generates <c>Assets/Scenes/Cells4D.unity</c>: Milestone 8's hand-authored 4D cell structure. A 2x1x2x2 grid
        /// of 3 m cells, fully walled except for a route that can only be completed by going ana and kata.
        /// </summary>
        [MenuItem("4Dex/Rebuild Cells4D Scene")]
        public static void BuildCells()
        {
            (Scene scene, Common common) = CreateCommon();

            // The route: +X, then ana to w = 1, then +Z, then kata back to w = 0. The last leg is only reachable
            // through W, so finishing the loop needs ana/kata, not just ordinary directions (SPEC §24, M8).
            var openings = new List<CellOpening>
            {
                Open(0, 0, 0, 0, Direction4.PosX),
                Open(1, 0, 0, 0, Direction4.PosW),
                Open(1, 0, 0, 1, Direction4.PosZ),
                Open(1, 0, 1, 1, Direction4.NegX),
                Open(0, 0, 1, 1, Direction4.NegW),
                // A dead end, so the structure is a small maze rather than one corridor.
                Open(0, 0, 0, 1, Direction4.PosZ),
            };

            var maze = common.WorldRoot.AddComponent<MazeRuntime>();
            Set(maze, "_player", common.Player);
            Set(maze, "_hud", common.Hud);
            var mazeObject = new SerializedObject(maze);
            mazeObject.FindProperty("_size").vector4Value = new Vector4(2f, 1f, 2f, 2f);
            SerializedProperty list = mazeObject.FindProperty("_openings");
            list.arraySize = openings.Count;
            for (int i = 0; i < openings.Count; i++)
            {
                SerializedProperty element = list.GetArrayElementAtIndex(i);
                element.FindPropertyRelative("X").intValue = openings[i].X;
                element.FindPropertyRelative("Y").intValue = openings[i].Y;
                element.FindPropertyRelative("Z").intValue = openings[i].Z;
                element.FindPropertyRelative("W").intValue = openings[i].W;
                element.FindPropertyRelative("Direction").enumValueIndex = (int)openings[i].Direction;
            }
            mazeObject.ApplyModifiedPropertiesWithoutUndo();

            Material floorMaterial = GetOrCreateMaterial("LabFloor", new Color(0.30f, 0.31f, 0.33f), "4Dex/Phase4D");
            GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floor.name = "Floor (3D reference, not 4D)";
            floor.transform.localScale = new Vector3(1.2f, 1f, 1.2f);
            floor.transform.position = new Vector3(1.5f, 0f, 1.5f); // under the middle of the grid
            floor.GetComponent<Renderer>().sharedMaterial = floorMaterial;

            // Walls are ordinary 4D objects, so a wall one cell away in W is perceived like any other neighbouring
            // geometry: a dithered echo you can survey, not a special case.
            var walls = new List<Object4DBehaviour>();
            foreach ((Cell4D cell, Direction4 direction) in maze.WallFaces())
            {
                (Vector4 position, Vector4 half) = maze.WallSlab(cell, direction, 0.2f);
                walls.Add(AddObject(common.WorldRoot, $"Wall {cell.X}{cell.Y}{cell.Z}{cell.W} {direction}", PrimitiveType.Cube,
                    WallMaterial(), position, half, default));
            }

            foreach (Object4DBehaviour wall in walls)
                common.Registry.Register(wall);
            common.Driver.ProjectAll();
            foreach (Object4DBehaviour wall in walls)
                common.Registry.Unregister(wall);

            EditorSceneManager.SaveScene(scene, CellsScenePath);
            Debug.Log($"[4Dex] Built {CellsScenePath} with {walls.Count} walls and {openings.Count} openings.");
        }

        /// <summary>
        /// Generates <c>Assets/Scenes/Maze4D.unity</c>: Milestone 9's procedural maze. A 3x1x3x2 grid carved by
        /// <see cref="MazeGenerator4D"/> with a W bias, so routes leave the slice often.
        /// </summary>
        [MenuItem("4Dex/Rebuild Maze4D Scene")]
        public static void BuildMaze()
        {
            (Scene scene, Common common) = CreateCommon();

            var size = new Vector4(3f, 1f, 3f, 2f);
            var maze = common.WorldRoot.AddComponent<MazeRuntime>();
            Set(maze, "_player", common.Player);
            Set(maze, "_hud", common.Hud);
            var mazeObject = new SerializedObject(maze);
            mazeObject.FindProperty("_size").vector4Value = size;
            mazeObject.FindProperty("_generate").boolValue = true;
            SerializedProperty generation = mazeObject.FindProperty("_generation");
            generation.FindPropertyRelative("Seed").intValue = 4;
            // Above 0.5, so the carve crosses W more often than it would by chance: a 4D maze should need ana/kata.
            generation.FindPropertyRelative("WBias").floatValue = 0.65f;
            generation.FindPropertyRelative("ExtraOpenings").intValue = 2;
            mazeObject.ApplyModifiedPropertiesWithoutUndo();

            Material floorMaterial = GetOrCreateMaterial("LabFloor", new Color(0.30f, 0.31f, 0.33f), "4Dex/Phase4D");
            GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floor.name = "Floor (3D reference, not 4D)";
            floor.transform.localScale = new Vector3(1.6f, 1f, 1.6f);
            floor.transform.position = new Vector3(3f, 0f, 3f); // under the middle of the 3x3 grid
            floor.GetComponent<Renderer>().sharedMaterial = floorMaterial;

            var walls = new List<Object4DBehaviour>();
            foreach ((Cell4D cell, Direction4 direction) in maze.WallFaces())
            {
                (Vector4 position, Vector4 half) = maze.WallSlab(cell, direction, 0.2f);
                walls.Add(AddObject(common.WorldRoot, $"Wall {cell.X}{cell.Y}{cell.Z}{cell.W} {direction}", PrimitiveType.Cube,
                    WallMaterial(), position, half, default));
            }

            foreach (Object4DBehaviour wall in walls)
                common.Registry.Register(wall);
            common.Driver.ProjectAll();
            foreach (Object4DBehaviour wall in walls)
                common.Registry.Unregister(wall);

            EditorSceneManager.SaveScene(scene, MazeScenePath);
            int throughW = MazeGenerator4D.CountOpenings(maze.Maze, throughWOnly: true);
            Debug.Log($"[4Dex] Built {MazeScenePath}: {walls.Count} walls, " +
                      $"{MazeGenerator4D.CountOpenings(maze.Maze, false)} openings ({throughW} through W), " +
                      $"connected: {MazeGenerator4D.IsConnected(maze.Maze)}.");
        }

        /// <summary>
        /// Generates <c>Assets/Scenes/Box4D.unity</c>: one closed 6 m room with three shapes at w = 0, +1 and -1.
        /// The lab without the open horizon, so perception is judged against walls at a known distance.
        /// </summary>
        [MenuItem("4Dex/Rebuild Box4D Scene")]
        public static void BuildBox()
        {
            (Scene scene, Common common) = CreateCommon();

            var maze = common.WorldRoot.AddComponent<MazeRuntime>();
            Set(maze, "_player", common.Player);
            Set(maze, "_hud", common.Hud);
            var mazeObject = new SerializedObject(maze);
            mazeObject.FindProperty("_size").vector4Value = new Vector4(1f, 1f, 1f, 1f);
            mazeObject.FindProperty("_cellSize").floatValue = 6f;
            mazeObject.ApplyModifiedPropertiesWithoutUndo();

            AddFloor(new Vector3(0.8f, 1f, 0.8f), Vector3.zero);

            var objects = new List<Object4DBehaviour>();
            foreach ((Cell4D cell, Direction4 direction) in maze.WallFaces())
            {
                (Vector4 position, Vector4 half) = maze.WallSlab(cell, direction, 0.2f);
                objects.Add(AddObject(common.WorldRoot, $"Wall {direction}", PrimitiveType.Cube, WallMaterial(),
                    position, half, default));
            }

            // Three shapes, the SPEC §23 layout, inside the room: same place in 3D, one W apart.
            objects.Add(AddObject(common.WorldRoot, "A  w=0  (cube)", PrimitiveType.Cube, common.ObjectMaterial,
                new Vector4(0f, 1.3f, 1.6f, 0f), new Vector4(0.25f, 0.25f, 0.25f, 0.25f), default));
            objects.Add(AddObject(common.WorldRoot, "B  w=+1 ana  (sphere)", PrimitiveType.Sphere, common.ObjectMaterial,
                new Vector4(1f, 1.3f, 1.6f, 1f), new Vector4(0.25f, 0.25f, 0.25f, 0.25f), default));
            objects.Add(AddObject(common.WorldRoot, "C  w=-1 kata  (diamond)", PrimitiveType.Cube, common.ObjectMaterial,
                new Vector4(-1f, 1.3f, 1.6f, -1f), new Vector4(0.22f, 0.22f, 0.22f, 0.25f), new PlaneAngles { XY = 45f }));

            PlaceOnce(common, objects);
            EditorSceneManager.SaveScene(scene, BoxScenePath);
            Debug.Log($"[4Dex] Built {BoxScenePath}: one room, {objects.Count - 3} walls, 3 shapes.");
        }

        /// <summary>
        /// Generates <c>Assets/Scenes/Rooms4D.unity</c>: a 3x3x3x3 grid of rooms, 81 of them, with nine distinct
        /// shapes spread through it. Needs the fly keys, since the grid has three levels in Y.
        /// </summary>
        [MenuItem("4Dex/Rebuild Rooms4D Scene")]
        public static void BuildRooms()
        {
            (Scene scene, Common common) = CreateCommon();

            const float cell = 4f;
            var maze = common.WorldRoot.AddComponent<MazeRuntime>();
            Set(maze, "_player", common.Player);
            Set(maze, "_hud", common.Hud);
            var mazeObject = new SerializedObject(maze);
            mazeObject.FindProperty("_size").vector4Value = new Vector4(3f, 3f, 3f, 3f);
            mazeObject.FindProperty("_cellSize").floatValue = cell;
            mazeObject.FindProperty("_buildYFaces").boolValue = true; // three levels, so floors and ceilings matter
            mazeObject.FindProperty("_generate").boolValue = true;
            SerializedProperty generation = mazeObject.FindProperty("_generation");
            generation.FindPropertyRelative("Seed").intValue = 11;
            generation.FindPropertyRelative("WBias").floatValue = 0.5f;
            // Plenty of loops: this is a place to wander and compare perception, not a maze to solve.
            generation.FindPropertyRelative("ExtraOpenings").intValue = 60;
            mazeObject.ApplyModifiedPropertiesWithoutUndo();

            // No plain floor plane here: with Y faces built, the grid's own bottom boundary is the floor, and a plane
            // would cut through the lowest rooms. Start on the middle level, where the shapes are.
            Set(common.Player, "_startPosition", new Vector4(0f, 3f, 0f, 0f));

            var objects = new List<Object4DBehaviour>();
            foreach ((Cell4D cell4, Direction4 direction) in maze.WallFaces())
            {
                (Vector4 position, Vector4 half) = maze.WallSlab(cell4, direction, 0.18f);
                objects.Add(AddObject(common.WorldRoot, $"Wall {cell4.X}{cell4.Y}{cell4.Z}{cell4.W} {direction}",
                    PrimitiveType.Cube, WallMaterial(), position, half, default));
            }

            // Nine distinct silhouettes at assorted cells and W levels: some share a room with you, some are one
            // room away, some are only a W step away and invisible until you focus or survey.
            (string Name, PrimitiveType Shape, Vector4 Cell, Vector4 Half, PlaneAngles Turn)[] shapes =
            {
                ("S1 cube", PrimitiveType.Cube, new Vector4(0, 1, 0, 1), Q(0.3f), default),
                ("S2 sphere", PrimitiveType.Sphere, new Vector4(1, 1, 0, 1), Q(0.3f), default),
                ("S3 diamond", PrimitiveType.Cube, new Vector4(2, 1, 0, 1), Q(0.28f), new PlaneAngles { XY = 45f }),
                ("S4 pillar", PrimitiveType.Cube, new Vector4(0, 1, 1, 1), new Vector4(0.2f, 0.9f, 0.2f, 0.3f), default),
                ("S5 slab", PrimitiveType.Cube, new Vector4(1, 1, 1, 0), new Vector4(0.9f, 0.08f, 0.5f, 0.3f), default),
                ("S6 tilted", PrimitiveType.Cube, new Vector4(2, 1, 1, 2), Q(0.3f), new PlaneAngles { XW = 45f }),
                ("S7 small", PrimitiveType.Sphere, new Vector4(0, 1, 2, 2), Q(0.18f), default),
                ("S8 plank", PrimitiveType.Cube, new Vector4(1, 1, 2, 2), new Vector4(0.6f, 0.3f, 0.12f, 0.3f), default),
                ("S9 wide", PrimitiveType.Cube, new Vector4(2, 1, 2, 0), new Vector4(0.7f, 0.25f, 0.7f, 0.3f), default),
            };
            foreach ((string name, PrimitiveType shape, Vector4 at, Vector4 half, PlaneAngles turn) in shapes)
            {
                // Cell centre, lifted to about chest height in that room.
                var position = new Vector4(at.x * cell, at.y * cell - 0.8f, at.z * cell, at.w * cell);
                objects.Add(AddObject(common.WorldRoot, name, shape, common.ObjectMaterial, position, half, turn));
            }

            PlaceOnce(common, objects);
            EditorSceneManager.SaveScene(scene, RoomsScenePath);
            Debug.Log($"[4Dex] Built {RoomsScenePath}: {maze.Maze.CellCount} rooms, {objects.Count - shapes.Length} walls, " +
                      $"{shapes.Length} shapes, connected: {MazeGenerator4D.IsConnected(maze.Maze)}.");
        }

        private static Vector4 Q(float half) => new Vector4(half, half, half, half);

        /// <summary>Walls are darker than the shapes, so a solid shape does not vanish against a solid wall.</summary>
        private static Material WallMaterial() => GetOrCreateMaterial("LabWall", new Color(0.42f, 0.43f, 0.46f), "4Dex/Phase4D");

        private static void AddFloor(Vector3 scale, Vector3 position)
        {
            Material floorMaterial = GetOrCreateMaterial("LabFloor", new Color(0.30f, 0.31f, 0.33f), "4Dex/Phase4D");
            GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floor.name = "Floor (3D reference, not 4D)";
            floor.transform.localScale = scale;
            floor.transform.position = position;
            floor.GetComponent<Renderer>().sharedMaterial = floorMaterial;
        }

        /// <summary>Registers, projects once so the saved scene shows the real layout, then unregisters.</summary>
        private static void PlaceOnce(Common common, List<Object4DBehaviour> objects)
        {
            foreach (Object4DBehaviour o in objects)
                common.Registry.Register(o);
            common.Driver.ProjectAll();
            foreach (Object4DBehaviour o in objects)
                common.Registry.Unregister(o);
        }

        private static CellOpening Open(int x, int y, int z, int w, Direction4 direction) =>
            new CellOpening { X = x, Y = y, Z = z, W = w, Direction = direction };

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

            // -scene Cells4D renders the cell structure instead of the lab.
            int sceneArg = Array.IndexOf(args, "-scene");
            string requested = sceneArg >= 0 && sceneArg + 1 < args.Length ? args[sceneArg + 1] : "";
            string scenePath = requested.Contains("Cells") ? CellsScenePath
                : requested.Contains("Maze") ? MazeScenePath
                : requested.Contains("Box") ? BoxScenePath
                : requested.Contains("Rooms") ? RoomsScenePath
                : ScenePath;
            EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);

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
            player.State.MoveWorld(new Vec4(FloatArg(args, "-playerX", 0f), 0f, FloatArg(args, "-playerZ", 0f), 0f));
            player.State.MoveAnaKata(FloatArg(args, "-playerW", 0f));
            player.State.SetFocusW(FloatArg(args, "-focusW", 0f));
            driver.ForceSurvey(FloatArg(args, "-survey", 0f));
            // -vignette 0.5 shows the comfort vignette half closed; nothing moves in edit mode to close it by itself.
            float aperture = FloatArg(args, "-vignette", 0f);
            var vignette = Object.FindAnyObjectByType<ComfortVignette>();
            if (aperture > 0f && vignette != null)
                vignette.Preview(aperture);
            // -rotXW 90 etc.: turn the player in its own planes, in degrees, applied in the listed order.
            foreach (Plane4 plane in Plane4Extensions.All)
            {
                float degrees = FloatArg(args, "-rot" + plane, 0f);
                if (degrees != 0f)
                    player.State.RotateLocal(plane, degrees * Mathf.Deg2Rad);
            }
            driver.ProjectAll();

            var hud = Object.FindAnyObjectByType<CoordinateHUD>();
            // -lookDown tilts the camera (the HUD lives on it) toward the floor, where the shadow field lands, and
            // -cameraY raises it: mazes have no ceilings, so from above the layout reads as a map.
            float lookDown = FloatArg(args, "-lookDown", 0f);
            if (lookDown != 0f)
                hud.transform.localRotation = Quaternion.Euler(lookDown, 0f, 0f);
            float cameraY = FloatArg(args, "-cameraY", 0f);
            if (cameraY != 0f)
            {
                Vector3 local = hud.transform.localPosition;
                hud.transform.localPosition = new Vector3(local.x, cameraY, local.z);
            }
            // OnEnable does not run in edit mode, so show what desktop mode would put in the footer.
            if (Object.FindAnyObjectByType<DesktopTestMode>() != null)
                hud.Help = () => DesktopTestMode.Legend;
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

        private static ComfortSettings GetOrCreateComfortSettings()
        {
            var settings = AssetDatabase.LoadAssetAtPath<ComfortSettings>(ComfortSettingsPath);
            if (settings != null)
                return settings;
            Directory.CreateDirectory(Path.GetDirectoryName(ComfortSettingsPath)!);
            settings = ScriptableObject.CreateInstance<ComfortSettings>();
            AssetDatabase.CreateAsset(settings, ComfortSettingsPath);
            AssetDatabase.SaveAssets();
            return settings;
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

        /// <summary>One asset per SPEC §6 preset. Existing assets are kept as they are: they may hold tuning.</summary>
        private static List<InputMappingProfile> GetOrCreateMappingProfiles()
        {
            Directory.CreateDirectory(MappingFolder);
            var profiles = new List<InputMappingProfile>();
            foreach (string name in MappingPresets.Names)
            {
                string path = $"{MappingFolder}/{name}.asset";
                var asset = AssetDatabase.LoadAssetAtPath<InputMappingProfile>(path);
                if (asset == null)
                {
                    asset = ScriptableObject.CreateInstance<InputMappingProfile>();
                    asset.Profile = MappingPresets.Create(name);
                    AssetDatabase.CreateAsset(asset, path);
                }
                profiles.Add(asset);
            }
            AssetDatabase.SaveAssets();
            return profiles;
        }

        private static void SetList<T>(Object target, string field, List<T> values) where T : Object
        {
            var so = new SerializedObject(target);
            SerializedProperty list = so.FindProperty(field);
            list.arraySize = values.Count;
            for (int i = 0; i < values.Count; i++)
                list.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void PutFirstInBuildSettings(string scenePath)
        {
            var scenes = EditorBuildSettings.scenes.Where(s => s.path != scenePath).ToList();
            scenes.Insert(0, new EditorBuildSettingsScene(scenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }

        private static void Set(Object target, string field, Vector4 value)
        {
            var so = new SerializedObject(target);
            so.FindProperty(field).vector4Value = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void Set(Object target, string field, Object value)
        {
            var so = new SerializedObject(target);
            so.FindProperty(field).objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
