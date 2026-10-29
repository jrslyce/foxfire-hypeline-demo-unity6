using System;
namespace Foxfire
{
public enum WardenClip { Idle, Walking, Running, Spin, Slam, Slash, Dead }

// Presentation follows simulation time, including pause, speed changes and run resets.
public sealed class WardenAnimation
{
    public WardenClip Clip { get; private set; }
    public float Seconds { get; private set; }
    public float Alpha { get; private set; }
    public bool Hurt { get; private set; }
    public V Position { get; private set; }
    public bool Visible { get; private set; }
    public int FacingSign { get; private set; } = 1;
    public V FacingDirection { get; private set; } = new V(1, 0);
    public WardenState State { get; private set; } = WardenState.Dormant;
    public WardenMove Move { get; private set; } = WardenMove.None;
    public int Stage { get; private set; }
    Enemy tracked;
    int run = -1;
    double started, hurtUntil, previousClock, stateClock, impactClock;
    float hp;

    public static float Duration(WardenClip clip)
    {
        switch (clip)
        {
            case WardenClip.Walking: return 23f / 24;
            case WardenClip.Running: return 15f / 24;
            case WardenClip.Spin: return 73f / 24;
            case WardenClip.Slam: return 3;
            case WardenClip.Slash: return 53f / 24;
            case WardenClip.Dead: return 71f / 24;
            default: return 1;
        }
    }
    // Retained for callers from the released presentation test. Runtime selection below never uses
    // attack parity; the encounter's explicit WardenMove is authoritative.
    [Obsolete("Use ClipFor(WardenState, WardenMove) from the encounter state.")]
    public static WardenClip Attack(int index) => index % 2 == 1 ? WardenClip.Slam :
        index % 4 == 0 ? WardenClip.Spin : WardenClip.Slash;

    // Release frames chosen from the supplied animation: airborne spin, downward slam and slash.
    public static float Impact(WardenClip clip) => clip == WardenClip.Spin ? 1.5f : clip == WardenClip.Slam ? 1.65f : 1.1f;
    public static float Impact(WardenMove move) => Impact(ClipFor(WardenState.Windup, move));

    static float ClampSeconds(double value, float max)
    {
        if (value <= 0) return 0;
        if (value >= max) return max;
        return (float)value;
    }

    public static WardenClip ClipFor(WardenState state, WardenMove move)
    {
        if (state == WardenState.Windup || state == WardenState.Recover)
        {
            switch (move)
            {
                case WardenMove.Slash: return WardenClip.Slash;
                case WardenMove.Slam: return WardenClip.Slam;
                case WardenMove.Spin:
                case WardenMove.Summon: return WardenClip.Spin;
            }
        }
        return state == WardenState.Chase ? WardenClip.Walking : WardenClip.Idle;
    }

    public void Update(Simulation game)
    {
        double now = game.Clock;
        if (run != game.RunNumber)
        {
            run = game.RunNumber; tracked = null; Visible = false; Clip = WardenClip.Idle;
            State = WardenState.Dormant; Move = WardenMove.None; Stage = 0; FacingSign = 1; FacingDirection = new V(1, 0);
            hurtUntil = 0; started = now; stateClock = now; impactClock = now; previousClock = now;
        }
        var boss = game.Boss;
        Hurt = false; Alpha = 1;
        if (boss != null)
        {
            if (tracked != boss)
            {
                tracked = boss; hp = boss.hp; Position = boss.pos;
                State = boss.wardenState; Move = boss.wardenMove; Stage = boss.wardenStage;
                stateClock = boss.wardenStateClock; impactClock = boss.wardenImpactClock;
                Clip = ClipFor(State, Move); started = State == WardenState.Recover
                    ? impactClock - Impact(Move) : stateClock;
                if (double.IsNaN(started) || double.IsInfinity(started)) started = now;
            }
            Visible = boss.spawnTell <= 0;
            V previousPosition = Position;
            Position = boss.pos;
            if (boss.hp < hp) hurtUntil = now + .15;
            hp = boss.hp; Hurt = now < hurtUntil;

            var nextState = boss.wardenState;
            var nextMove = boss.wardenMove;
            bool entered = nextState != State || nextMove != Move ||
                           boss.wardenStateClock != stateClock || boss.wardenImpactClock != impactClock;
            State = nextState;
            Move = nextMove;
            Stage = boss.wardenStage;
            stateClock = boss.wardenStateClock;
            impactClock = boss.wardenImpactClock;

            // A nearly vertical direction should not make the single-view atlas chatter between
            // mirrored poses. Only a meaningful horizontal component changes the persistent facing.
            if (boss.dir.x < -.15f)
                FacingSign = -1;
            else if (boss.dir.x > .15f)
                FacingSign = 1;
            if (boss.dir.Length > .0001f)
                FacingDirection = boss.dir.Unit;

            WardenClip desired = ClipFor(State, Move);
            if (State == WardenState.Chase)
            {
                // Chase is explicit, while the supplied running loop is selected only from actual
                // movement speed. This keeps a stationary windup/recovery from looking like locomotion.
                double elapsed = Math.Max(0, now - previousClock);
                float distance = V.Dist(boss.pos, previousPosition);
                if (elapsed > 0)
                    desired = distance / elapsed > 1.5 ? WardenClip.Running : WardenClip.Walking;
                else if (!entered && (Clip == WardenClip.Walking || Clip == WardenClip.Running))
                    desired = Clip;
            }

            if (entered || desired != Clip)
            {
                Clip = desired;
                started = State == WardenState.Recover ? impactClock - Impact(Move) :
                          State == WardenState.Chase && !entered ? now : stateClock;
                if (double.IsNaN(started) || double.IsInfinity(started)) started = now;
            }
        }
        else if (tracked != null && tracked.hp <= 0)
        {
            if (Clip != WardenClip.Dead) { Clip = WardenClip.Dead; started = now; }
            Alpha = (float)Math.Max(0, Math.Min(1, (Duration(Clip) + .5 - (now - started)) / .5));
            Visible = Alpha > 0;
        }
        else Visible = false;
        bool locomotion = Clip == WardenClip.Walking || Clip == WardenClip.Running;
        Seconds = locomotion ? (float)Math.Max(0, now - started) : ClampSeconds(now - started, Duration(Clip));
        previousClock = now;
    }
}
}
