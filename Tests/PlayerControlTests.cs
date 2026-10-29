using System;
using Foxfire;
class PlayerControlTests {
    static void Main() {
        var b = new Balance { introSeconds = 0, bossAt = 1000, maxSeconds = 1001 };
        var g = new Simulation(b); g.PlayerControlled = true;
        g.Step(.05f); var start = g.Fox;
        g.PlayerDirection = new V(1, 1); g.Step(.05f);
        var dx = g.Fox.x - start.x; var dy = g.Fox.y - start.y;
        if (Math.Abs(dx-dy) > .001f || Math.Abs(Math.Sqrt(dx*dx+dy*dy)-g.Speed*.05f) > .001f) throw new Exception("Diagonal movement is incorrect");
        var stopped = g.Fox; g.PlayerDirection = new V();
        for (int i=0; i<100; i++) g.Step(.05f);
        if (V.Dist(stopped,g.Fox) > .001f) throw new Exception("Fox moved without input");
        g.Reset(); if (!g.PlayerControlled) throw new Exception("Reset lost player control");
        Console.WriteLine("Player controls passed");
    }
}
