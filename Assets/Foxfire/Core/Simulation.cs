using System;
using System.Collections.Generic;
namespace Foxfire
{
public enum Phase
{
    Intro,
    Combat,
    Boss,
    Results
}
public enum Outcome
{
    None,
    Victory,
    Defeat,
    Timeout
}
public enum EnemyKind
{
    Sprig,
    Boar,
    Wisp,
    Warden,
    MiniBoss
}
public enum Upgrade
{
    EmberPower,
    Quickcast,
    FleetPaws,
    Vitality,
    PiercingEmber,
    OrbitingSpark
}
public class Enemy
{
    // Presentation events; these never affect combat timing or random-number consumption.
    public int attackCount;
    public double attackClock;
    public bool champion;
    // Mini-bosses are a separate enemy kind so they never satisfy the final
    // Warden victory condition, while this marker gives presentation a stable
    // boss-style identity.
    public bool miniBoss;
    public string displayName;
    public int id;
    public EnemyKind kind;
    public V pos, dir;
    public WardenState wardenState;
    public WardenMove wardenMove;
    public int wardenStage;
    public double wardenStateClock, wardenImpactClock;
    public bool wardenSummoned;
    public float hp, maxHP, speed, radius, tell, charge, cooldown, orbitTimer, spawnTell, dissolve;
    public float shotTell, shotCooldown;
}
public class Shot
{
    public V pos, velocity;
    public float damage, life;
    public bool hostile;
    public int pierce;
    public HashSet<int> hit = new HashSet<int>();
}
public class Pickup
{
    public V pos;
    public int xp;
    public float life;
    public bool heal;
}
public class Hazard
{
    public V pos;
    public float radius, tell, life, damage, tick;
    public bool star, viewer;
    public string viewerName;
}
public class Burst
{
    public V pos;
    public float life, maxLife, radius;
    public int kind;
    public string viewerName;
}
public class RunResult
{
    public int seed, kills, upgrades, actions, run;
    public float seconds, hp;
    public Outcome outcome;
}
public sealed class ViewerAnnouncement
{
    public string viewerName, actionName;
    public double createdAt;
    public bool IsJoin { get { return actionName == null; } }
}
public sealed class Simulation
{
    public readonly Balance B;
    public readonly List<Enemy> Enemies = new List<Enemy>();
    public readonly List<Shot> Shots = new List<Shot>();
    public readonly List<Pickup> Pickups = new List<Pickup>();
    public readonly List<Hazard> Hazards = new List<Hazard>();
    public readonly List<Burst> Bursts = new List<Burst>();
    public readonly List<Shockwave> Shockwaves = new List<Shockwave>();
    public readonly int[] Upgrades = new int[6];
    public readonly List<RunResult> History = new List<RunResult>();
    public RunInteractions RunInteractions { get; private set; }
    public const int ThankYouNamesPerPage = 12;
    public int ThankYouPages { get { return Math.Max(1, (RunInteractions.ViewerCount + ThankYouNamesPerPage - 1) / ThankYouNamesPerPage); } }
    public float ResultsDuration { get { return Math.Max(B.resultsSeconds, ThankYouPages > 1 ? ThankYouPages * 5 : 0); } }
    public readonly List<ViewerAnnouncement> ViewerAnnouncements = new List<ViewerAnnouncement>();
    public readonly List<string> Notices = new List<string>();
    public Phase Phase { get; private set; }
    public Outcome Outcome { get; private set; }
    public string RunId { get; private set; }
    public int RunNumber { get; private set; }
    public int Seed { get; private set; }
    public float Time { get; private set; }
    public double Clock { get; private set; }
    public float StateTime { get; private set; }
    public V Fox, Direction;
    public bool PlayerControlled;
    public V PlayerDirection;
    public float HP, MaxHP, Laser, Rush, Immune, Celebration;
    // Viewer effects are public presentation state. Timers are authoritative
    // simulation clocks and never refresh while the corresponding effect is active.
    public float EnemyGlow, ReverseFacing, EnemySpin, PetalHeart, MuddyFeet, RainbowShots;
    public V PetalHeartPosition;
    public float ForceWavePulse;
    public int ForceWavePulsesRemaining;
    public int XP, Level, Kills, ExecutedActions, UnstuckEvents;
    public string Intent = "Patrolling shrine";
    public float NoticeTime;
    public Enemy Boss
    {
        get {
            return Enemies.Find(e => e.kind == EnemyKind.Warden);
        }
    }
    public float BossAttackIn
    {
        get {
            var boss = Boss;
            if (boss == null)
                return Math.Max(0, bossTimer);
            double elapsed = Clock - boss.wardenStateClock;
            switch (boss.wardenState)
            {
            case WardenState.Chase:
                return Math.Max(0, WardenEncounter.ChaseDuration(B, boss.wardenStage) - (float)elapsed);
            case WardenState.Windup:
                return Math.Max(0, WardenTiming.Windup(boss.wardenMove) - (float)elapsed);
            case WardenState.Recover:
                return Math.Max(0, WardenTiming.Recovery(boss.wardenMove) - (float)elapsed) +
                       WardenEncounter.ChaseDuration(B, boss.wardenStage);
            default:
                return 0;
            }
        }
    }
    public float Damage
    {
        get {
            return B.boltDamage * (1 + .2f * Upgrades[0]);
        }
    }
    public float Speed
    {
        get {
            return B.foxSpeed * (1 + .08f * Upgrades[2]) * (MuddyFeet > 0 ? .5f : 1);
        }
    }
    public float Interval
    {
        get {
            return B.attackInterval * (1 - .12f * Upgrades[1]);
        }
    }
    public bool Active
    {
        get {
            return Phase == Phase.Combat || Phase == Phase.Boss;
        }
    }
    public event Action Resetting;
    public event Action<RunResult> Finished;
    SeededRandom rng;
    int nextId, bossPattern, patrol;
    float spawnTimer, fireTimer, thinkTimer, bossTimer, stallTime, forceWaveTimer;
    V stallOrigin;
    sealed class PendingWardenSummon
    {
        public EnemyKind kind;
        public V pos;
    }
    readonly List<PendingWardenSummon> pendingWardenSummons = new List<PendingWardenSummon>();
    static readonly V[] PatrolPoints = { new V(-5, -3), new V(5, -3), new V(5, 3), new V(-5, 3) };
    readonly string boot = Guid.NewGuid().ToString("N").Substring(0, 10);
    public Simulation(Balance balance, int seed = 1)
    {
        B = balance;
        Seed = seed - 1;
        Reset();
    }
    public void Reset()
    {
        if (Resetting != null)
            Resetting();
        RunNumber++;
        RunInteractions = new RunInteractions();
        Seed++;
        RunId = boot + "-" + RunNumber;
        rng = new SeededRandom(Seed);
        Phase = Phase.Intro;
        Outcome = Outcome.None;
        Time = StateTime = 0;
        Fox = new V(0, -1);
        Direction = new V();
        HP = MaxHP = B.foxHP;
        XP = Level = Kills = ExecutedActions = UnstuckEvents = 0;
        Laser = Rush = Immune = Celebration = 0;
        EnemyGlow = ReverseFacing = EnemySpin = PetalHeart = MuddyFeet = RainbowShots = 0;
        PetalHeartPosition = new V();
        ForceWavePulse = 0;
        ForceWavePulsesRemaining = 0;
        Array.Clear(Upgrades, 0, 6);
        Enemies.Clear();
        Shots.Clear();
        Pickups.Clear();
        Hazards.Clear();
        Bursts.Clear();
        Shockwaves.Clear();
        pendingWardenSummons.Clear();
        Notices.Clear();
        NoticeTime = 0;
        spawnTimer = fireTimer = thinkTimer = stallTime = forceWaveTimer = 0;
        bossTimer = 4;
        bossPattern = nextId = patrol = 0;
        stallOrigin = Fox;
        Intent = "Entering the clearing";
    }
    public void Step(float dt)
    {
        if (dt <= 0 || dt > .1f || float.IsNaN(dt))
            throw new ArgumentOutOfRangeException("dt");
        Clock += dt;
        StateTime += dt;
        Celebration = Math.Max(0, Celebration - dt);
        for (int i = ViewerAnnouncements.Count - 1; i >= 0; i--)
            if (Clock - ViewerAnnouncements[i].createdAt >= 8) ViewerAnnouncements.RemoveAt(i);
        for (int i = Bursts.Count - 1; i >= 0; i--)
        {
            Bursts[i].life -= dt;
            if (Bursts[i].life <= 0)
                Bursts.RemoveAt(i);
        }
        NoticeTime -= dt;
        if (NoticeTime <= 0)
            Notices.Clear();
        if (Phase == Phase.Intro)
        {
            if (StateTime >= B.introSeconds)
            {
                Phase = Phase.Combat;
                StateTime = 0;
                Intent = "Guarding the clearing";
            }
            return;
        }
        if (Phase == Phase.Results)
        {
            if (StateTime >= ResultsDuration)
                Reset();
            return;
        }
        Time += dt;
        Immune = Math.Max(0, Immune - dt);
        Laser = Math.Max(0, Laser - dt);
        Rush = Math.Max(0, Rush - dt);
        UpdateViewerEffects(dt);
        if (Phase == Phase.Combat && Time >= B.bossAt)
        {
            Phase = Phase.Boss;
            StateTime = 0;
            Shots.Clear();
            var warden = AddEnemy(EnemyKind.Warden, new V(0, 4), false);
            WardenEncounter.Begin(this, warden);
            Notice("The Hollowroot Warden awakens");
        }
        if (PlayerControlled) Direction = PlayerDirection.Length > 1 ? PlayerDirection.Unit : PlayerDirection;
        else
        {
            thinkTimer -= dt;
            if (thinkTimer <= 0) { thinkTimer += .1f; Think(); }
        }
        Fox = (Fox + Direction * Speed * dt).Clamp();
        if (!PlayerControlled) Recover(dt);
        UpdateEnemies(dt);
        UpdateWeapon(dt);
        UpdateShots(dt);
        if (Phase == Phase.Boss && Boss != null && Boss.hp <= 0)
        {
            Shockwaves.Clear();
            pendingWardenSummons.Clear();
            ClearHostileShots();
        }
        UpdateHazards(dt);
        UpdateShockwaves(dt);
        Collect(dt);
        RemoveDead();
        FlushWardenSummons();
        if (Phase == Phase.Combat)
        {
            spawnTimer += dt;
            float period = 1 / B.spawnRates[Stage];
            if (spawnTimer >= period)
            {
                spawnTimer -= period;
                if (EnemyCount < Cap)
                {
                    float r = rng.Next();
                    EnemyKind k = Stage == 0 || r < (Stage == 1   ? .75f
                                                     : Stage == 2 ? .60f
                                                                  : .50f)
                                      ? EnemyKind.Sprig
                                  : r < (Stage == 1   ? 1
                                         : Stage == 2 ? .85f
                                                      : .80f)
                                      ? EnemyKind.Boar
                                      : EnemyKind.Wisp;
                    AddEnemy(k, SafePerimeter(), true);
                }
            }
        }
        // Resolve every source of damage before outcome: simultaneous death is defeat; a deadline boss kill
        // is victory.
        if (HP <= 0)
            Finish(Outcome.Defeat);
        else if (Phase == Phase.Boss && Boss == null)
            Finish(Outcome.Victory);
        else if (Time >= B.maxSeconds)
            Finish(Outcome.Timeout);
    }
    public int Stage
    {
        get {
            return Math.Min(3, (int)(Time / 60));
        }
    }
    public int Cap
    {
        get {
            return Math.Min(B.enemyCap, B.phaseCaps[Stage]);
        }
    }
    public int RegularCount
    {
        get {
            int n = 0;
            foreach (var e in Enemies)
                if (e.kind != EnemyKind.Warden && e.kind != EnemyKind.MiniBoss)
                    n++;
            return n;
        }
    }
    // Capacity includes viewer mini-bosses and Warden encounter summons but
    // excludes the final Warden itself. Keeping RegularCount separate preserves
    // the legacy farm-enemy metric used by the HUD and tests.
    public int EnemyCount
    {
        get {
            int n = 0;
            foreach (var e in Enemies)
                if (e.kind != EnemyKind.Warden)
                    n++;
            return n;
        }
    }
    public int WardenMinionCount
    {
        get {
            int n = 0;
            foreach (var e in Enemies)
                if (e.wardenSummoned && e.hp > 0)
                    n++;
            return n;
        }
    }
    public int PendingWardenSummons { get { return pendingWardenSummons.Count; } }
    public void Notice(string s)
    {
        Notices.Add(s);
        while (Notices.Count > 3)
            Notices.RemoveAt(0);
        NoticeTime = 8;
    }
    public void Effect(V p, float radius, int kind, float life = .5f, string viewerName = null)
    {
        if (Bursts.Count >= B.vfxCap)
            return;
        Bursts.Add(new Burst { pos = p, radius = radius, kind = kind, life = life, maxLife = life,
                               viewerName = viewerName });
    }
    void UpdateViewerEffects(float dt)
    {
        EnemyGlow = Math.Max(0, EnemyGlow - dt);
        ReverseFacing = Math.Max(0, ReverseFacing - dt);
        EnemySpin = Math.Max(0, EnemySpin - dt);
        PetalHeart = Math.Max(0, PetalHeart - dt);
        MuddyFeet = Math.Max(0, MuddyFeet - dt);
        RainbowShots = Math.Max(0, RainbowShots - dt);
        ForceWavePulse = Math.Max(0, ForceWavePulse - dt);
        if (ForceWavePulsesRemaining <= 0)
            return;
        forceWaveTimer -= dt;
        while (ForceWavePulsesRemaining > 0 && forceWaveTimer <= 0)
        {
            ApplyForceWavePulse();
            ForceWavePulsesRemaining--;
            ForceWavePulse = .35f;
            forceWaveTimer += .6f;
        }
    }
    void ApplyForceWavePulse()
    {
        foreach (var enemy in Enemies)
        {
            if (enemy.hp <= 0 || enemy.dissolve > 0 || enemy.spawnTell > 0)
                continue;
            V away = (enemy.pos - Fox).Unit;
            if (away.Length < .1f)
                away = V.Heading((enemy.id + Seed) * .73f);
            enemy.pos = (enemy.pos + away * 2.5f).Clamp();
        }
        Effect(Fox, 2.5f, 0, .35f);
    }
    public Enemy AddEnemy(EnemyKind kind, V p, bool tell)
    {
        var e = new Enemy { id = ++nextId, kind = kind, pos = p, spawnTell = tell ? B.portalTell : 0 };
        switch (kind)
        {
        case EnemyKind.Sprig:
            e.hp = B.sprigHP;
            e.speed = B.sprigSpeed;
            e.radius = .35f;
            break;
        case EnemyKind.Boar:
            e.hp = B.boarHP;
            e.speed = B.boarSpeed;
            e.radius = .55f;
            break;
        case EnemyKind.Wisp:
            e.hp = B.wispHP;
            e.speed = B.wispSpeed;
            e.radius = .4f;
            break;
        case EnemyKind.MiniBoss:
            e.hp = B.bossHP * .25f;
            e.speed = B.boarSpeed;
            e.radius = .9f;
            e.miniBoss = true;
            break;
        default:
            e.hp = B.bossHP;
            e.radius = 1.15f;
            e.speed = WardenEncounter.MoveSpeed(B, 1);
            break;
        }
        e.maxHP = e.hp;
        Enemies.Add(e);
        return e;
    }
    internal void SpawnHostile(V p, V velocity, float damage)
    {
        Shoot(p, velocity, damage, true);
    }
    internal void QueueWardenSummon(EnemyKind kind, V p)
    {
        pendingWardenSummons.Add(new PendingWardenSummon { kind = kind, pos = p });
    }
    internal bool TryWardenSummonPosition(Enemy boss, int index, int count, out V result)
    {
        float distance = B.wardenSummonSafeDistance > 0 ? B.wardenSummonSafeDistance : 5.5f;
        float baseAngle = (float)Math.Atan2(boss.dir.y, boss.dir.x) + (float)Math.PI;
        for (int attempt = 0; attempt < 16; attempt++)
        {
            float angle = baseAngle + (float)(Math.PI * 2 * (index + attempt) /
                                              Math.Max(1, count + 2));
            V p = (boss.pos + V.Heading(angle) * distance).Clamp();
            if (V.Dist(p, Fox) >= distance)
            {
                result = p;
                return true;
            }
        }
        // SafePerimeter uses the ordinary four-unit spawn rule. Summons retain
        // their stricter effective distance when the radial candidates clamp
        // into the fox, so select a deterministic safe corner or skip this
        // candidate if the current arena position makes one impossible.
        V[] corners = {
            new V(-13.6f, -6.6f), new V(13.6f, -6.6f),
            new V(13.6f, 6.6f), new V(-13.6f, 6.6f)
        };
        for (int attempt = 0; attempt < corners.Length; attempt++)
        {
            V corner = corners[(index + attempt) % corners.Length];
            if (V.Dist(corner, Fox) >= distance)
            {
                result = corner;
                return true;
            }
        }
        result = new V();
        return false;
    }
    void FlushWardenSummons()
    {
        if (pendingWardenSummons.Count == 0)
            return;
        var boss = Boss;
        if (Phase != Phase.Boss || boss == null || boss.hp <= 0)
        {
            pendingWardenSummons.Clear();
            return;
        }
        int cap = B.wardenSummonCap > 0 ? B.wardenSummonCap : 16;
        int available = Math.Min(cap - WardenMinionCount, Cap - EnemyCount);
        for (int i = 0; i < pendingWardenSummons.Count && available > 0; i++)
        {
            var pending = pendingWardenSummons[i];
            var summon = AddEnemy(pending.kind, pending.pos, true);
            summon.wardenSummoned = true;
            summon.spawnTell = B.wardenSummonTell > 0 ? B.wardenSummonTell :
                               (B.portalTell > 0 ? B.portalTell : .8f);
            available--;
        }
        pendingWardenSummons.Clear();
    }
    public V SafePerimeter()
    {
        for (int i = 0; i < 32; i++)
        {
            int side = rng.Int(4);
            V p = side < 2 ? new V((rng.Next() * 2 - 1) * 13, side == 0 ? -6.4f : 6.4f)
                           : new V(side == 2 ? -13.4f : 13.4f, (rng.Next() * 2 - 1) * 6);
            if (V.Dist(Fox, p) >= B.spawnSafeDistance)
                return p;
        }
        return new V(Fox.x > 0 ? -13 : 13, Fox.y > 0 ? -6 : 6);
    }
    float Danger(V p)
    {
        float score = 0;
        foreach (var e in Enemies)
        {
            if (e.dissolve > 0)
                continue;
            float d = V.Dist(p, e.pos);
            score += Math.Max(0, 2.1f + e.radius - d) * 3;
            if (e.charge > 0 || e.tell > 0)
            {
                V a = e.pos, b = e.pos + e.dir * B.chargeSpeed;
                score += Math.Max(0, 1.1f - SegmentDistance(p, a, b)) * 14;
            }
        }
        foreach (var h in Hazards)
            if (!h.star)
                score += Math.Max(0, h.radius + .65f - V.Dist(p, h.pos)) * 10;
        foreach (var s in Shots)
            if (s.hostile)
                score += Math.Max(0, .85f - SegmentDistance(p, s.pos, s.pos + s.velocity * .9f)) * 8;
        foreach (var wave in Shockwaves)
        {
            float distance = V.Dist(p, wave.pos);
            // Look ahead far enough for the autonomous planner's candidate
            // movement (roughly 0.45-1s), rather than reacting only when the
            // current one-tick annulus is already at the fox.
            const float dangerHorizon = .8f;
            float futureRadius = Math.Min(wave.maxRadius + wave.halfWidth,
                                          wave.radius + Math.Max(0, wave.speed) * dangerHorizon);
            float sweptOuter = futureRadius + wave.halfWidth + B.foxRadius;
            float sweptInner = Math.Max(0, wave.radius - wave.halfWidth - B.foxRadius);
            if (futureRadius >= wave.radius && distance >= sweptInner &&
                distance <= sweptOuter && !WardenEncounter.InGap(wave, p))
                score += 24;
        }
        var boss = Boss;
        if (boss != null && boss.hp > 0 && boss.wardenState == WardenState.Windup)
        {
            float distance = V.Dist(p, boss.pos);
            if (boss.wardenMove == WardenMove.Slash)
            {
                V offset = p - boss.pos;
                float pointAngle = (float)Math.Atan2(offset.y, offset.x);
                float facing = (float)Math.Atan2(boss.dir.y, boss.dir.x);
                if (distance <= (B.wardenSlashRadius > 0 ? B.wardenSlashRadius : 2.7f) + B.foxRadius &&
                    Math.Abs(WardenEncounter.NormalizeAngle(pointAngle - facing)) <=
                        (B.wardenSlashHalfAngle > 0 ? B.wardenSlashHalfAngle : .85f))
                    score += 26;
            }
            else if (boss.wardenMove == WardenMove.Slam || boss.wardenMove == WardenMove.Spin)
                score += Math.Max(0, 3.5f - distance) * 7;
        }
        return score;
    }
    public static float SegmentDistance(V p, V a, V b)
    {
        V ab = b - a;
        float d = V.Dot(ab, ab);
        float t = d > 0 ? Math.Max(0, Math.Min(1, V.Dot(p - a, ab) / d)) : 0;
        return V.Dist(p, a + ab * t);
    }
    void Think()
    {
        if (Speed <= 0)
        {
            Direction = new V();
            return;
        }
        V objective = Fox;
        var target = Target();
        Pickup pickup = null;
        float best = 1000;
        foreach (var p in Pickups)
        {
            float d = V.Dist(Fox, p.pos);
            if (p.heal && HP < MaxHP)
            {
                float score = d - (HP < MaxHP * .45f ? 20 : 8);
                if (score < best)
                {
                    best = score;
                    pickup = p;
                }
            }
            else if (!p.heal && d < 8 && d < best)
            {
                best = d;
                pickup = p;
            }
        }
        if (pickup != null)
        {
            objective = pickup.pos;
            Intent = pickup.heal ? "Seeking a healing pod" : "Gathering embers";
        }
        else if (target != null)
        {
            V away = (Fox - target.pos).Unit;
            objective = target.pos + away * (target.kind == EnemyKind.Warden ? 4.6f : 3);
            Intent =
                target.kind == EnemyKind.Warden ? "Circling the Warden" : "Keeping a safe firing distance";
        }
        else
        {
            objective = PatrolPoints[patrol];
            if (V.Dist(Fox, objective) < 1)
                patrol = (patrol + 1) % 4;
            Intent = "Patrolling the shrine";
        }
        float bestScore = float.NegativeInfinity;
        V chosen = new V();
        float current = V.Dist(Fox, objective);
        float intentSpeed = Danger(Fox) > 1 ? Speed : Math.Min(Speed, Math.Max(.8f, current));
        for (int i = 0; i < 17; i++)
        {
            V h = i == 16 ? new V() : V.Heading(i * (float)Math.PI / 8);
            V predicted = Fox + h * intentSpeed;
            V clamped = predicted.Clamp();
            float score = (current - V.Dist(clamped, objective)) * 1.4f -
                          Danger(Fox + h * intentSpeed * .45f) * 2 - Danger(clamped) -
                          V.Dist(predicted, clamped) * 15;
            if (target != null)
            {
                float range = V.Dist(clamped, target.pos);
                score -= Math.Max(0, range - B.attackRange) * .5f;
            }
            score += V.Dot(h, Direction) * .08f + ((i + Seed) % 17) * .00001f;
            if (score > bestScore)
            {
                bestScore = score;
                chosen = h * (intentSpeed / Speed);
            }
        }
        Direction = chosen;
        if (Danger(Fox) > 3)
            Intent = "Evading corruption";
    }
    void Recover(float dt)
    {
        if (Direction.Length < .1f)
        {
            stallTime = 0;
            stallOrigin = Fox;
            return;
        }
        stallTime += dt;
        if (V.Dist(stallOrigin, Fox) >= .25f)
        {
            stallTime = 0;
            stallOrigin = Fox;
            return;
        }
        if (stallTime > 8)
        {
            V best = Fox;
            float danger = float.MaxValue;
            for (int i = 0; i < 16; i++)
            {
                V p = (Fox + V.Heading(i * (float)Math.PI / 8) * 2).Clamp();
                float d = Danger(p);
                if (d < danger)
                {
                    danger = d;
                    best = p;
                }
            }
            Fox = best;
            stallTime = 0;
            stallOrigin = Fox;
            UnstuckEvents++;
            Effect(Fox, 2, 0);
            Notice("Guardian recovered from a blocked path");
        }
        else if (stallTime > 2)
            Direction = V.Heading((Seed + patrol++) * .8f);
    }
    public Enemy Target()
    {
        Enemy nearest = null;
        float d = B.attackRange;
        foreach (var e in Enemies)
        {
            if (e.hp <= 0 || e.spawnTell > 0 || e.dissolve > 0)
                continue;
            float n = V.Dist(Fox, e.pos);
            if (n < d)
            {
                d = n;
                nearest = e;
            }
        }
        var boss = Boss;
        if (boss != null && boss.hp > 0 && V.Dist(Fox, boss.pos) <= B.attackRange &&
            (nearest == null || d > 2))
            return boss;
        return nearest;
    }
    void UpdateEnemies(float dt)
    {
        foreach (var e in Enemies)
        {
            if (e.hp <= 0)
                continue;
            if (e.dissolve > 0)
            {
                e.dissolve -= dt;
                if (e.dissolve <= 0)
                    e.hp = 0;
                continue;
            }
            if (e.spawnTell > 0)
            {
                e.spawnTell -= dt;
                continue;
            }
            e.cooldown -= dt;
            e.shotCooldown -= dt;
            e.orbitTimer -= dt;
            V diff = Fox - e.pos;
            float dist = diff.Length;
            float rush = Rush > 0 ? B.rushMultiplier : 1;
            if (e.kind == EnemyKind.Warden)
            {
                WardenEncounter.Update(this, e, dt);
            }
            else if (e.kind == EnemyKind.Boar)
            {
                if (e.shotTell > 0)
                {
                    UpdateShotTell(e, diff, dt);
                }
                else if (e.tell > 0)
                {
                    // The boar tracks the fox through most of the readable tell,
                    // then commits during the final quarter-second for a fair dodge.
                    if (e.tell > .25f)
                        e.dir = diff.Unit;
                    e.tell -= dt;
                    if (e.tell <= 0)
                        e.charge = EnemyCombat.Positive(B.chargeDuration, 1.8f);
                }
                else if (e.charge > 0)
                {
                    e.pos = (e.pos + e.dir * EnemyCombat.Positive(B.chargeSpeed, 4.2f) * rush * dt).Clamp();
                    e.charge -= dt;
                    if (e.charge <= 0)
                        e.cooldown = EnemyCombat.Positive(B.chargeCooldown, 4);
                }
                else if (e.cooldown <= 0 && dist >= 4 && dist <= 7)
                {
                    e.dir = diff.Unit;
                    e.tell = EnemyCombat.Positive(B.chargeTell, 1);
                }
                else if (e.shotCooldown <= 0 && dist >= 1 && dist <= 8.5f)
                {
                    BeginShot(e, diff);
                }
                else
                    e.pos = (e.pos + diff.Unit * e.speed * rush * dt).Clamp();
            }
            else if (e.kind == EnemyKind.Sprig || e.kind == EnemyKind.Wisp)
            {
                if (e.shotTell > 0)
                {
                    UpdateShotTell(e, diff, dt);
                }
                else if (e.tell > 0)
                {
                    // Sprigs commit toward the fox. Wisps only lunge away when
                    // crowded, preserving their readable ranged spacing.
                    if (e.tell > .15f)
                        e.dir = e.kind == EnemyKind.Sprig ? diff.Unit : (e.pos - Fox).Unit;
                    e.tell -= dt;
                    if (e.tell <= 0)
                        e.charge = EnemyCombat.LungeDuration(B);
                }
                else if (e.charge > 0)
                {
                    e.pos = (e.pos + e.dir * EnemyCombat.LungeSpeed(B) * rush * dt).Clamp();
                    e.charge -= dt;
                    if (e.charge <= 0)
                        e.cooldown = EnemyCombat.LungeCooldown(B);
                }
                else if (e.kind == EnemyKind.Wisp && e.cooldown <= 0 && dist < 4.25f)
                {
                    e.dir = (e.pos - Fox).Unit;
                    e.tell = EnemyCombat.LungeTell(B);
                }
                else if (e.kind == EnemyKind.Sprig && e.cooldown <= 0 && dist >= 2.2f && dist <= 6.8f)
                {
                    e.dir = diff.Unit;
                    e.tell = EnemyCombat.LungeTell(B);
                }
                else if (e.shotCooldown <= 0 && dist >= (e.kind == EnemyKind.Wisp ? 4.2f : 2.2f) && dist <= 8)
                {
                    BeginShot(e, diff);
                }
                else if (e.kind == EnemyKind.Wisp)
                {
                    float spacing = dist > 5.3f ? 1 : dist < 4.6f ? -1 : 0;
                    e.pos = (e.pos + diff.Unit * spacing * e.speed * rush * dt).Clamp();
                }
                else
                    e.pos = (e.pos + diff.Unit * e.speed * rush * dt).Clamp();
            }
            else if (e.kind == EnemyKind.MiniBoss)
            {
                // Mini-bosses borrow the Warden's telegraphed attacks but use
                // a summon-free cycle and never satisfy Boss (which only
                // returns the final EnemyKind.Warden).
                WardenEncounter.UpdateMiniBoss(this, e, dt);
            }
            else
                e.pos = (e.pos + diff.Unit * e.speed * rush * dt).Clamp();
            if (V.Dist(Fox, e.pos) < B.foxRadius + e.radius)
                Hurt(e.kind == EnemyKind.Boar    ? B.boarDamage * (e.champion ?
                     (B.championDamageMultiplier > 0 ? B.championDamageMultiplier : 1.25f) : 1)
                     : e.kind == EnemyKind.Sprig ? B.sprigDamage
                     : e.kind == EnemyKind.Wisp  ? B.wispDamage
                                                 : B.bossDamage);
            if (Upgrades[5] > 0 && V.Dist(Fox, e.pos) < 1.6f && e.orbitTimer <= 0)
            {
                e.hp -= 5;
                e.orbitTimer = 1;
                Effect(e.pos, .4f, 0);
            }
        }
    }
    void BeginShot(Enemy e, V diff)
    {
        e.dir = diff.Unit;
        e.shotTell = EnemyCombat.ShotTell(B, e.kind);
    }
    void UpdateShotTell(Enemy e, V diff, float dt)
    {
        // Follow the fox through the tell, with a short lock at the end so the
        // fan remains aimed but still gives a moving fox a readable dodge.
        if (e.shotTell > .15f)
            e.dir = diff.Unit;
        e.shotTell -= dt;
        if (e.shotTell <= 0)
        {
            FireEnemyFan(e);
            e.shotCooldown = EnemyCombat.ShotCooldown(B, e.kind);
        }
    }
    void FireEnemyFan(Enemy e)
    {
        int count = EnemyCombat.ProjectileCount(B, e.kind);
        float damage = e.kind == EnemyKind.Boar ? B.boarDamage :
                       e.kind == EnemyKind.Sprig ? B.sprigDamage : B.wispDamage;
        damage *= EnemyCombat.ProjectileDamageScale(B);
        for (int i = 0; i < count; i++)
            Shoot(e.pos, EnemyCombat.FanVelocity(e.dir, i, count,
                                                 EnemyCombat.ProjectileSpread(B, e.kind),
                                                 EnemyCombat.ProjectileSpeed(B, e.kind)), damage, true);
    }
    void Shoot(V p, V vel, float damage, bool hostile)
    {
        if (Shots.Count >= B.projectileCap)
            return;
        Shots.Add(new Shot { pos = p, velocity = vel, damage = damage, hostile = hostile,
                             life = hostile ? 12 : 1, pierce = hostile ? 0 : Upgrades[4] });
    }
    void UpdateWeapon(float dt)
    {
        fireTimer -= dt;
        var target = Target();
        if (target == null)
            return;
        if (Laser > 0)
        {
            target.hp -= B.laserDPS * dt;
            return;
        }
        if (fireTimer <= 0)
        {
            fireTimer = Interval;
            Shoot(Fox, (target.pos - Fox).Unit * B.boltSpeed,
                  Damage * (RainbowShots > 0 ? 2 : 1), false);
        }
    }
    void UpdateShots(float dt)
    {
        for (int i = Shots.Count - 1; i >= 0; i--)
        {
            var s = Shots[i];
            V old = s.pos;
            s.pos += s.velocity * dt;
            s.life -= dt;
            if (s.hostile)
            {
                if (SegmentDistance(Fox, old, s.pos) < B.foxRadius + .12f)
                {
                    Hurt(s.damage);
                    s.life = 0;
                }
            }
            else
            {
                foreach (var e in Enemies)
                {
                    if (e.hp <= 0 || e.spawnTell > 0 || e.dissolve > 0 || s.hit.Contains(e.id))
                        continue;
                    if (SegmentDistance(e.pos, old, s.pos) < e.radius + .14f)
                    {
                        e.hp -= s.damage;
                        s.hit.Add(e.id);
                        Effect(e.pos, .35f, 0);
                        if (e.kind == EnemyKind.Sprig)
                            e.pos = (e.pos + s.velocity.Unit * .18f).Clamp();
                        if (s.pierce-- <= 0 || e.kind == EnemyKind.Warden)
                        {
                            s.life = 0;
                            break;
                        }
                    }
                }
            }
            if (s.life <= 0 || Math.Abs(s.pos.x) > 17 || Math.Abs(s.pos.y) > 10)
                Shots.RemoveAt(i);
        }
    }
    void UpdateHazards(float dt)
    {
        for (int i = Hazards.Count - 1; i >= 0; i--)
        {
            var h = Hazards[i];
            if (h.tell > 0)
            {
                h.tell -= dt;
                continue;
            }
            if (h.star)
            {
                foreach (var e in Enemies)
                    if (e.spawnTell <= 0 && V.Dist(e.pos, h.pos) <= h.radius)
                        e.hp -= B.starDamage;
                // Keep star attribution after the tell hazard is consumed. A
                // star is not a viewer mischief hazard: viewerName is display
                // identity only and must not participate in the lock.
                Effect(h.pos, h.radius, 1, 1, h.viewerName);
                Hazards.RemoveAt(i);
                continue;
            }
            h.life -= dt;
            h.tick -= dt;
            if (h.tick <= 0)
            {
                h.tick += 1;
                if (V.Dist(Fox, h.pos) < h.radius + B.foxRadius)
                {
                    Hurt(h.damage);
                    if (h.viewer)
                        Effect(h.pos, .5f, 2, .5f, h.viewerName);
                }
            }
            if (h.life <= 0)
                Hazards.RemoveAt(i);
        }
    }
    void UpdateShockwaves(float dt)
    {
        for (int i = Shockwaves.Count - 1; i >= 0; i--)
        {
            var wave = Shockwaves[i];
            wave.previousRadius = wave.radius;
            wave.radius += Math.Max(0, wave.speed) * dt;
            float distance = V.Dist(Fox, wave.pos);
            float inner = Math.Max(0, Math.Min(wave.previousRadius, wave.radius) - wave.halfWidth);
            float outer = Math.Max(wave.previousRadius, wave.radius) + wave.halfWidth;
            if (!wave.hit && distance + B.foxRadius >= inner &&
                distance - B.foxRadius <= outer && !WardenEncounter.InGap(wave, Fox))
            {
                wave.hit = true;
                Hurt(wave.damage);
            }
            if (wave.radius > wave.maxRadius + wave.halfWidth)
                Shockwaves.RemoveAt(i);
        }
    }
    void ClearHostileShots()
    {
        for (int i = Shots.Count - 1; i >= 0; i--)
            if (Shots[i].hostile)
                Shots.RemoveAt(i);
    }
    void ClearWardenEncounterEntities(bool clearHostileShots)
    {
        // Run this as a separate index pass before the normal dead-enemy pass;
        // no active foreach over Enemies is mutated.
        for (int i = Enemies.Count - 1; i >= 0; i--)
            if (Enemies[i].wardenSummoned)
                Enemies.RemoveAt(i);
        Shockwaves.Clear();
        pendingWardenSummons.Clear();
        if (clearHostileShots)
            ClearHostileShots();
    }
    public void Hurt(float amount)
    {
        if (Immune > 0)
            return;
        HP = Math.Max(0, HP - amount);
        Immune = B.immunity;
        Effect(Fox, .8f, 2);
    }
    void RemoveDead()
    {
        bool wardenDied = false;
        foreach (var e in Enemies)
            if (e.kind == EnemyKind.Warden && e.hp <= 0)
            {
                wardenDied = true;
                break;
            }
        if (wardenDied)
            ClearWardenEncounterEntities(true);
        for (int i = Enemies.Count - 1; i >= 0; i--)
        {
            var e = Enemies[i];
            if (e.hp > 0)
                continue;
            Enemies.RemoveAt(i);
            // Warden summons do not grant farmable rewards; surviving regular and viewer enemies still do.
            if (e.wardenSummoned)
                continue;
            Kills++;
            if (e.kind != EnemyKind.Warden)
                DropXP(e.pos, e.kind == EnemyKind.Boar ? 3 : 1);
            Effect(e.pos, e.radius + 1, 2);
        }
    }
    public void DropXP(V p, int value)
    {
        int xpCount = 0;
        Pickup nearest = null;
        float best = float.MaxValue;
        foreach (var x in Pickups)
        {
            if (x.heal)
                continue;
            xpCount++;
            float d = V.Dist(p, x.pos);
            if (d < best)
            {
                best = d;
                nearest = x;
            }
        }
        if (xpCount >= B.xpCap && nearest != null)
            nearest.xp += value;
        else
            Pickups.Add(new Pickup { pos = p, xp = value, life = float.MaxValue });
    }
    void Collect(float dt)
    {
        for (int i = Pickups.Count - 1; i >= 0; i--)
        {
            var p = Pickups[i];
            p.life -= dt;
            if (V.Dist(Fox, p.pos) < .65f)
            {
                if (p.heal)
                    HP = Math.Min(MaxHP, HP + B.healAmount);
                else
                    XP += p.xp;
                Effect(p.pos, .6f, 0);
                Pickups.RemoveAt(i);
            }
            else if (p.life <= 0)
                Pickups.RemoveAt(i);
        }
        while (Level < B.thresholds.Length && XP >= B.thresholds[Level])
            GrantUpgrade();
    }
    public void GrantUpgrade()
    {
        if (Level >= 6)
            return;
        int u = ChooseUpgrade();
        Upgrades[u]++;
        Level++;
        if (u == 3)
        {
            MaxHP += 20;
            HP += 20;
        }
        Notice("Upgrade: " + UpgradeName(u));
        Effect(Fox, 1.6f, 0, 1);
    }
    public int ChooseUpgrade()
    {
        if (HP < MaxHP * .45f && Upgrades[3] < 2)
            return 3;
        if (Upgrades[4] == 0)
            return 4;
        if (Upgrades[5] == 0)
            return 5;
        if (Upgrades[0] < 3 && Upgrades[0] <= Upgrades[1])
            return 0;
        if (Upgrades[1] < 3)
            return 1;
        if (Upgrades[2] < 2)
            return 2;
        return 3;
    }
    public static string UpgradeName(int i)
    {
        return new[] { "Ember Power", "Quickcast",      "Fleet Paws",
                       "Vitality",    "Piercing Ember", "Orbiting Spark" }[i];
    }
    void Finish(Outcome result)
    {
        RunInteractions.Seal();
        bool wardenEncounter = Phase == Phase.Boss;
        if (!wardenEncounter)
            foreach (var e in Enemies)
                if (e.kind == EnemyKind.Warden || e.wardenSummoned)
                {
                    wardenEncounter = true;
                    break;
                }
        if (wardenEncounter)
            ClearWardenEncounterEntities(true);
        Outcome = result;
        Phase = Phase.Results;
        StateTime = 0;
        var r = new RunResult { seed = Seed,      run = RunNumber,           kills = Kills,
                                upgrades = Level, actions = ExecutedActions, seconds = Time,
                                hp = HP,          outcome = result };
        History.Add(r);
        if (History.Count > 100)
            History.RemoveAt(0);
        if (Finished != null)
            Finished(r);
    }
    public void AnnounceViewer(string viewerName, string actionName)
    {
        ViewerAnnouncements.Add(new ViewerAnnouncement {
            viewerName = Actions.ViewerName(viewerName), actionName = actionName, createdAt = Clock
        });
        if (ViewerAnnouncements.Count > 3) ViewerAnnouncements.RemoveAt(0);
    }
    public void Execute(ActionKind action, string displayName = "MuxyVisitor")
    {
        switch (action)
        {
        case ActionKind.TealCelebration:
            Celebration = 3;
            break;
        case ActionKind.EnemyGlow:
            EnemyGlow = 8;
            break;
        case ActionKind.ReverseFacing:
            ReverseFacing = 8;
            break;
        case ActionKind.EnemySpin:
            EnemySpin = 6;
            break;
        case ActionKind.PetalHeart:
            PetalHeart = 6;
            PetalHeartPosition = Fox;
            break;
        case ActionKind.HealthCut:
            HP = Math.Max(0, HP * .5f);
            break;
        case ActionKind.MiniBoss:
            var mini = AddEnemy(EnemyKind.MiniBoss, SafePerimeter(), true);
            WardenEncounter.Begin(this, mini);
            break;
        case ActionKind.MuddyFeet:
            MuddyFeet = 8;
            break;
        case ActionKind.ForceWave:
            // The first pulse is applied at activation time; the two remaining
            // pulses are scheduled at .6s and 1.2s from this same clock point.
            ForceWavePulsesRemaining = 3;
            forceWaveTimer = 0;
            ApplyForceWavePulse();
            ForceWavePulsesRemaining--;
            forceWaveTimer = .6f;
            ForceWavePulse = .35f;
            break;
        case ActionKind.RainbowShots:
            RainbowShots = 5;
            break;
        case ActionKind.HealPod:
            V safe = Fox;
            float score = float.MaxValue;
            for (int i = 0; i < 16; i++)
            {
                V p = (Fox + V.Heading(i * (float)Math.PI / 8) * 2).Clamp();
                float d = Danger(p);
                if (d < score)
                {
                    score = d;
                    safe = p;
                }
            }
            Pickups.Add(new Pickup { pos = safe, heal = true, life = B.healExpiry });
            break;
        case ActionKind.LaserMode:
            Laser = B.laserDuration;
            break;
        case ActionKind.StarStrike:
            Enemy cluster = null;
            int max = -1;
            foreach (var e in Enemies)
            {
                int n = 0;
                foreach (var other in Enemies)
                    if (V.Dist(e.pos, other.pos) <= B.starRadius)
                        n++;
                if (n > max)
                {
                    max = n;
                    cluster = e;
                }
            }
            if (cluster != null)
                Hazards.Add(
                    new Hazard { pos = cluster.pos, radius = B.starRadius, tell = B.starTell, star = true,
                                 viewerName = Actions.ViewerName(displayName) });
            break;
        case ActionKind.SprigSwarm:
            V portal = SafePerimeter();
            for (int i = 0; i < 3; i++)
                AddEnemy(EnemyKind.Sprig, portal, true);
            break;
        case ActionKind.ThornPatch:
            Hazards.Add(
                new Hazard { pos = (Fox + (Direction.Length > .1f ? Direction : new V(1, 0)) * 2.5f).Clamp(),
                             radius = B.thornRadius, tell = B.rootTell, life = B.thornDuration,
                             damage = B.thornDamage, viewer = true,
                             viewerName = Actions.ViewerName(displayName) });
            break;
        case ActionKind.EnemyRush:
            Rush = B.rushDuration;
            break;
        case ActionKind.OrangeChampion:
            var champion = AddEnemy(EnemyKind.Boar, SafePerimeter(), true);
            champion.champion = true;
            champion.displayName = Actions.ViewerName(displayName);
            champion.hp *= B.championHealthMultiplier > 0 ? B.championHealthMultiplier : 3;
            champion.maxHP = champion.hp;
            champion.radius *= 1.2f;
            break;
        }
        ExecutedActions++;
        AnnounceViewer(displayName, Actions.Label(action));
    }
}
}
