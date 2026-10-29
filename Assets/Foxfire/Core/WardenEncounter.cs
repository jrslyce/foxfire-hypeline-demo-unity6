using System;

namespace Foxfire
{
public enum WardenState
{
    Dormant,
    Chase,
    Windup,
    Recover
}

public enum WardenMove
{
    None,
    Slash,
    Slam,
    Spin,
    Summon
}

public static class WardenTiming
{
    public static float Windup(WardenMove move)
    {
        switch (move)
        {
        case WardenMove.Slash: return 1.1f;
        case WardenMove.Slam: return 1.65f; // Reviewed rebake: first downward strike, frame 17/31.
        case WardenMove.Spin:
        case WardenMove.Summon: return 1.5f;
        default: return 0;
        }
    }

    public static float Recovery(WardenMove move)
    {
        switch (move)
        {
        case WardenMove.Slash: return 53f / 24 - Windup(move);
        case WardenMove.Slam: return 3f - Windup(move);
        case WardenMove.Spin:
        case WardenMove.Summon: return 73f / 24 - Windup(move);
        default: return 0;
        }
    }

    public static float Total(WardenMove move)
    {
        return Windup(move) + Recovery(move);
    }
}

// A finite outward-moving ring. Angles are simulation-XY radians.
public sealed class Shockwave
{
    public V pos;
    public float radius, previousRadius, speed, maxRadius, halfWidth, damage;
    public float gapAngle, gapHalfAngle;
    public bool hit;
}

public static class WardenEncounter
{
    public const int DefaultShockwaveCap = 6;

    static float Positive(float value, float fallback)
    {
        return value > 0 && !float.IsNaN(value) ? value : fallback;
    }

    static int Positive(int value, int fallback)
    {
        return value > 0 ? value : fallback;
    }

    public static float ChaseDuration(Balance b, int stage)
    {
        if (stage >= 3)
            return Positive(b.wardenStage3ChaseDuration, 1.8f);
        if (stage >= 2)
            return Positive(b.wardenStage2ChaseDuration, 2.2f);
        return Positive(b.wardenChaseDuration, 2.6f);
    }

    public static float MoveSpeed(Balance b, int stage)
    {
        if (stage >= 3)
            return Positive(b.wardenStage3MoveSpeed, 2.5f);
        if (stage >= 2)
            return Positive(b.wardenStage2MoveSpeed, 2.15f);
        return Positive(b.wardenMoveSpeed, 1.4f);
    }

    public static int Stage(Balance b, Enemy boss)
    {
        float ratio = boss.maxHP > 0 ? boss.hp / boss.maxHP : 0;
        float stage2 = Positive(b.wardenStage2Health, .70f);
        float stage3 = Positive(b.wardenStage3Health, .35f);
        if (ratio <= stage3)
            return 3;
        if (ratio <= stage2)
            return 2;
        return 1;
    }

    public static WardenMove NextMove(Enemy boss)
    {
        // The cycle deliberately exposes all three supplied attack clips. Summon
        // uses the supplied spin clip in presentation but remains its own action.
        switch (boss.attackCount % 4)
        {
        case 0: return WardenMove.Slash;
        case 1: return WardenMove.Slam;
        case 2: return WardenMove.Spin;
        default: return WardenMove.Summon;
        }
    }

    public static void Begin(Simulation game, Enemy boss)
    {
        boss.wardenState = WardenState.Chase;
        boss.wardenMove = WardenMove.None;
        boss.wardenStage = 1;
        boss.wardenStateClock = game.Clock;
        boss.wardenImpactClock = game.Clock;
        boss.dir = (game.Fox - boss.pos).Unit;
        if (boss.dir.Length <= .0001f)
            boss.dir = new V(0, -1);
    }

    public static void Update(Simulation game, Enemy boss, float dt)
    {
        UpdateEncounter(game, boss, dt, true);
    }

    // Mini-bosses borrow the readable Warden attack cadence without becoming
    // the authoritative final encounter. In particular they never summon
    // Warden-owned minions, which keeps phase and victory semantics unchanged.
    public static void UpdateMiniBoss(Simulation game, Enemy boss, float dt)
    {
        UpdateEncounter(game, boss, dt, false);
    }

    static void UpdateEncounter(Simulation game, Enemy boss, float dt, bool allowSummon)
    {
        if (boss.hp <= 0)
            return;

        int currentStage = Stage(game.B, boss);
        if (boss.wardenStage <= 0)
            boss.wardenStage = currentStage;
        else if (currentStage > boss.wardenStage)
            boss.wardenStage = currentStage;
        boss.speed = MoveSpeed(game.B, boss.wardenStage);

        switch (boss.wardenState)
        {
        case WardenState.Dormant:
            return;
        case WardenState.Chase:
            UpdateChase(game, boss, dt, allowSummon);
            return;
        case WardenState.Windup:
            if (game.Clock - boss.wardenStateClock >= WardenTiming.Windup(boss.wardenMove))
            {
                Impact(game, boss);
                boss.wardenState = WardenState.Recover;
                boss.wardenStateClock = game.Clock;
            }
            return;
        case WardenState.Recover:
            if (game.Clock - boss.wardenStateClock >= WardenTiming.Recovery(boss.wardenMove))
            {
                boss.wardenState = WardenState.Chase;
                boss.wardenMove = WardenMove.None;
                boss.wardenStateClock = game.Clock;
            }
            return;
        }
    }

    static void UpdateChase(Simulation game, Enemy boss, float dt, bool allowSummon)
    {
        if (game.Clock - boss.wardenStateClock >= ChaseDuration(game.B, boss.wardenStage))
        {
            V towardFox = game.Fox - boss.pos;
            if (towardFox.Length > .0001f)
                boss.dir = towardFox.Unit;
            boss.wardenMove = NextMove(boss, allowSummon);
            boss.wardenState = WardenState.Windup;
            boss.wardenStateClock = game.Clock;
            return;
        }

        V diff = game.Fox - boss.pos;
        if (diff.Length > .0001f)
        {
            boss.dir = diff.Unit;
            boss.pos = (boss.pos + boss.dir * MoveSpeed(game.B, boss.wardenStage) * dt).Clamp();
        }
    }

    static WardenMove NextMove(Enemy boss, bool allowSummon)
    {
        if (allowSummon)
            return NextMove(boss);
        switch (boss.attackCount % 3)
        {
        case 0: return WardenMove.Slash;
        case 1: return WardenMove.Slam;
        default: return WardenMove.Spin;
        }
    }

    static void Impact(Simulation game, Enemy boss)
    {
        // These are the only writes to the impact counters. A state can cross an
        // impact boundary once, so each scheduled impact is journaled once.
        boss.attackCount++;
        boss.attackClock = game.Clock;
        boss.wardenImpactClock = game.Clock;
        switch (boss.wardenMove)
        {
        case WardenMove.Slash:
            Slash(game, boss);
            break;
        case WardenMove.Slam:
            Slam(game, boss);
            break;
        case WardenMove.Spin:
            Spin(game, boss);
            break;
        case WardenMove.Summon:
            Summon(game, boss);
            break;
        }
    }

    static void Slash(Simulation game, Enemy boss)
    {
        float radius = Positive(game.B.wardenSlashRadius, 2.7f);
        float halfAngle = Positive(game.B.wardenSlashHalfAngle, .85f);
        V toFox = game.Fox - boss.pos;
        float angle = (float)Math.Atan2(toFox.y, toFox.x);
        float facing = (float)Math.Atan2(boss.dir.y, boss.dir.x);
        if (toFox.Length <= radius + game.B.foxRadius &&
            Math.Abs(NormalizeAngle(angle - facing)) <= halfAngle)
            game.Hurt(Positive(game.B.wardenSlashDamage, 18));
        game.Effect(boss.pos, radius, 2);
    }

    static void Slam(Simulation game, Enemy boss)
    {
        float facing = (float)Math.Atan2(boss.dir.y, boss.dir.x);
        float gapAngle = facing + (float)(Math.PI * .5);
        float gapHalf = Positive(game.B.wardenProjectileGapHalfAngle, .42f);
        int count = Positive(game.B.wardenProjectileCount, 14);
        float projectileSpeed = Positive(game.B.wardenProjectileSpeed, 3.4f);
        float damage = Positive(game.B.wardenSlamDamage, 12);
        SpawnGappedWave(game, boss.pos, facing, count, gapAngle, gapHalf,
                        projectileSpeed, damage);
        float ringDamage = Positive(game.B.wardenRingDamage, 14);
        AddShockwave(game, boss.pos, gapAngle, gapHalf, ringDamage, 1);
        if (boss.wardenStage >= 3)
        {
            // The second pulse has a slower expansion, leaving a readable gap
            // between rings instead of stacking a duplicate circle.
            SpawnGappedWave(game, boss.pos, facing + .12f, count,
                            gapAngle, gapHalf, projectileSpeed * .68f,
                            damage * .8f);
            AddShockwave(game, boss.pos, gapAngle, gapHalf,
                         ringDamage, .68f);
        }
        game.Effect(boss.pos, 1.5f, 1, .8f);
    }

    static void Spin(Simulation game, Enemy boss)
    {
        float facing = (float)Math.Atan2(boss.dir.y, boss.dir.x);
        float gapAngle = facing - (float)(Math.PI * .5);
        SpawnGappedWave(game, boss.pos, facing + .18f,
                        Positive(game.B.wardenProjectileCount, 14), gapAngle,
                        Positive(game.B.wardenProjectileGapHalfAngle, .42f),
                        Positive(game.B.wardenProjectileSpeed, 3.4f),
                        Positive(game.B.wardenSpinDamage, 10));
        if (V.Dist(game.Fox, boss.pos) <= 1.9f + game.B.foxRadius)
            game.Hurt(Positive(game.B.wardenSpinDamage, 10));
        game.Effect(boss.pos, 1.7f, 1, .8f);
    }

    static void Summon(Simulation game, Enemy boss)
    {
        int cap = Positive(game.B.wardenSummonCap, 16);
        int requested = Positive(game.B.wardenSummonCount, 8);
        int available = Math.Max(0, cap - game.WardenMinionCount - game.PendingWardenSummons);
        requested = Math.Min(requested, available);
        for (int i = 0; i < requested; i++)
        {
            V p;
            if (game.TryWardenSummonPosition(boss, i, requested, out p))
                game.QueueWardenSummon(i % 2 == 0 ? EnemyKind.Boar : EnemyKind.Sprig, p);
        }
        game.Notice(requested > 0 ? "The Warden calls corrupted guardians" :
                                   "The Warden's summoning circle fades");
        game.Effect(boss.pos, 1.9f, 1, 1);
    }

    static void SpawnGappedWave(Simulation game, V origin, float baseAngle,
                                int count, float gapAngle, float gapHalf,
                                float speed, float damage)
    {
        for (int i = 0; i < count; i++)
        {
            float angle = baseAngle + (float)(Math.PI * 2 * i / count);
            if (Math.Abs(NormalizeAngle(angle - gapAngle)) <= gapHalf)
                continue;
            game.SpawnHostile(origin, V.Heading(angle) * speed, damage);
        }
    }

    static void AddShockwave(Simulation game, V origin, float gapAngle,
                             float gapHalf, float damage, float speedScale)
    {
        int cap = Positive(game.B.wardenShockwaveCap, DefaultShockwaveCap);
        if (game.Shockwaves.Count >= cap)
            return;
        game.Shockwaves.Add(new Shockwave {
            pos = origin,
            radius = 0,
            previousRadius = 0,
            speed = Positive(game.B.wardenRingSpeed, 4.8f) * speedScale,
            maxRadius = Positive(game.B.wardenRingMaxRadius, 30),
            halfWidth = Positive(game.B.wardenRingHalfWidth, .42f),
            damage = damage,
            gapAngle = gapAngle,
            gapHalfAngle = gapHalf
        });
    }

    public static bool InGap(Shockwave wave, V point)
    {
        V offset = point - wave.pos;
        if (offset.Length <= .0001f)
            return false;
        float angle = (float)Math.Atan2(offset.y, offset.x);
        return Math.Abs(NormalizeAngle(angle - wave.gapAngle)) <= wave.gapHalfAngle;
    }

    public static float NormalizeAngle(float angle)
    {
        while (angle > Math.PI) angle -= (float)(Math.PI * 2);
        while (angle < -Math.PI) angle += (float)(Math.PI * 2);
        return angle;
    }
}
}
