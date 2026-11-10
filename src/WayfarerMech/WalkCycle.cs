using System;

namespace WayfarerMech.Core
{
    /// <summary>Distance-driven gait in native pixels. One cycle advances 48 px.</summary>
    public static class WalkCycle
    {
        public const double HalfStride = 12;
        public const double CycleDistance = HalfStride * 4;
        public struct Sample { public double Sweep, Lift; public bool Stance; }
        public static Sample Evaluate(double phase)
        {
            phase -= Math.Floor(phase);
            if (phase < 0.5)
                return new Sample { Sweep = HalfStride * (1 - 4 * phase), Stance = true };
            double t = (phase - 0.5) * 2;
            double travel = t * t * (3 - 2 * t);
            return new Sample { Sweep = HalfStride * (2 * travel - 1),
                Lift = 8 * Math.Pow(Math.Sin(Math.PI * t), 2), Stance = false };
        }
    }
}
