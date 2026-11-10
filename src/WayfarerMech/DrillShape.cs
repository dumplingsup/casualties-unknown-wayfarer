using System;

namespace WayfarerMech.Core
{
    /// <summary>
    /// A cell selected by the drill planner. DistanceSq is measured from the mech centre so
    /// the caller can apply a nearest-first budget. Unity-free by design: tests link this file.
    /// </summary>
    public struct GridCell
    {
        public int X;
        public int Y;
        public float DistanceSq;
    }

    /// <summary>Read-only view of the voxel grid used by the pure planner.</summary>
    public interface IDrillGrid
    {
        bool InBounds(int x, int y);
        int GetBlock(int x, int y);
        float GetBlockHealth(int x, int y);
    }

    /// <summary>
    /// Corridor-shaped drill face. The hull is projected onto the plane perpendicular to the
    /// aim direction (0.5 world-unit margin), then each column walks from the hull outward and
    /// stops at the first solid cell. Only a limited depth behind that surface is returned.
    /// </summary>
    public static class DrillShape
    {
        // Blocks with this much health are treated as world boundaries and never drilled.
        public const float IndestructibleHealth = 100000f;
        public const int MaxColumns = 64;
        public const int MaxSteps = 128;

        /// <summary>
        /// Plans one drill pulse.
        /// </summary>
        /// <param name="centerWorldX">Mech centre X in world units.</param>
        /// <param name="centerWorldY">Mech centre Y in world units.</param>
        /// <param name="gridOffsetX">World-grid offset (WorldGeneration.halfWidth).</param>
        /// <param name="gridOffsetY">World-grid offset (WorldGeneration.halfHeight).</param>
        /// <param name="hullWidth">Collision hull width in world units.</param>
        /// <param name="hullHeight">Collision hull height in world units.</param>
        /// <param name="dirX">Normalized aim X.</param>
        /// <param name="dirY">Normalized aim Y.</param>
        /// <param name="maxCenterRange">Maximum distance from mech centre (world units).</param>
        /// <param name="maxDepth">Maximum solid cells per column, from first surface inward.</param>
        /// <param name="maxCells">Per-pulse total budget after nearest-first sorting.</param>
        /// <param name="grid">Grid query implementation.</param>
        /// <param name="output">Reusable output buffer; must be at least maxCells long.</param>
        /// <param name="candidateCount">Unique cells found before the per-pulse budget.</param>
        /// <returns>Number of cells written to output.</returns>
        public static int Plan(
            float centerWorldX, float centerWorldY,
            float gridOffsetX, float gridOffsetY,
            float hullWidth, float hullHeight,
            float dirX, float dirY,
            float maxCenterRange, int maxDepth, int maxCells,
            IDrillGrid grid, GridCell[] output, out int candidateCount)
        {
            if (grid == null) throw new ArgumentNullException(nameof(grid));
            if (output == null) throw new ArgumentNullException(nameof(output));
            if (output.Length < maxCells) throw new ArgumentException("Output buffer is smaller than maxCells", nameof(output));

            candidateCount = 0;
            if (maxCells <= 0 || maxDepth <= 0 || maxCenterRange <= 0f) return 0;

            float len = (float)Math.Sqrt(dirX * dirX + dirY * dirY);
            if (len < 1e-5f) return 0;
            dirX /= len;
            dirY /= len;

            float absX = Math.Abs(dirX);
            float absY = Math.Abs(dirY);
            // Project the hull's AABB onto the aim axis and the perpendicular axis.
            float startDist = absX * hullWidth * 0.5f + absY * hullHeight * 0.5f + 0.5f;
            float halfSpan = absY * hullWidth * 0.5f + absX * hullHeight * 0.5f + 0.5f;
            if (startDist >= maxCenterRange) return 0;

            float centerGX = centerWorldX + gridOffsetX;
            float centerGY = centerWorldY + gridOffsetY;
            // Outward-rounded count of grid columns across the projected width. Rays are
            // placed at the centre of each equal segment so no column is sampled twice.
            int columns = Math.Max(1, (int)Math.Ceiling(halfSpan * 2f));
            if (columns > MaxColumns) columns = MaxColumns;

            int count = 0;
            float maxRangeSq = maxCenterRange * maxCenterRange;
            float maxLength = maxCenterRange - startDist;

            for (int i = 0; i < columns; i++)
            {
                float offset = columns == 1
                    ? 0f
                    : -halfSpan + ((float)i + 0.5f) * (halfSpan * 2f / columns);
                // Perpendicular p = (-dirY, dirX).
                float startX = centerGX - dirY * offset + dirX * startDist;
                float startY = centerGY + dirX * offset + dirY * startDist;
                count = WalkColumn(startX, startY, dirX, dirY, centerGX, centerGY,
                    maxRangeSq, maxLength, maxDepth, grid, output, count, ref candidateCount);
                if (count >= output.Length) break;
            }

            // Nearest cells first, then enforce the pulse budget. Keeping the sort in the pure
            // planner means the same order is used by the game and by tests.
            for (int i = 1; i < count; i++)
            {
                var item = output[i];
                int j = i - 1;
                while (j >= 0 && output[j].DistanceSq > item.DistanceSq)
                {
                    output[j + 1] = output[j];
                    j--;
                }
                output[j + 1] = item;
            }
            return Math.Min(count, maxCells);
        }

        private static int WalkColumn(
            float startX, float startY, float dirX, float dirY,
            float centerGX, float centerGY, float maxRangeSq, float maxLength,
            int maxDepth, IDrillGrid grid, GridCell[] output, int count, ref int candidateCount)
        {
            int cx = (int)Math.Floor(startX);
            int cy = (int)Math.Floor(startY);
            int stepX = dirX > 0f ? 1 : (dirX < 0f ? -1 : 0);
            int stepY = dirY > 0f ? 1 : (dirY < 0f ? -1 : 0);
            float tDeltaX = stepX == 0 ? float.PositiveInfinity : Math.Abs(1f / dirX);
            float tDeltaY = stepY == 0 ? float.PositiveInfinity : Math.Abs(1f / dirY);
            float tMaxX = stepX == 0 ? float.PositiveInfinity
                : (stepX > 0 ? (cx + 1f - startX) : (startX - cx)) * tDeltaX;
            float tMaxY = stepY == 0 ? float.PositiveInfinity
                : (stepY > 0 ? (cy + 1f - startY) : (startY - cy)) * tDeltaY;
            float travelled = 0f;
            bool  foundSurface = false;
            int depth = 0;

            for (int step = 0; step < MaxSteps; step++)
            {
                if (travelled > maxLength) break;

                float ccx = cx + 0.5f - centerGX;
                float ccy = cy + 0.5f - centerGY;
                float distSq = ccx * ccx + ccy * ccy;
                bool inBounds = grid.InBounds(cx, cy);
                if (!inBounds)
                {
                    // A column that starts outside the map never reaches a surface.
                    if (!foundSurface) break;
                    break;
                }

                if (distSq > maxRangeSq)
                {
                    if (foundSurface) break;
                }
                else
                {
                    int block = grid.GetBlock(cx, cy);
                    if (block != 0)
                    {
                        float health = grid.GetBlockHealth(cx, cy);
                        bool damageable = health > 0f && health < IndestructibleHealth;
                        if (!damageable)
                        {
                            // World boundary / unknown indestructible block: stop this column.
                            break;
                        }
                        foundSurface = true;
                        if (depth < maxDepth)
                        {
                            if (!Contains(output, count, cx, cy))
                            {
                                output[count] = new GridCell { X = cx, Y = cy, DistanceSq = distSq };
                                count++;
                                candidateCount++;
                                if (count >= output.Length) return count;
                            }
                            depth++;
                        }
                        if (depth >= maxDepth) break;
                    }
                    else if (foundSurface)
                    {
                        // The solid surface layer ended; do not drill into the next cave.
                        break;
                    }
                }

                if (tMaxX < tMaxY)
                {
                    travelled = tMaxX;
                    tMaxX += tDeltaX;
                    cx += stepX;
                }
                else
                {
                    travelled = tMaxY;
                    tMaxY += tDeltaY;
                    cy += stepY;
                }
            }
            return count;
        }

        private static bool Contains(GridCell[] cells, int count, int x, int y)
        {
            for (int i = 0; i < count; i++)
            {
                if (cells[i].X == x && cells[i].Y == y) return true;
            }
            return false;
        }
    }
}
