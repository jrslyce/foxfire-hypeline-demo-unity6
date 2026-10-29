using System;
using System.Collections.Generic;
using System.IO;
namespace Foxfire
{
public enum ActionKind
{
    HealPod,
    LaserMode,
    StarStrike,
    SprigSwarm,
    ThornPatch,
    EnemyRush,
    OrangeChampion,
    TealCelebration,
    EnemyGlow,
    ReverseFacing,
    EnemySpin,
    PetalHeart,
    HealthCut,
    MiniBoss,
    MuddyFeet,
    ForceWave,
    RainbowShots
}
public static class Actions
{
    public static readonly string[] Ids = { "heal_pod",    "laser_mode",  "star_strike",
                                            "sprig_swarm", "thorn_patch", "enemy_rush", "orange_champion", "teal_celebration",
                                            "enemy_glow", "reverse_facing", "enemy_spin", "petal_heart", "health_cut",
                                            "mini_boss", "muddy_feet", "force_wave", "rainbow_shots" };
    static readonly string[] Labels = { "Healing Pod", "Laser Mode",  "Star Strike",
                                        "Shroom Swarm", "Thorn Patch", "Enemy Rush", "Orange Champion", "Teal Celebration",
                                        "Enemy Glow", "Reverse Facing", "Enemy Spin", "Petal Heart", "50% Health Cut",
                                        "Mini-Boss", "Muddy Feet", "Force Wave", "Rainbow Shots" };
    public static int Count { get { return Ids.Length; } }
    static readonly string[] Descriptions = {
        "Drop a healing pod for the fox.", "Give the fox a powerful laser beam.",
        "Strike a group of enemies with starlight.", "Send three shroomlings into the clearing.",
        "Place a thorn patch in the fox's path.", "Give enemies a short burst of speed.",
        "Summon a tougher orange boar with your viewer name above it.",
        "Celebrate with teal sparkles around the fox for three seconds.",
        "Make every enemy glow for eight seconds.", "Make characters and enemies move opposite their facing for eight seconds.",
        "Send enemies into a six-second spin.", "Place a heart of petals at the fox for six seconds.",
        "Cut the fox's current health in half.", "Summon a separate mini-boss at quarter health.",
        "Slow the fox to half speed for eight seconds.", "Knock enemies back in three radial waves.",
        "Double friendly bullet damage for five seconds." };
    public static string Description(ActionKind k) { return Descriptions[(int)k]; }
    // Old saved balance assets may have only the original catalog cooldowns.
    public static float Cooldown(Balance b, ActionKind k)
    {
        return b.actionCooldowns != null && (int)k < b.actionCooldowns.Length
            ? b.actionCooldowns[(int)k] : DefaultCooldown(k);
    }
    static float DefaultCooldown(ActionKind k)
    {
        switch (k)
        {
        case ActionKind.TealCelebration:
        case ActionKind.EnemyGlow:
        case ActionKind.ReverseFacing:
        case ActionKind.EnemySpin:
        case ActionKind.PetalHeart:
        case ActionKind.RainbowShots:
            return 10;
        case ActionKind.HealthCut:
            return 20;
        case ActionKind.MiniBoss:
            return 30;
        case ActionKind.MuddyFeet:
            return 15;
        case ActionKind.ForceWave:
            return 12;
        case ActionKind.OrangeChampion:
            return 5;
        default:
            return 45;
        }
    }
    // Plain, bounded text for game nameplates; identity itself comes from the trusted adapter.
    public static string ViewerName(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return "MuxyVisitor";
        var result = new System.Text.StringBuilder(25);
        foreach (char c in name.Trim())
        {
            if (char.IsLetterOrDigit(c) || c == '_' || c == '#' || c == '-') result.Append(c);
            if (result.Length == 25) break;
        }
        return result.Length > 0 ? result.ToString() : "MuxyVisitor";
    }
    public static string Label(ActionKind k)
    {
        return Labels[(int)k];
    }
    public static bool Hinder(ActionKind k)
    {
        return k == ActionKind.SprigSwarm || k == ActionKind.ThornPatch ||
               k == ActionKind.EnemyRush || k == ActionKind.OrangeChampion ||
               k == ActionKind.HealthCut || k == ActionKind.MiniBoss || k == ActionKind.MuddyFeet;
    }
    public static string Category(ActionKind k)
    {
        if (k == ActionKind.TealCelebration || k == ActionKind.EnemyGlow || k == ActionKind.ReverseFacing ||
            k == ActionKind.EnemySpin || k == ActionKind.PetalHeart)
            return "cosmetic";
        return Hinder(k) ? "hinder" : "help";
    }
    public static bool DemoName(string name)
    {
        return name == "Muxy#1" || name == "LadyBird" || name == "BBQ-fan" || name == "RainyStreet";
    }
    public static string Scoped(string run, ActionKind k)
    {
        return "fox_" + run + "_" + Ids[(int)k];
    }
}
public class ActionRequest
{
    public string receiptId, sessionId, actionId, displayName, eventKind;
    public long expiresAtUnixMs;
    public double queuedAt;
    public ActionKind kind;
}
public class Receipt
{
    public string id, status, reason;
    public Receipt(string id, string status, string reason = "")
    {
        this.id = id;
        this.status = status;
        this.reason = reason;
    }
}
public interface IViewerInteractionGateway : IDisposable
{
    string Status { get; }
    bool Ready { get; }
    bool IsSimulation { get; }
    event Action<ActionRequest> Requested;
    void Tick();
    void Publish(Simulation game, InteractionDispatcher dispatcher);
    void SendReceipt(Receipt receipt);
}
public sealed class LocalSimulatorGateway : IViewerInteractionGateway
{
    public string Status
    {
        get {
            return "LOCAL SIMULATION · Muxy disconnected";
        }
    }
    public bool Ready
    {
        get {
            return true;
        }
    }
    public bool IsSimulation
    {
        get {
            return true;
        }
    }
    public event Action<ActionRequest> Requested;
    public Receipt LastReceipt;
    public void Submit(Simulation game, ActionKind action, string session = "local-operator")
    {
        if (Requested != null)
            Requested(new ActionRequest { receiptId = Guid.NewGuid().ToString("N"), sessionId = session,
                                          actionId = Actions.Scoped(game.RunId, action) });
    }
    public void Tick()
    {
    }
    public void Publish(Simulation g, InteractionDispatcher d)
    {
    }
    public void SendReceipt(Receipt r)
    {
        LastReceipt = r;
    }
    public void Dispose()
    {
        Requested = null;
    }
}
// Terminal receipts append synchronously, with atomic bounded compaction. Pending requests never persist.
public sealed class ReceiptJournal
{
    readonly string path;
    readonly int cap;
    int records;
    readonly Queue<string> order = new Queue<string>();
    readonly Dictionary<string, Receipt> entries = new Dictionary<string, Receipt>();
    public int Count
    {
        get {
            return entries.Count;
        }
    }
    public ReceiptJournal(int cap, string path = null)
    {
        if (cap < 1)
            throw new ArgumentOutOfRangeException("cap");
        this.cap = cap;
        this.path = path;
        if (path != null && File.Exists(path))
        {
            bool completeTail;
            using (var file = File.OpenRead(path))
            {
                completeTail = file.Length == 0;
                if (file.Length > 0)
                {
                    file.Seek(-1, SeekOrigin.End);
                    completeTail = file.ReadByte() == '\n';
                }
            }
            using (var reader = new StreamReader(path))
                while (!reader.EndOfStream)
                {
                    string line = reader.ReadLine();
                    // A killed writer may leave a partial last record. Never acknowledge it on replay.
                    if (reader.EndOfStream && !completeTail)
                        break;
                    string[] p = line.Split('\t');
                    if (p.Length == 3)
                    {
                        Remember(new Receipt(p[0], p[1], p[2]), false);
                        records++;
                    }
                }
            if (!completeTail || records >= 2L * cap)
                Compact();
        }
    }
    public Receipt Find(string id)
    {
        Receipt r;
        return id != null && entries.TryGetValue(id, out r) ? r : null;
    }
    public void Remember(Receipt r, bool persist = true)
    {
        if (!entries.ContainsKey(r.id))
            order.Enqueue(r.id);
        entries[r.id] = r;
        while (order.Count > cap)
            entries.Remove(order.Dequeue());
        if (persist && path != null)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)));
            if (records >= 2L * cap - 1)
                Compact();
            else
            {
                using (var file = new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.Read, 256))
                using (var writer = new StreamWriter(file, System.Text.Encoding.UTF8, 256))
                    WriteReceipt(writer, r);
                records++;
            }
        }
    }
    static void WriteReceipt(StreamWriter writer, Receipt r)
    {
        writer.Write(r.id); writer.Write('\t'); writer.Write(r.status);
        writer.Write('\t'); writer.WriteLine(r.reason);
    }
    void Compact()
    {
        string tmp = path + ".tmp";
        using (var writer = new StreamWriter(tmp))
            foreach (string key in order)
                WriteReceipt(writer, entries[key]);
        if (File.Exists(path))
            File.Replace(tmp, path, null);
        else
            File.Move(tmp, path);
        records = entries.Count;
    }
}
public sealed class InteractionDispatcher : IDisposable
{
    public readonly ReceiptJournal Journal;
    readonly Simulation game;
    readonly IViewerInteractionGateway gateway;
    readonly List<ActionRequest> queue = new List<ActionRequest>();
    readonly HashSet<ActionKind> reserved = new HashSet<ActionKind>();
    readonly Dictionary<string, double> sessions = new Dictionary<string, double>();
    readonly double[] cooldowns = new double[Actions.Count];
    double dispatchAt;
    public int QueueCount
    {
        get {
            return queue.Count;
        }
    }
    public event Action<Receipt> Received;
    public InteractionDispatcher(Simulation game, IViewerInteractionGateway gateway, ReceiptJournal journal)
    {
        this.game = game;
        this.gateway = gateway;
        Journal = journal;
        gateway.Requested += SubmitEvent;
        game.Resetting += Reset;
    }
    void SubmitEvent(ActionRequest request)
    {
        Submit(request);
    }
    static bool ValidId(string s)
    {
        if (string.IsNullOrEmpty(s) || s.Length > 160)
            return false;
        foreach (char c in s)
            if (!(char.IsLetterOrDigit(c) || c == '-' || c == '_' || c == '.' || c == ':'))
                return false;
        return true;
    }
    Receipt Emit(Receipt r, bool remember)
    {
        if (remember)
            Journal.Remember(r);
        gateway.SendReceipt(r);
        if (Received != null)
            Received(r);
        return r;
    }
    public Receipt Submit(ActionRequest r)
    {
        if (r == null || !ValidId(r.receiptId) || !ValidId(r.sessionId))
            return Emit(new Receipt("invalid", "rejected", "invalid_identity"), false);
        var known = Journal.Find(r.receiptId);
        if (known != null)
            return Emit(known, false);
        if (Expired(r))
        {
            // Count a fresh expired gameplay request only when it belongs to this active run.
            RecordSent(r);
            return Emit(new Receipt(r.receiptId, "rejected", "request_expired"), true);
        }
        if (r.eventKind == "join")
        {
            if (!gateway.Ready || !Actions.DemoName(r.displayName))
                return Emit(new Receipt(r.receiptId, "rejected", "invalid_join"), true);
            game.AnnounceViewer(r.displayName, null);
            return Emit(new Receipt(r.receiptId, "executed"), true);
        }
        if (!string.IsNullOrEmpty(r.eventKind) && r.eventKind != "action")
            return Emit(new Receipt(r.receiptId, "rejected", "unknown_event"), true);
        foreach (var p in queue)
            if (p.receiptId == r.receiptId)
                return Emit(new Receipt(r.receiptId, "queued"), false);
        int k = -1;
        for (int i = 0; i < Actions.Count; i++)
            if (r.actionId == Actions.Scoped(game.RunId, (ActionKind)i))
                k = i;
        if (k < 0)
            return Emit(new Receipt(r.receiptId, "rejected", "stale_or_unknown_action"), true);
        r.kind = (ActionKind)k;
        if (game.Active) game.RunInteractions.Sent(r.kind, r.displayName);
        string reason = Availability(r.kind);
        if (reason != null)
            return Emit(new Receipt(r.receiptId, "rejected", reason), true);
        double at;
        if (sessions.TryGetValue(r.sessionId, out at) && game.Clock < at)
            return Emit(new Receipt(r.receiptId, "rejected", "session_cooldown"), true);
        foreach (var p in queue)
            if (p.sessionId == r.sessionId)
                return Emit(new Receipt(r.receiptId, "rejected", "session_pending"), true);
        if (queue.Count >= game.B.queueCap)
            return Emit(new Receipt(r.receiptId, "rejected", "queue_full"), true);
        // Own the queued envelope: caller mutations cannot alter an accepted request.
        queue.Add(new ActionRequest { receiptId = r.receiptId, sessionId = r.sessionId, actionId = r.actionId,
                                      displayName = Actions.ViewerName(r.displayName), expiresAtUnixMs = r.expiresAtUnixMs,
                                      kind = r.kind, queuedAt = game.Clock });
        reserved.Add(r.kind);
        return Emit(new Receipt(r.receiptId, "queued"), false);
    }
    public string Availability(ActionKind k, bool executing = false)
    {
        if (!gateway.Ready)
            return "gateway_unavailable";
        if (!game.Active)
            return "phase_unavailable";
        if (!executing && reserved.Contains(k))
            return "action_pending";
        if (game.Clock < cooldowns[(int)k])
            return "shared_cooldown";
        if (game.Phase != Phase.Boss && Actions.Hinder(k) &&
            (game.HP < game.MaxHP * .25f || game.Time >= game.B.maxSeconds - 10))
            return "guardian_protected";
        if (k == ActionKind.HealPod)
        {
            if (game.HP >= game.MaxHP)
                return "full_health";
            if (game.Pickups.Exists(p => p.heal))
                return "pod_active";
        }
        if (k == ActionKind.TealCelebration && game.Celebration > 0)
            return "effect_active";
        if (k == ActionKind.LaserMode && game.Laser > 0)
            return "effect_active";
        if (k == ActionKind.EnemyGlow && game.EnemyGlow > 0)
            return "effect_active";
        if (k == ActionKind.ReverseFacing && game.ReverseFacing > 0)
            return "effect_active";
        if (k == ActionKind.EnemySpin && game.EnemySpin > 0)
            return "effect_active";
        if (k == ActionKind.PetalHeart && game.PetalHeart > 0)
            return "effect_active";
        if (k == ActionKind.MuddyFeet && game.MuddyFeet > 0)
            return "effect_active";
        if (k == ActionKind.RainbowShots && game.RainbowShots > 0)
            return "effect_active";
        if (k == ActionKind.ForceWave && (game.ForceWavePulsesRemaining > 0 || game.ForceWavePulse > 0))
            return "effect_active";
        if (k == ActionKind.StarStrike)
        {
            if (!game.Enemies.Exists(e => e.hp > 0 && e.dissolve <= 0))
                return "no_target";
            if (game.Hazards.Exists(h => h.star))
                return "effect_active";
        }
        if (k == ActionKind.SprigSwarm && game.EnemyCount > game.Cap - 3)
            return "enemy_capacity";
        if (k == ActionKind.OrangeChampion)
        {
            if (game.EnemyCount >= game.Cap) return "enemy_capacity";
            if (game.Enemies.Exists(e => e.champion && e.hp > 0)) return "champion_active";
        }
        if (k == ActionKind.MiniBoss)
        {
            if (game.Enemies.Exists(e => e.miniBoss && e.hp > 0)) return "mini_boss_active";
            if (game.EnemyCount >= game.Cap) return "enemy_capacity";
        }
        if (k == ActionKind.EnemyRush && game.RegularCount == 0)
            return "no_regular_enemy";
        if (k == ActionKind.ThornPatch || k == ActionKind.EnemyRush)
        {
            if (game.Rush > 0 || game.Hazards.Exists(h => h.viewer))
                return "mischief_active";
            if (!executing &&
                (reserved.Contains(ActionKind.ThornPatch) || reserved.Contains(ActionKind.EnemyRush)))
                return "mischief_pending";
        }
        return null;
    }
    public float Cooldown(ActionKind k)
    {
        return (float)Math.Max(0, cooldowns[(int)k] - game.Clock);
    }
    static bool Expired(ActionRequest r)
    {
        return r.expiresAtUnixMs > 0 && DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() >= r.expiresAtUnixMs;
    }
    void RecordSent(ActionRequest request)
    {
        if (!game.Active || (!string.IsNullOrEmpty(request.eventKind) && request.eventKind != "action")) return;
        // Expired resubmissions of an already queued envelope must not count twice.
        foreach (var pending in queue)
            if (pending.receiptId == request.receiptId) return;
        for (int i = 0; i < Actions.Count; i++)
            if (request.actionId == Actions.Scoped(game.RunId, (ActionKind)i))
            {
                game.RunInteractions.Sent((ActionKind)i, request.displayName);
                return;
            }
    }
    public void Tick()
    {
        gateway.Tick();
        if (!game.Active && queue.Count > 0)
        {
            RejectQueue("phase_ended");
            return;
        }
        for (int i = queue.Count - 1; i >= 0; i--)
            if (game.Clock - queue[i].queuedAt >= game.B.queueExpiry)
            {
                var old = queue[i];
                queue.RemoveAt(i);
                reserved.Remove(old.kind);
                Emit(new Receipt(old.receiptId, "rejected", "queue_expired"), true);
            }
        if (queue.Count == 0 || game.Clock < dispatchAt)
            return;
        var r = queue[0];
        queue.RemoveAt(0);
        reserved.Remove(r.kind);
        string reason = Expired(r) ? "request_expired" :
            r.actionId != Actions.Scoped(game.RunId, r.kind) ? "stale_run" : Availability(r.kind, true);
        double sessionAt;
        if (reason == null && sessions.TryGetValue(r.sessionId, out sessionAt) && game.Clock < sessionAt)
            reason = "session_cooldown";
        if (reason != null)
        {
            Emit(new Receipt(r.receiptId, "rejected", reason), true);
            return;
        }
        game.Execute(r.kind, r.displayName);
        game.RunInteractions.Played(r.kind);
        cooldowns[(int)r.kind] = game.Clock + Actions.Cooldown(game.B, r.kind);
        sessions[r.sessionId] = game.Clock + game.B.sessionCooldown;
        dispatchAt = game.Clock + game.B.dispatchInterval;
        Emit(new Receipt(r.receiptId, "executed"), true);
        // Only recent cooldowns are needed; keep anonymous visitor state bounded.
        if (sessions.Count > 256)
        {
            var old = new List<string>();
            foreach (var s in sessions)
                if (s.Value <= game.Clock)
                    old.Add(s.Key);
            foreach (var key in old)
                sessions.Remove(key);
        }
    }
    void RejectQueue(string reason)
    {
        foreach (var r in queue)
            Emit(new Receipt(r.receiptId, "rejected", reason), true);
        queue.Clear();
        reserved.Clear();
    }
    void Reset()
    {
        RejectQueue("run_reset");
        sessions.Clear();
        Array.Clear(cooldowns, 0, cooldowns.Length);
        dispatchAt = game.Clock;
    }
    public void Dispose()
    {
        gateway.Requested -= SubmitEvent;
        game.Resetting -= Reset;
        gateway.Dispose();
    }
}
}
