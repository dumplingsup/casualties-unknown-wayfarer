using System;
using WayfarerMech.Core;

int count = 0;
foreach (var target in new[] { (0d, 16d), (6d, 16d), (-6d, 16d), (0d, 0d), (0d, 99d), (10d, 2d) })
foreach (int bend in new[] { -1, 1 })
{
    var p = TwoBonePose.Solve(target.Item1, target.Item2, 9, 9, bend);
    Check(Math.Abs(Math.Sqrt(p.KneeX*p.KneeX + p.KneeY*p.KneeY)-9)<1e-6, "upper link length");
    Check(Math.Abs(Math.Sqrt(Math.Pow(p.EndX-p.KneeX,2)+Math.Pow(p.EndY-p.KneeY,2))-9)<1e-6, "lower link length");
    Check(!double.IsNaN(p.Upper) && !double.IsNaN(p.Lower), "finite rotations");
    double a = p.Upper*Math.PI/180, b = (p.Upper+p.Lower)*Math.PI/180;
    Check(Math.Abs(9*Math.Sin(a)+9*Math.Sin(b)-p.EndX)<1e-6 &&
        Math.Abs(9*Math.Cos(a)+9*Math.Cos(b)-p.EndY)<1e-6, "rotations reproduce target");
    if (p.Reachable) Check(Math.Abs(p.EndX-target.Item1)<0.00002 && Math.Abs(p.EndY-target.Item2)<0.00002, "reachable endpoint");
}
Check(!TwoBonePose.Solve(0,1,9,3).Reachable, "inside minimum reach");
Check(!TwoBonePose.Solve(0,20,9,9).Reachable, "outside maximum reach");
for (int i = 0; i < 100; i++)
{
    double phase = i / 100d;
    var gait = WalkCycle.Evaluate(phase);
    Check(gait.Lift >= 0 && gait.Lift <= 8.00001, "swing clearance bounded");
    var pose = TwoBonePose.Solve(gait.Sweep, 16 - 4 + 1.2 * Math.Sin((phase * 2 % 1) * Math.PI) - gait.Lift, 9, 9);
    Check(pose.Reachable, "full stride reachable with chassis compression");
    if (phase < 0.49)
    {
        var next = WalkCycle.Evaluate(phase + 0.01);
        Check(Math.Abs(next.Sweep - gait.Sweep + WalkCycle.CycleDistance * 0.01) < 1e-8,
            "support sweep exactly cancels chassis travel");
    }
    var other = WalkCycle.Evaluate(phase + 0.5);
    Check(gait.Stance != other.Stance, "alternating support phases");
}
foreach (double boundary in new[] { 0d, 0.5d, 1d })
{
    var before = WalkCycle.Evaluate(boundary - 1e-7);
    var after = WalkCycle.Evaluate(boundary + 1e-7);
    Check(Math.Abs(before.Sweep - after.Sweep) < 0.0001 && Math.Abs(before.Lift - after.Lift) < 0.0001,
        "continuous lift-off/touchdown");
}
Console.WriteLine($"PASS {count} pose/gait assertions");
void Check(bool ok, string name) { if (!ok) throw new Exception(name); count++; }
