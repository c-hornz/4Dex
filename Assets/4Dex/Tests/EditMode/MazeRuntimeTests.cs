using System.Linq;
using FourDex.Math4D;
using FourDex.Runtime;
using FourDex.World;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace FourDex.Tests.EditMode
{
    /// <summary>Milestone 8: the player navigates cells through ordinary directions and ana/kata, and walls stop them.</summary>
    public class MazeRuntimeTests
    {
        private GameObject _root;
        private Player4D _player;
        private MazeRuntime _maze;

        private const float CellSize = 3f;

        [SetUp]
        public void SetUp()
        {
            _root = new GameObject("World");
            _player = _root.AddComponent<Player4D>();
            _maze = _root.AddComponent<MazeRuntime>();
            var so = new SerializedObject(_maze);
            so.FindProperty("_player").objectReferenceValue = _player;
            so.FindProperty("_size").vector4Value = new Vector4(2f, 1f, 2f, 2f);
            so.FindProperty("_cellSize").floatValue = CellSize;
            SerializedProperty openings = so.FindProperty("_openings");
            openings.arraySize = 2;
            SetOpening(openings.GetArrayElementAtIndex(0), 0, 0, 0, 0, Direction4.PosX);
            SetOpening(openings.GetArrayElementAtIndex(1), 1, 0, 0, 0, Direction4.PosW);
            so.ApplyModifiedPropertiesWithoutUndo();

            // OnEnable does not run in edit mode; install the filter the way it does.
            _player.State.MovementFilter = (from, to) =>
                CellCollision.Resolve(_maze.Maze, _maze.CellSize, from, to, 0.35f);
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(_root);

        private static void SetOpening(SerializedProperty element, int x, int y, int z, int w, Direction4 direction)
        {
            element.FindPropertyRelative("X").intValue = x;
            element.FindPropertyRelative("Y").intValue = y;
            element.FindPropertyRelative("Z").intValue = z;
            element.FindPropertyRelative("W").intValue = w;
            element.FindPropertyRelative("Direction").enumValueIndex = (int)direction;
        }

        [Test]
        public void ThePlayerWalksThroughOpenings_AndAnaKataIsJustAnotherDirection()
        {
            // The Milestone 8 acceptance criterion: reach the far cell using an ordinary direction and then ana.
            Assert.AreEqual(new Cell4D(0, 0, 0, 0), _maze.CurrentCell);

            _player.State.MoveWorld(new Vec4(CellSize, 0f, 0f, 0f));
            Assert.AreEqual(new Cell4D(1, 0, 0, 0), _maze.CurrentCell);

            _player.State.MoveAnaKata(CellSize);
            Assert.AreEqual(new Cell4D(1, 0, 0, 1), _maze.CurrentCell);
        }

        [Test]
        public void WallsStopThePlayer_InWAsInAnyOtherDirection()
        {
            _player.State.MoveAnaKata(CellSize); // +W is walled from the starting cell
            Assert.AreEqual(new Cell4D(0, 0, 0, 0), _maze.CurrentCell);
            Assert.Less(_player.State.Position.W, 1.5f);

            _player.State.MoveWorld(new Vec4(0f, 0f, CellSize, 0f)); // +Z is walled too
            Assert.AreEqual(new Cell4D(0, 0, 0, 0), _maze.CurrentCell);
        }

        [Test]
        public void TheReadoutNamesTheCellAndItsExits()
        {
            string text = _maze.Describe();
            StringAssert.Contains("CELL [0, 0, 0, 0]", text);
            StringAssert.Contains("+X", text);
            Assert.IsFalse(text.Contains("ana"), "the starting cell has no way ana");

            _player.State.MoveWorld(new Vec4(CellSize, 0f, 0f, 0f));
            StringAssert.Contains("+W ana", _maze.Describe());
        }

        [Test]
        public void EveryClosedFaceGetsOneWall_AndOpeningsGetNone()
        {
            var faces = _maze.WallFaces().ToList();

            // Each face appears once: no duplicate wall between the same pair of cells.
            Assert.AreEqual(faces.Count, faces.Select(f => Key(f.Cell, f.Direction)).Distinct().Count());
            // Nothing is built across an opening, and no floors or ceilings are built.
            Assert.IsFalse(faces.Any(f => f.Cell == new Cell4D(0, 0, 0, 0) && f.Direction == Direction4.PosX));
            Assert.IsFalse(faces.Any(f => f.Direction.Axis() == 1));
            Assert.IsTrue(faces.Any(f => f.Direction == Direction4.PosW), "walls close the W faces as well");
        }

        private static string Key(Cell4D cell, Direction4 direction)
        {
            // Both sides of a shared face map to the same key.
            Cell4D owner = direction.IsPositive() ? cell : cell.Neighbor(direction);
            return $"{owner}:{direction.Axis()}";
        }

        [Test]
        public void GeneratedMazes_AreConnected_AndCrossW()
        {
            // Milestone 9 in the scene: flipping the generate flag carves the maze instead of using the openings.
            var so = new SerializedObject(_maze);
            so.FindProperty("_size").vector4Value = new Vector4(3f, 1f, 3f, 2f);
            so.FindProperty("_generate").boolValue = true;
            SerializedProperty generation = so.FindProperty("_generation");
            generation.FindPropertyRelative("Seed").intValue = 4;
            generation.FindPropertyRelative("WBias").floatValue = 0.65f;
            so.ApplyModifiedPropertiesWithoutUndo();

            Maze4D maze = _maze.Maze;

            Assert.IsTrue(MazeGenerator4D.IsConnected(maze));
            Assert.Greater(MazeGenerator4D.CountOpenings(maze, throughWOnly: true), 0, "a 4D maze must use ana/kata");
            Assert.AreEqual(maze.CellCount - 1, MazeGenerator4D.CountOpenings(maze, throughWOnly: false));
            Assert.Greater(_maze.WallFaces().Count(), 0);
        }

        [Test]
        public void YFaces_AreBuiltOnlyWhenAsked()
        {
            // Single-level worlds skip floors and ceilings; multi-level ones need them, or flying goes through them.
            Assert.IsFalse(_maze.WallFaces().Any(f => f.Direction.Axis() == 1));

            var so = new SerializedObject(_maze);
            so.FindProperty("_size").vector4Value = new Vector4(2f, 3f, 2f, 2f);
            so.FindProperty("_buildYFaces").boolValue = true;
            so.ApplyModifiedPropertiesWithoutUndo();

            Assert.IsTrue(_maze.WallFaces().Any(f => f.Direction.Axis() == 1));
        }

        [Test]
        public void WallSlabs_SitOnTheirFace_AndSpanOneCellThroughW()
        {
            (Vector4 position, Vector4 half) = _maze.WallSlab(new Cell4D(0, 0, 0, 0), Direction4.PosX, 0.2f);

            Assert.AreEqual(1.5f, position.x, 1e-4f, "on the face, not in the cell centre");
            Assert.AreEqual(0.1f, half.x, 1e-4f, "thin across the face");
            Assert.AreEqual(CellSize * 0.5f, half.w, 1e-4f, "and one cell thick through W");
        }
    }
}
