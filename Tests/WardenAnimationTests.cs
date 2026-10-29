using System;
using System.Collections.Generic;
using Foxfire;

class WardenAnimationTests
{
    const float Dt = 1f / 30;
    static int checks;

    static void Check(bool value, string message)
    {
        checks++;
        if (!value)
            throw new Exception("FAIL: " + message);
    }

    static WardenClip ClipFor(WardenMove move)
    {
        switch (move)
        {
        case WardenMove.Slam: return WardenClip.Slam;
        case WardenMove.Slash: return WardenClip.Slash;
        case WardenMove.Spin:
        case WardenMove.Summon: return WardenClip.Spin;
        default: return WardenClip.Idle;
        }
    }

    static Simulation Start()
    {
        var game = new Simulation(new Balance { introSeconds = 0, bossAt = .01f, maxSeconds = 240,
            foxHP = 100000, boltDamage = 0, foxSpeed = 0 }, 13);
        int guard = 0;
        while (game.Boss == null && guard++ < 20)
            game.Step(Dt);
        Check(game.Boss != null, "boss appears for animation test");
        return game;
    }

    public static void Main()
    {
        try
        {
            Check((int)(WardenAnimation.Impact(WardenClip.Slam) / WardenAnimation.Duration(WardenClip.Slam) * 31) == 17,
                  "slam impact lands on the reviewed downward-strike pose, not the raised-arm recovery");
            Check(Math.Abs(WardenTiming.Windup(WardenMove.Slam) - WardenAnimation.Impact(WardenClip.Slam)) < .001f,
                  "slam damage and presentation use the same reviewed impact clock");
            var game = Start();
            Check(WardenFacing.DirectionIndex(new V(1, 0)) == 0 &&
                  WardenFacing.DirectionIndex(new V(0, 1)) == 2 &&
                  WardenFacing.DirectionIndex(new V(-1, 0)) == 4 &&
                  WardenFacing.DirectionIndex(new V(0, -1)) == 6,
                  "directional locomotion selector covers all cardinal target headings");
            var visual = new WardenAnimation();
            V originalHeading = game.Boss.dir;
            for (int d = 0; d < WardenFacing.DirectionCount; d++)
            {
                game.Boss.dir = V.Heading(d * (float)Math.PI / 4);
                visual.Update(game);
                Check(V.Dist(visual.FacingDirection, game.Boss.dir) < .0001f &&
                      WardenFacing.DirectionIndex(visual.FacingDirection) == d,
                      "presentation tracks each of eight chase headings");
            }
            game.Boss.dir = originalHeading;
            visual.Update(game);
            Check(visual.Visible, "spawned boss is visible");
            Check(visual.FacingDirection.Length > .99f, "warden presentation exposes a normalized target heading");
            Check(visual.Clip == WardenClip.Idle || visual.Clip == WardenClip.Walking ||
                  visual.Clip == WardenClip.Running, "chase uses an existing locomotion pose");

            // Force a measured fast chase step so the renderer's Running path is covered even
            // when a particular balance asset gives the boss a slower chase speed.
            var runningBoss = game.Boss;
            runningBoss.wardenState = WardenState.Chase;
            runningBoss.wardenMove = WardenMove.None;
            game.Step(Dt);
            runningBoss = game.Boss;
            runningBoss.wardenState = WardenState.Chase;
            runningBoss.wardenMove = WardenMove.None;
            runningBoss.pos = (visual.Position + new V(2, 0)).Clamp();
            visual.Update(game);
            Check(visual.Clip == WardenClip.Running, "fast chase selects Running");
            for (int i = 0; i < 30; i++)
            {
                game.Step(Dt);
                runningBoss = game.Boss;
                runningBoss.wardenState = WardenState.Chase;
                runningBoss.wardenMove = WardenMove.None;
                runningBoss.pos = (runningBoss.pos + new V(.1f, 0)).Clamp();
                visual.Update(game);
            }
            Check(visual.Clip == WardenClip.Running &&
                  visual.Seconds > WardenAnimation.Duration(WardenClip.Running) + .05f,
                  "locomotion keeps cycling beyond one clip duration");
            WardenClip heldClip = visual.Clip;
            float heldSeconds = visual.Seconds;
            visual.Update(game);
            visual.Update(game);
            Check(heldClip == visual.Clip && heldSeconds == visual.Seconds,
                  "prolonged locomotion pose holds across repeated same-clock updates");

            var seen = new HashSet<WardenMove>();
            int attacks = 0;
            int guardSteps = 0;
            while (game.Time < 90 && guardSteps++ < 3000)
            {
                game.Step(Dt);
                visual.Update(game);
                var boss = game.Boss;
                if (boss == null)
                    break;
                if (boss.wardenState == WardenState.Windup)
                {
                    Check(visual.Clip == ClipFor(boss.wardenMove),
                          "windup state selects its explicit supplied clip");
                    Check(visual.Seconds >= 0 && visual.Seconds <= WardenAnimation.Duration(visual.Clip) + .05f,
                          "windup clip stays within its supplied duration");
                }
                if (boss.attackCount != attacks)
                {
                    Check(boss.attackCount == attacks + 1, "animation sees one impact at a time");
                    WardenClip expected = ClipFor(boss.wardenMove);
                    Check(visual.Clip == expected, "impact uses move instead of attack parity");
                    Check(Math.Abs(visual.Seconds - WardenAnimation.Impact(expected)) < .05f,
                          "animation release aligns with explicit combat impact");
                    seen.Add(boss.wardenMove);
                    attacks = boss.attackCount;
                }
                WardenClip oldClip = visual.Clip;
                float oldSeconds = visual.Seconds;
                V oldPosition = visual.Position;
                visual.Update(game);
                Check(oldClip == visual.Clip && oldSeconds == visual.Seconds &&
                      oldPosition.x == visual.Position.x && oldPosition.y == visual.Position.y,
                      "pose holds when simulation clock does not advance");
            }
            Check(seen.Contains(WardenMove.Slash) && seen.Contains(WardenMove.Slam) &&
                  seen.Contains(WardenMove.Spin), "all three supplied attack clips are exercised");

            var bossForHurt = game.Boss;
            Check(bossForHurt != null, "boss remains available for hurt check");
            bossForHurt.hp -= 1;
            visual.Update(game);
            Check(visual.Hurt, "damage flashes without interrupting current animation");

            bossForHurt.hp = 0;
            game.Step(Dt);
            visual.Update(game);
            Check(game.Outcome == Outcome.Victory && visual.Clip == WardenClip.Dead && visual.Visible,
                  "removed boss plays death while victory is immediate");
            for (int i = 0; i < 120 && visual.Visible; i++)
            {
                game.Step(Dt);
                visual.Update(game);
            }
            Check(!visual.Visible, "death pose completes and fades");

            game.Reset();
            visual.Update(game);
            Check(!visual.Visible && visual.Clip == WardenClip.Idle,
                  "reset clears animation state");
            Console.WriteLine("PASS Warden animation: " + checks + " assertions");
        }
        catch (Exception e)
        {
            Console.Error.WriteLine(e);
            Environment.Exit(1);
        }
    }
}
