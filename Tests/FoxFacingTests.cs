using System;
using Foxfire;

class FoxFacingTests
{
    static void Check(bool value, string reason) { if (!value) throw new Exception(reason); }
    static int Face(FoxFacing facing, double seconds, double degrees, bool moving = true) =>
        facing.Sample(seconds, (float)Math.Cos(degrees * Math.PI / 180),
            (float)Math.Sin(degrees * Math.PI / 180), moving);

    static void Main()
    {
        Check(FoxHeroDirection(0) == 0 && FoxHeroDirection(90) == 8 &&
            FoxHeroDirection(180) == 16 && FoxHeroDirection(270) == 24,
            "Atlas directions match east/north/west/south");
        var facing = new FoxFacing();
        Check(Face(facing, 0, 0) == 0, "Initial movement faces the heading immediately");
        int previous = 0;
        for (int i = 1; i <= 30; i++)
        {
            int next = Face(facing, 1.0 / 60, 180);
            int distance = Math.Abs(next - previous);
            Check(Math.Min(distance, 32 - distance) <= 2, "Reversal passes through intermediate views");
            previous = next;
        }
        Check(previous == 16, "Reversal completes within half a second");
        Check(Face(facing, 1, 0, false) == 16, "Stopped fox retains its facing");
        Check(facing.Sample(1, 0, 0, true) == 16, "Zero heading retains its facing");
        Check(Face(facing, 0, 0) == 16 && Face(facing, -1, 0) == 16,
            "Paused or negative time does not turn the fox");

        foreach (int fps in new[] { 30, 60, 120 })
        {
            facing = new FoxFacing(); Face(facing, 0, 0);
            for (int i = 0; i < fps / 10; i++) previous = Face(facing, 1.0 / fps, 180);
            Check(previous == 29, "Turn speed is consistent at " + fps + " FPS");
        }
        facing = new FoxFacing(); Face(facing, 0, 170);
        Check(Face(facing, 1.0 / 30, -170) == 16, "Crossing 180 degrees takes the short path");
        Check(Face(facing, 1.0 / 30, -170) == 17, "Short turn reaches its destination");
        facing = new FoxFacing(); Face(facing, 0, 350);
        Check(Face(facing, 1.0 / 30, 10) == 0, "East wrap passes through east");
        Check(Face(facing, 1.0 / 30, 10) == 1, "East wrap reaches the next view");

        facing = new FoxFacing(); Face(facing, 0, 0);
        var views = new System.Collections.Generic.HashSet<int>();
        for (int i = 0; i < 15; i++) views.Add(Face(facing, 1.0 / 30, 180));
        Check(views.Count >= 14, "A streamed reversal shows at least 14 intermediate/destination views at 30 FPS");

        facing = new FoxFacing(); Face(facing, 0, 0);
        for (int i = 0; i < 40; i++)
            Check(Face(facing, .1, i % 2 == 0 ? 5.4 : 5.9) == 0,
                "Small fluctuations around a view boundary do not flicker");
        Check(Face(facing, .1, 7) == 1, "Intentional turn crosses the boundary");
        for (int i = 0; i < 40; i++)
            Check(Face(facing, .1, i % 2 == 0 ? 5.4 : 5.9) == 1,
                "Boundary hysteresis also holds the new view");
        Console.WriteLine("PASS fox facing: intermediate views, cardinal mapping, time scaling, wrap, stop and jitter");
    }

    static int FoxHeroDirection(double degrees) => FoxFacing.DirectionIndex(degrees * Math.PI / 180);
}
