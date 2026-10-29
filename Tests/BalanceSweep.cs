using System;
using System.Collections.Generic;
using System.IO;
using Foxfire;
class BalanceSweep
{
    static void Main()
    {
        var lines = new List<string> { "boss_hp,seeds,victories,defeats,timeouts" };
        foreach (float hp in new[] { 800f, 1000f, 1200f, 1400f })
        {
            int wins = 0, deaths = 0, timeouts = 0;
            for (int seed = 1; seed <= 100; seed++)
            {
                var b = new Balance { bossHP = hp };
                var s = new Simulation(b, seed);
                while (s.Phase != Phase.Results)
                    s.Step(1f / 30);
                if (s.Outcome == Outcome.Victory)
                    wins++;
                else if (s.Outcome == Outcome.Defeat)
                    deaths++;
                else
                    timeouts++;
            }
            string line = $"{hp},100,{wins},{deaths},{timeouts}";
            lines.Add(line);
            Console.WriteLine(line);
        }
        File.WriteAllLines("artifacts/balance-sweep.csv", lines);
    }
}
