using System;

namespace WayfarerMech.Core
{
    // Pure 2D inverse kinematics. Coordinates: x right, y down; angles are
    // rotations from the downward rest axis, matching the pixel rig.
    public static class TwoBonePose
    {
        public struct Pose
        {
            public double Upper, Lower, KneeX, KneeY, EndX, EndY;
            public bool Reachable;
        }

        public static Pose Solve(double x, double y, double upper, double lower, int bend = -1)
        {
            if (upper <= 0 || lower <= 0) throw new ArgumentOutOfRangeException(nameof(upper));
            double distance = Math.Sqrt(x * x + y * y);
            double min = Math.Abs(upper - lower) + 0.00001;
            double max = upper + lower - 0.00001;
            double clamped = Math.Max(min, Math.Min(max, distance));
            double ux = distance > 0.000001 ? x / distance : 0;
            double uy = distance > 0.000001 ? y / distance : 1;
            double along = (upper * upper - lower * lower + clamped * clamped) / (2 * clamped);
            double side = Math.Sqrt(Math.Max(0, upper * upper - along * along)) * (bend < 0 ? -1 : 1);
            double kx = ux * along + uy * side, ky = uy * along - ux * side;
            double ex = ux * clamped, ey = uy * clamped;
            double first = Math.Atan2(kx, ky) * 180 / Math.PI;
            double second = Math.Atan2(ex - kx, ey - ky) * 180 / Math.PI;
            return new Pose { Upper = first, Lower = second - first,
                KneeX = kx, KneeY = ky, EndX = ex, EndY = ey,
                Reachable = distance >= Math.Abs(upper - lower) && distance <= upper + lower };
        }
    }
}
