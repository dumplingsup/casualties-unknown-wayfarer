using System;
using System.Collections.Generic;
using WayfarerMech.Core;

internal sealed class FakeGrid : IDrillGrid
{
    private readonly int minX, maxX, minY, maxY;
    private readonly Func<int, int, bool> solid;
    private readonly Func<int, int, float> health;
    public FakeGrid(int minX, int maxX, int minY, int maxY, Func<int, int, bool> solid, Func<int, int, float> health = null)
    {
        this.minX = minX; this.maxX = maxX; this.minY = minY; this.maxY = maxY;
        this.solid = solid; this.health = health ?? ((x, y) => 100f);
    }
    public bool InBounds(int x, int y) => x >= minX && x <= maxX && y >= minY && y <= maxY;
    public int GetBlock(int x, int y) => InBounds(x, y) && solid(x, y) ? 1 : 0;
    public float GetBlockHealth(int x, int y) => health(x, y);
}

internal static class Program
{
    private static int failures;
    private static GridCell[] buffer = new GridCell[256];

    private static void Check(string name, bool condition, string detail = "")
    {
        if (condition) { Console.WriteLine("PASS " + name + (detail.Length > 0 ? "  " + detail : "")); return; }
        failures++;
        Console.WriteLine("FAIL " + name + (detail.Length > 0 ? "  " + detail : ""));
    }

    private static bool NoDuplicates(GridCell[] cells, int count)
    {
        var set = new HashSet<long>();
        for (int i = 0; i < count; i++)
            if (!set.Add(((long)cells[i].X << 32) ^ (uint)cells[i].Y)) return false;
        return true;
    }

    private static (int minY, int maxY, int minX, int maxX) Bounds(GridCell[] cells, int count)
    {
        int minY = int.MaxValue, maxY = int.MinValue, minX = int.MaxValue, maxX = int.MinValue;
        for (int i = 0; i < count; i++)
        {
            minY = Math.Min(minY, cells[i].Y); maxY = Math.Max(maxY, cells[i].Y);
            minX = Math.Min(minX, cells[i].X); maxX = Math.Max(maxX, cells[i].X);
        }
        return (minY, maxY, minX, maxX);
    }

    private static int Plan(float cx, float cy, float dirX, float dirY, float hullW, float hullH,
        float range, int depth, int maxCells, IDrillGrid grid, out int candidates)
    {
        return DrillShape.Plan(cx, cy, 0f, 0f, hullW, hullH, dirX, dirY, range, depth, maxCells, grid, buffer, out candidates);
    }

    private static int Main()
    {
        // 1. Horizontal corridor: hull projection is 7.6 units tall -> 8-9 ray columns; depth 2.
        var solidRight = new FakeGrid(-50, 50, -50, 50, (x, y) => x >= 6);
        int n = Plan(0, 0, 1, 0, 4.2f, 6.6f, 10f, 2, 24, solidRight, out int candidates);
        var b = Bounds(buffer, n);
        Check("horizontal count", n == 16, $"n={n} candidates={candidates}");
        Check("horizontal surface", b.minX == 6 && b.maxX == 7, $"x={b.minX}..{b.maxX}");
        Check("horizontal cover", b.minY <= -3 && b.maxY >= 3 && b.minY >= -4 && b.maxY <= 4, $"y={b.minY}..{b.maxY}");
        Check("horizontal dedupe", NoDuplicates(buffer, n));

        // 2. Upward corridor: hull projection is wide; 2 layers, no cells wider than the hull.
        var solidUp = new FakeGrid(-50, 50, -50, 50, (x, y) => y >= 6);
        n = Plan(0, 0, 0, 1, 4.2f, 6.6f, 10f, 2, 24, solidUp, out candidates);
        b = Bounds(buffer, n);
        Check("vertical count", n >= 10 && n <= 14, $"n={n}");
        Check("vertical surface", b.minY == 6 && b.maxY == 7, $"y={b.minY}..{b.maxY}");
        Check("vertical cover", b.minX >= -3 && b.maxX <= 3, $"x={b.minX}..{b.maxX}");

        // 3. Diagonal: never returns cells behind the first visible surface.
        var solidDiag = new FakeGrid(-50, 50, -50, 50, (x, y) => x + y >= 10);
        n = Plan(0, 0, 0.70710678f, 0.70710678f, 4.2f, 6.6f, 10f, 2, 24, solidDiag, out candidates);
        bool behind = false; bool tooFar = false;
        for (int i = 0; i < n; i++)
        {
            if (buffer[i].X + buffer[i].Y < 10) behind = true;
            float dx = buffer[i].X + 0.5f, dy = buffer[i].Y + 0.5f;
            if (dx * dx + dy * dy > 100f + 4f) tooFar = true;
        }
        Check("diagonal forward only", n > 0 && !behind, $"n={n}");
        Check("diagonal range", !tooFar);
        Check("diagonal dedupe", NoDuplicates(buffer, n));
        Check("diagonal budget", n <= 24, $"n={n}");

        // 4. Indestructible boundary blocks a column instead of leaking through it.
        var indestructible = new FakeGrid(-50, 50, -50, 50, (x, y) => x >= 6,
            (x, y) => x == 6 ? DrillShape.IndestructibleHealth : 100f);
        n = Plan(0, 0, 1, 0, 4.2f, 6.6f, 10f, 2, 24, indestructible, out candidates);
        Check("indestructible blocked", n == 0, $"n={n}");

        // 5. Per-pulse budget keeps the nearest cells and is sorted ascending.
        var thick = new FakeGrid(-50, 50, -50, 50, (x, y) => x >= 6);
        n = Plan(0, 0, 1, 0, 4.2f, 6.6f, 10f, 4, 5, thick, out candidates);
        bool sorted = true;
        for (int i = 1; i < n; i++) if (buffer[i - 1].DistanceSq > buffer[i].DistanceSq) sorted = false;
        Check("budget cap", n == 5 && candidates >= 18, $"n={n} candidates={candidates}");
        Check("nearest-first sort", sorted);

        // 6. Air gap before the surface is crossed; the surface is still found.
        var gap = new FakeGrid(-50, 50, -50, 50, (x, y) => x >= 9);
        n = Plan(0, 0, 1, 0, 4.2f, 6.6f, 15f, 2, 24, gap, out candidates);
        b = Bounds(buffer, n);
        Check("air gap surface", n > 0 && b.minX == 9, $"n={n} x={b.minX}..{b.maxX}");

        // 7. Range: a surface beyond maxCenterRange is not touched.
        var far = new FakeGrid(-50, 50, -50, 50, (x, y) => x >= 12);
        n = Plan(0, 0, 1, 0, 4.2f, 6.6f, 10f, 2, 24, far, out candidates);
        Check("range respected", n == 0, $"n={n}");

        // 8. Empty/out-of-bounds inputs are safe.
        var empty = new FakeGrid(-50, 50, -50, 50, (x, y) => false);
        n = Plan(0, 0, 1, 0, 4.2f, 6.6f, 10f, 2, 24, empty, out candidates);
        Check("empty grid", n == 0);
        n = Plan(1000, 1000, 1, 0, 4.2f, 6.6f, 10f, 2, 24, empty, out candidates);
        Check("out of bounds", n == 0);

        // 9. The hull projection formula matters: a two-unit-tall hull cannot cover 8 rows.
        n = Plan(0, 0, 1, 0, 4.2f, 2f, 10f, 2, 24, solidRight, out candidates);
        b = Bounds(buffer, n);
        Check("small hull projection", n <= 12 && b.minY >= -2 && b.maxY <= 1, $"n={n} y={b.minY}..{b.maxY}");

        // 10. No surface behind the drill start point even if the mech is inside solid rock.
        var everywhere = new FakeGrid(-50, 50, -50, 50, (x, y) => true);
        n = Plan(0, 0, -1, 0, 4.2f, 6.6f, 10f, 2, 24, everywhere, out candidates);
        b = Bounds(buffer, n);
        Check("start outside hull", n > 0 && b.minX <= -3 && b.maxX <= -2, $"n={n} x={b.minX}..{b.maxX}");

        Console.WriteLine(failures == 0 ? "ALL TESTS PASSED" : failures + " TEST(S) FAILED");
        return failures == 0 ? 0 : 1;
    }
}
