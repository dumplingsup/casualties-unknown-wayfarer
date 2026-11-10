using UnityEngine;

namespace WayfarerMech
{
    /// <summary>
    /// Terrain-aware placement for leaving the mech. All checks are re-validated at use time
    /// (cached positions are never trusted after the map has been dug out). Scene changes
    /// destroy the controller, so cached points do not cross layers.
    /// </summary>
    public static class MechSafeExit
    {
        private static readonly int GroundMask = LayerMask.GetMask("Ground");

        public static Vector2 ManualExit(Vector2 center, Vector2 bodySize, int facing)
        {
            foreach (float offset in new[] { 3.4f, 2.4f, 1.2f })
            foreach (int side in new[] { facing, -facing })
            {
                Vector2 candidate = center + Vector2.right * (offset * side);
                if (!Physics2D.OverlapBox(candidate, bodySize * 0.88f, 0f, GroundMask)) return candidate;
            }
            return center;
        }

        public static bool IsStandingSpot(Vector2 pos, Vector2 bodySize)
        {
            // Clearance for the body, with a small margin so we don't rely on exact contact.
            if (Physics2D.OverlapBox(pos, bodySize * 0.88f, 0f, GroundMask)) return false;
            // Head/body clearance; a low ceiling blocks the player even if the feet are clear.
            if (Physics2D.OverlapBox(pos + Vector2.up * 0.25f, bodySize * 0.82f, 0f, GroundMask)) return false;
            Vector2 foot = pos + Vector2.down * (bodySize.y * 0.5f - 0.15f);
            var hit = Physics2D.Raycast(foot, Vector2.down, 1.05f, GroundMask);
            return hit && hit.normal.y > 0.4f;
        }

        /// <summary>Find grounded placement for automatic save/lifecycle recovery.</summary>
        public static bool FindNearby(Vector2 center, Vector2 bodySize, out Vector2 result)
        {
            float[] horizontal = { 3.4f, 4.4f, 5.4f, 6.4f, 7.4f };
            float[] vertical = { 0f, 1.5f, -1.5f, 3f, -3f };
            for (int i = 0; i < horizontal.Length; i++)
            {
                for (int side = -1; side <= 1; side += 2)
                {
                    for (int j = 0; j < vertical.Length; j++)
                    {
                        var candidate = center + new Vector2(horizontal[i] * side, vertical[j]);
                        if (IsStandingSpot(candidate, bodySize)) { result = candidate; return true; }
                    }
                }
            }
            result = center;
            return false;
        }

        /// <summary>
        /// Forced lifecycle placement: nearby side spot, then a terrain scan below/around the
        /// current column, then the current position if it is already a valid standing spot.
        /// Never returns a spot that is inside a solid block.
        /// </summary>
        public static bool FindForced(Vector2 origin, Vector2 bodySize, out Vector2 result, out string method)
        {
            if (FindNearby(origin, bodySize, out result)) { method = "nearby"; return true; }
            if (FindGridLanding(origin, bodySize, out result)) { method = "terrain-scan"; return true; }
            if (IsStandingSpot(origin, bodySize)) { result = origin; method = "current"; return true; }
            result = origin;
            method = "none";
            return false;
        }

        /// <summary>Scan the world grid around the current column for a solid surface with air above.</summary>
        private static bool FindGridLanding(Vector2 origin, Vector2 bodySize, out Vector2 result)
        {
            result = origin;
            var w = WorldGeneration.world;
            if (!w || !w.worldExists || w.width < 4 || w.height < 8) return false;
            Vector2Int originCell = w.WorldToBlockPos(origin);
            int top = Mathf.Clamp(originCell.y + 10, 3, (int)w.height - 3);
            int bottom = 2;
            int maxRadius = 40;
            for (int radius = 0; radius <= maxRadius; radius++)
            {
                for (int side = -1; side <= 1; side += 2)
                {
                    int dx = radius * side;
                    if (radius == 0 && side == 1) break;
                    if (!TryColumn(w, originCell.x + dx, top, bottom, bodySize, out result)) continue;
                    return true;
                }
            }
            // Last resort: a known-clear spot near the top of the layer.
            float y = (float)w.halfHeight - bodySize.y * 0.5f - 1f;
            for (float x = 0f; Mathf.Abs(x) < (float)w.halfWidth - 2f; x += 4f)
            {
                var candidate = new Vector2(x, y);
                if (IsStandingSpot(candidate, bodySize)) { result = candidate; return true; }
                if (IsStandingSpot(new Vector2(-x, y), bodySize)) { result = new Vector2(-x, y); return true; }
            }
            return false;
        }

        private static bool TryColumn(WorldGeneration w, int x, int top, int bottom, Vector2 bodySize, out Vector2 result)
        {
            result = Vector2.zero;
            if (x < 1 || x >= (int)w.width - 1) return false;
            for (int y = top; y >= bottom; y--)
            {
                var cell = new Vector2Int(x, y);
                if (w.GetBlock(cell) == 0) continue;
                var info = w.GetBlockInfo((ushort)w.GetBlock(cell));
                if (info == null || info.health <= 0f) continue;
                // Five cells of air above the surface is enough for the player body.
                bool air = true;
                for (int k = 1; k <= 6 && air; k++)
                {
                    if (y + k >= (int)w.height - 1) { air = false; break; }
                    if (w.GetBlock(new Vector2Int(x, y + k)) != 0) air = false;
                }
                if (!air) continue;
                if (IsLiquid(cell)) continue;
                Vector2 groundCenter = w.BlockToWorldPos(cell);
                var candidate = new Vector2(groundCenter.x, groundCenter.y + 0.5f + bodySize.y * 0.5f + 0.15f);
                if (IsStandingSpot(candidate, bodySize)) { result = candidate; return true; }
            }
            return false;
        }

        private static bool IsLiquid(Vector2Int cell)
        {
            if (!FluidManager.main) return false;
            try { return FluidManager.main.WaterInfo(cell).type > 0; }
            catch { return false; }
        }
    }
}
