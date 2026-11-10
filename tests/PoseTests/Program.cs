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
Console.WriteLine($"PASS {count} pose assertions");
void Check(bool ok, string name) { if (!ok) throw new Exception(name); count++; }
