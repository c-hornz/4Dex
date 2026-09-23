using System;
using System.Collections.Generic;
using System.Text;
using FourDex.Math4D;
using FourDex.World;
using UnityEngine;

namespace FourDex.Runtime
{
    /// <summary>One opening carved into an otherwise solid grid: the face of a cell in a direction.</summary>
    [Serializable]
    public struct CellOpening
    {
        public int X, Y, Z, W;
        public Direction4 Direction;

        public Cell4D Cell => new Cell4D(X, Y, Z, W);
    }

    /// <summary>
    /// A hand-authored 4D cell structure in the scene (SPEC §19, Milestone 8): a fully walled grid with the listed
    /// openings carved, wall collision applied to the player, and the current cell reported to the HUD.
    /// </summary>
    /// <remarks>
    /// The maze itself is <see cref="Maze4D"/>, which stores each wall once per shared face, so neighbouring cells
    /// cannot disagree. Walls are ordinary 4D objects in the scene, so a wall one cell away through W is perceived
    /// through the same system as anything else: a dithered echo, not a special case.
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class MazeRuntime : MonoBehaviour
    {
        [SerializeField] private Player4D? _player;
        [SerializeField] private CoordinateHUD? _hud;

        [Tooltip("Cells along X, Y, Z and W. Y is usually 1: there is no climbing yet.")]
        [SerializeField] private Vector4 _size = new Vector4(2f, 1f, 2f, 2f);

        [SerializeField, Min(0.1f)] private float _cellSize = 3f;

        [Tooltip("How close the player's centre may come to a wall.")]
        [SerializeField, Min(0f)] private float _playerRadius = 0.35f;

        [Tooltip("Build the Y faces as floors and ceilings. Only worth it where the grid has more than one level and the player can fly.")]
        [SerializeField] private bool _buildYFaces;

        [Tooltip("Carve the maze procedurally instead of using the authored openings below (SPEC §19, Milestone 9).")]
        [SerializeField] private bool _generate;

        [Tooltip("Seed, W bias and extra openings. Same seed, same maze.")]
        [SerializeField] private MazeGenerationOptions _generation = new MazeGenerationOptions();

        [Tooltip("Faces to open when not generating. Everything else, including the outer boundary, stays walled.")]
        [SerializeField] private List<CellOpening> _openings = new List<CellOpening>();

        private Maze4D? _maze;

        public float CellSize => _cellSize;

        public Maze4D Maze => _maze ??= Build();

        public IReadOnlyList<CellOpening> Openings => _openings;

        /// <summary>The cell the player is standing in.</summary>
        public Cell4D CurrentCell =>
            _player != null ? CellCollision.CellAt(_player.State.Position, _cellSize) : default;

        private void OnEnable()
        {
            if (_player != null)
                _player.State.MovementFilter = Resolve;
            if (_hud != null)
                _hud.Footer = Describe;
        }

        private void OnDisable()
        {
            if (_player != null && _player.State.MovementFilter == Resolve)
                _player.State.MovementFilter = null;
            if (_hud != null)
                _hud.Footer = null;
        }

        private Vec4 Resolve(Vec4 from, Vec4 to) => CellCollision.Resolve(Maze, _cellSize, from, to, _playerRadius);

        /// <summary>Which cell the player is in and which way they can leave it: the Milestone 8 readout.</summary>
        public string Describe()
        {
            Cell4D cell = CurrentCell;
            var sb = new StringBuilder("CELL ").Append(cell);
            if (!Maze.Contains(cell))
                return sb.Append("  (outside the maze)").ToString();

            sb.Append("   exits ");
            int found = 0;
            foreach (Direction4 direction in Direction4Extensions.All)
            {
                if (Maze.HasWall(cell, direction))
                    continue;
                sb.Append(' ').Append(Label(direction));
                found++;
            }
            return found == 0 ? sb.Append(" none").ToString() : sb.ToString();
        }

        private static string Label(Direction4 direction) => direction switch
        {
            Direction4.PosW => "+W ana",
            Direction4.NegW => "-W kata",
            _ => (direction.IsPositive() ? "+" : "-") + "XYZW"[direction.Axis()],
        };

        /// <summary>Every face that needs a wall built, each shared face once, skipping floors and ceilings.</summary>
        public IEnumerable<(Cell4D Cell, Direction4 Direction)> WallFaces()
        {
            foreach (Cell4D cell in Maze.Cells())
            {
                foreach (Direction4 direction in Direction4Extensions.All)
                {
                    // Y faces are skipped in single-level worlds: the floor is a plain 3D reference and a ceiling
                    // would only shut out the light.
                    if (direction.Axis() == 1 && !_buildYFaces)
                        continue;
                    if (!Maze.HasWall(cell, direction))
                        continue;
                    // Shared faces belong to the cell on the negative side, so each is built once; boundary faces
                    // have no neighbour and are built by the cell that has them.
                    Cell4D neighbour = cell.Neighbor(direction);
                    if (Maze.Contains(neighbour) && !direction.IsPositive())
                        continue;
                    yield return (cell, direction);
                }
            }
        }

        /// <summary>Centre and half-extents of the wall slab on one face, in 4D.</summary>
        public (Vector4 Position, Vector4 HalfExtents) WallSlab(Cell4D cell, Direction4 direction, float thickness)
        {
            Vec4 centre = cell.Center(_cellSize);
            int axis = direction.Axis();
            var position = new Vector4(centre.X, centre.Y, centre.Z, centre.W);
            position[axis] += direction.Sign() * 0.5f * _cellSize;

            // Full cell in the other three axes, thin across the face. Its W extent is one cell, so a wall in the
            // next W cell sits a cell away in W and is perceived exactly like any other neighbouring-W geometry.
            var half = new Vector4(_cellSize * 0.5f, _cellSize * 0.5f, _cellSize * 0.5f, _cellSize * 0.5f);
            half[axis] = 0.5f * thickness;
            return (position, half);
        }

        private Maze4D Build()
        {
            int x = Mathf.Max(1, Mathf.RoundToInt(_size.x));
            int y = Mathf.Max(1, Mathf.RoundToInt(_size.y));
            int z = Mathf.Max(1, Mathf.RoundToInt(_size.z));
            int w = Mathf.Max(1, Mathf.RoundToInt(_size.w));

            if (_generate)
                return MazeGenerator4D.Generate(x, y, z, w, _generation);

            var maze = new Maze4D(x, y, z, w);
            foreach (CellOpening opening in _openings)
            {
                Cell4D cell = opening.Cell;
                if (!maze.Contains(cell) || !maze.Contains(cell.Neighbor(opening.Direction)))
                {
                    Debug.LogWarning($"[4Dex] Opening {cell} {opening.Direction} leaves the maze; ignored.", this);
                    continue;
                }
                maze.SetWall(cell, opening.Direction, false);
            }
            return maze;
        }
    }
}
