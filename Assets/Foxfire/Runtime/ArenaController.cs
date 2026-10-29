using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
namespace Foxfire
{
public sealed class ArenaController : MonoBehaviour
{
    public BalanceAsset balance;
    public Simulation Game { get; private set; }
    public InteractionDispatcher Dispatcher { get; private set; }
    internal WardenAnimation WardenPresentation { get { return wardenAnimation; } }
    LocalSimulatorGateway simulator;
    IViewerInteractionGateway gateway;
    readonly Dictionary<string, Sprite> art = new Dictionary<string, Sprite>();
    readonly List<SpriteRenderer> pool = new List<SpriteRenderer>();
    int used, cosmeticUsed;
    FoxHeroFrames foxHero;
    WardenFrames wardenFrames;
    EnemyModelVisuals enemyModels;
    ForestEnvironment forestEnvironment;
    readonly WardenAnimation wardenAnimation = new WardenAnimation();
    CombatPreview combatPreviewFixture;
    HudPreview hudPreviewFixture;
    ForestHud forestHud;
    V previousFox;
    double previousFoxClock;
    float accumulator, publishTimer, heartbeatTimer;
    long frame;
    bool showSimulator = false;
    bool menuOpen = true;
    bool hasStarted;
    StartMenuView startMenu;
    bool captureActions;
    float speed = 1;
    GUIStyle label, small, title, button, headlineStyle, actionSmall, nameplate, viewerLabel;
    GUIStyle menuTitle, menuEyebrow, menuBody, menuMuted, menuButton, menuKey;
    static readonly string[] EnemyArt = { "sprig", "boar", "wisp", "boss" };
    Texture2D white;
    string lastStatus = "Choose an action to send a local test event";
    string output;
    readonly Queue<float> frameSamples = new Queue<float>();
    WorkloadProfile profile;
    string profileScenario;
    float profileVisitorTimer;
    int profileReceipt;
    float profileRequestsPerSecond = 10;
    float hudRefreshAt;
    Phase hudPhase;
    int hudRun;
    string hpText, levelText, footerText, speedText, resultText, countdownText;
    void RefreshHud()
    {
        if(hpText!=null && Time.unscaledTime<hudRefreshAt && hudPhase==Game.Phase && hudRun==Game.RunNumber)
            return;
        hudRefreshAt=Time.unscaledTime+.1f; hudPhase=Game.Phase; hudRun=Game.RunNumber;
        hpText=$"{Mathf.CeilToInt(Game.HP)} / {Game.MaxHP:0}";
        levelText="LV "+(Game.Level+1);
        if(showSimulator)
        {
            footerText=Game.Intent+"   ·   "+Game.Kills+" defeated";
            speedText="SIM  /  "+speed.ToString("0")+"×";
        }
        if(Game.Phase==Phase.Intro || Game.Phase==Phase.Results)
        {
            resultText=Game.Phase==Phase.Intro?"One fox. An enchanted forest. A little outside influence.":
                $"{Game.Kills} defeated     {Game.Level} upgrades     {Game.ExecutedActions} actions";
            countdownText=Game.Phase==Phase.Intro?
                $"Autonomous run begins in {Mathf.CeilToInt(Game.B.introSeconds-Game.StateTime)}…":
                $"Next adventure begins in {Mathf.CeilToInt(Game.ResultsDuration-Game.StateTime)}s";
        }
    }
    static readonly Color Teal = PlaceholderArt.Hex("00bebe"), Cream = PlaceholderArt.Hex("ffe7be"),
                          Plum = PlaceholderArt.Hex("b06eb3"), Dark = PlaceholderArt.Hex("10191c"),
                          Coral = PlaceholderArt.Hex("ef6b68"), HotPink = PlaceholderArt.Hex("ff3f9f");
    const int MaxCosmeticSprites = 256;
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        if (FindObjectOfType<ArenaController>() == null)
            new GameObject("Foxfire Arena").AddComponent<ArenaController>();
    }
    void Awake()
    {
        Application.targetFrameRate = 30;
        Application.runInBackground = true;
        QualitySettings.vSyncCount = 0;
        Screen.SetResolution(1280, 720, false);
        balance = balance != null ? balance : Resources.Load<BalanceAsset>("DefaultBalance");
        string[] args = Environment.GetCommandLineArgs();
        captureActions = Debug.isDebugBuild && Array.IndexOf(args, "-capture-actions") >= 0;
        double profileSeconds = 0;
        for(int i=0;i<args.Length-1;i++)
        {
            if(args[i]=="-profile-scenario") profileScenario=args[i+1];
            if(args[i]=="-profile-seconds") double.TryParse(args[i+1],out profileSeconds);
            if(args[i]=="-profile-requests-per-second")
            {
                if(!float.TryParse(args[i+1],System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture,out profileRequestsPerSecond) ||
                    float.IsNaN(profileRequestsPerSecond) || profileRequestsPerSecond<0 || profileRequestsPerSecond>1920)
                    throw new ArgumentException("Profile request rate must be between 0 and 1920 per second");
            }
        }
        var values = balance != null ? balance.values : new Balance();
        bool wardenPreview = Debug.isDebugBuild && Array.IndexOf(args, "-warden-preview") >= 0;
        bool enemyPreview = Debug.isDebugBuild && Array.IndexOf(args, "-enemy-preview") >= 0;
        bool combatPreview = Debug.isDebugBuild && Array.IndexOf(args, "-combat-preview") >= 0;
        bool actionPreview = Debug.isDebugBuild && Array.IndexOf(args, "-action-preview") >= 0;
        bool hudPreview = Debug.isDebugBuild && Array.IndexOf(args, "-hud-preview") >= 0;
        if (enemyPreview)
        {
            values = JsonUtility.FromJson<Balance>(JsonUtility.ToJson(values));
            values.introSeconds = 0; values.bossAt = 100000; values.maxSeconds = 100001;
            values.foxHP = 100000; values.boltDamage = 0;
        }
        if (combatPreview || actionPreview || hudPreview)
        {
            values = JsonUtility.FromJson<Balance>(JsonUtility.ToJson(values));
            values.introSeconds = 0; values.bossAt = 100000; values.maxSeconds = 100001;
            values.boltDamage = 0;
            values.phaseCaps = new int[values.phaseCaps != null ? values.phaseCaps.Length : 4];
        }
        if (wardenPreview)
        {
            values = JsonUtility.FromJson<Balance>(JsonUtility.ToJson(values));
            // The preview must reach stage 3 before the deterministic death capture. It is an
            // isolated in-memory balance and never changes the release asset.
            values.introSeconds = 0; values.bossAt = .01f; values.maxSeconds = 180;
            values.resultsSeconds = 30;
            values.foxHP = 100000; values.bossHP = 1000000; values.boltDamage = 0;
        }
        if(profileScenario!=null)
        {
            if(profileScenario!="idle" && profileScenario!="normal" && profileScenario!="saturated")
                throw new ArgumentException("Profile scenario must be idle, normal or saturated");
            values=JsonUtility.FromJson<Balance>(JsonUtility.ToJson(values));
            if(profileScenario=="idle") values.introSeconds=1000000;
            if(profileScenario=="saturated")
            {
                values.introSeconds=0; values.bossAt=1000000; values.maxSeconds=1000001;
                values.foxHP=1000000; values.phaseCaps=new[]{40,40,40,40};
            }
        }
        values = JsonUtility.FromJson<Balance>(JsonUtility.ToJson(values));
        values.introSeconds = 0;
        Game = new Simulation(values, 1);
        Game.PlayerControlled = true;
        startMenu = FindObjectOfType<StartMenuView>(true);
        if (startMenu != null) startMenu.SetState(this, true, false, Outcome.None);
        var hypelineManager = FindObjectOfType<MuxyGatewayManager>();
        if (hypelineManager != null)
            gateway = new HypelineGateway(hypelineManager, () => !menuOpen);
        else
            gateway = simulator = new LocalSimulatorGateway();
        output = Environment.GetEnvironmentVariable("FOXFIRE_STATE_DIR") ??
                 Path.Combine(Application.persistentDataPath, "operations");
        Directory.CreateDirectory(output);
        if(profileScenario!=null) profile=new WorkloadProfile(output,profileScenario,profileSeconds);
        Dispatcher = new InteractionDispatcher(
            Game, gateway, new ReceiptJournal(Game.B.journalCap, Path.Combine(output, "receipts.tsv")));
        gateway.Publish(Game, Dispatcher);
        Dispatcher.Received += r => {
            lastStatus = r.status.ToUpperInvariant() +
                (string.IsNullOrEmpty(r.reason) ? "" : ": " + r.reason.Replace('_', ' '));
            if (captureActions && r.status == "executed") StartCoroutine(CaptureAction(r.id));
        };
        // Development evidence uses actual game renders, never desktop capture or synthetic video.
        if (Debug.isDebugBuild && Array.IndexOf(args, "-capture-stream") >= 0)
            StartCoroutine(CaptureStream());
        Game.Finished += RecordRun;
        var camera = Camera.main;
        if (camera == null)
        {
            var go = new GameObject("Arena Camera");
            camera = go.AddComponent<Camera>();
            go.tag = "MainCamera";
        }
        camera.orthographic = true;
        camera.orthographicSize = 9;
        camera.transform.position = new Vector3(0, 0, -10);
        camera.backgroundColor = Dark;
        camera.clearFlags = CameraClearFlags.SolidColor;
        foreach (string key in new[] { "fox", "sprig", "boar", "orange_boar", "wisp", "boss", "tree", "shard", "pod", "ring",
                                       "diamond", "square", "circle", "rock", "log", "eyes" })
            art[key] = PlaceholderArt.Make(key);
        foxHero = new FoxHeroFrames();
        wardenFrames = new WardenFrames();
        enemyModels = new EnemyModelVisuals();
        previousFox = Game.Fox;
        previousFoxClock = Game.Clock;
        white = Texture2D.whiteTexture;
        BuildEnvironment();
        if (wardenPreview) gameObject.AddComponent<WardenPreview>().arena = this;
        if (enemyPreview) gameObject.AddComponent<EnemyPreview>().arena = this;
        if (combatPreview) combatPreviewFixture = gameObject.AddComponent<CombatPreview>();
        if (combatPreview) combatPreviewFixture.arena = this;
        if (actionPreview) gameObject.AddComponent<ActionPreview>().arena = this;
        if (hudPreview) hudPreviewFixture = gameObject.AddComponent<HudPreview>();
        if (hudPreview) hudPreviewFixture.arena = this;
        for (int i = 0; i < args.Length; i++)
        {
            if (args[i] == "-capture" && i + 1 < args.Length)
                StartCoroutine(Capture(args[i + 1]));
            if (gateway.IsSimulation && args[i] == "-sim-speed" && i + 1 < args.Length)
                float.TryParse(args[i + 1], out speed);
            if (args[i] == "-hide-simulator")
                showSimulator = false;
            if (gateway.IsSimulation && args[i] == "-show-simulator")
                showSimulator = true;
        }
        if (combatPreview || actionPreview || hudPreview)
            showSimulator = false;
    }
    IEnumerator Capture(string path)
    {
        yield return new WaitForSecondsRealtime(12);
        ScreenCapture.CaptureScreenshot(path);
        yield return new WaitForSecondsRealtime(2);
    }
    IEnumerator CaptureStream()
    {
        string path = Path.Combine(output, "stream-frame.png");
        var interval = new WaitForSecondsRealtime(.2f);
        var endOfFrame = new WaitForEndOfFrame();
        while (true)
        {
            yield return endOfFrame;
            var frameTexture = ScreenCapture.CaptureScreenshotAsTexture();
            File.WriteAllBytes(path + ".tmp", frameTexture.EncodeToPNG());
            Destroy(frameTexture);
            if (File.Exists(path)) File.Replace(path + ".tmp", path, null);
            else File.Move(path + ".tmp", path);
            yield return interval;
        }
    }
    IEnumerator CaptureAction(string receiptId)
    {
        // Development-only evidence captures the effect after its portal tell, without screen access.
        yield return new WaitForSecondsRealtime(1);
        ScreenCapture.CaptureScreenshot(Path.Combine(output, "action-" + receiptId + ".png"));
    }
    void RecordRun(RunResult r)
    {
        string path = Path.Combine(output, "runs.csv");
        if (File.Exists(path) && new FileInfo(path).Length > 2 * 1024 * 1024)
        {
            File.Copy(path, path + ".previous", true);
            File.Delete(path);
        }
        File.AppendAllText(
            path,
            $"{DateTime.UtcNow:O},{r.run},{r.seed},{r.outcome},{r.seconds:F2},{r.kills},{r.upgrades},{r.actions}\n");
    }
    void Update()
    {
        if (forestEnvironment != null)
            forestEnvironment.Tick(Time.unscaledDeltaTime);
        if (hasStarted && Input.GetKeyDown(KeyCode.Escape))
        {
            menuOpen = !menuOpen;
            if (startMenu != null) startMenu.SetState(this, menuOpen, hasStarted, Game.Outcome);
        }
        if (menuOpen)
        {
            Game.PlayerDirection = new V();
            accumulator = 0;
            publishTimer += Time.unscaledDeltaTime;
            if (publishTimer >= .5f) { publishTimer = 0; gateway.Publish(Game, Dispatcher); }
            RenderWorld();
            RefreshHud();
            return;
        }
        Game.PlayerDirection = new V(
            (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow) ? 1 : 0) -
            (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow) ? 1 : 0),
            (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow) ? 1 : 0) -
            (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow) ? 1 : 0));
        if (combatPreviewFixture != null || hudPreviewFixture != null)
            return;
        if(profileScenario=="saturated") WorkloadProfile.Saturate(Game);
        float dt = Mathf.Min(Time.unscaledDeltaTime, .25f);
        accumulator += dt * Mathf.Clamp(speed, 1, 20);
        int steps = 0;
        long stepStart = profile != null ? WorkloadProfile.Timestamp() : 0;
        while (accumulator >= 1f / 30 && steps++ < 180)
        {
            Game.Step(1f / 30);
            Dispatcher.Tick();
            accumulator -= 1f / 30;
        }
        double stepMs = profile != null ? WorkloadProfile.Milliseconds(stepStart) : 0;
        if(profileScenario=="saturated")
        {
            // Synthetic submissions via the actual validator/journal; no live Muxy traffic.
            // The generator has no unbounded backlog and submits at most 64 events per rendered frame.
            profileVisitorTimer=Mathf.Min(1,profileVisitorTimer+dt);
            int submitted=0;
            while(profileRequestsPerSecond>0 && profileVisitorTimer>=1/profileRequestsPerSecond && submitted++<64)
            {
                profileVisitorTimer-=1/profileRequestsPerSecond;
                simulator.Submit(Game,(ActionKind)(profileReceipt++%Actions.Count),"profile-"+profileReceipt);
            }
        }
        publishTimer += dt;
        if (publishTimer >= (gateway.IsSimulation ? 2 : .5f))
        {
            publishTimer = 0;
            gateway.Publish(Game, Dispatcher);
        }
        frame++;
        heartbeatTimer += dt;
        frameSamples.Enqueue(Time.unscaledDeltaTime * 1000);
        if (frameSamples.Count > 900)
            frameSamples.Dequeue();
        if (heartbeatTimer >= 2)
        {
            heartbeatTimer = 0;
            var samples = frameSamples.ToArray();
            Array.Sort(samples);
            float p95 = samples[Math.Min(samples.Length - 1, (int)(samples.Length * .95f))];
            File.WriteAllText(Path.Combine(output, "metrics.json"),
                              "{\"samples\":" + samples.Length + ",\"p95_frame_ms\":" +
                                  p95.ToString("F3", System.Globalization.CultureInfo.InvariantCulture) +
                                  ",\"unity_allocated_bytes\":" +
                                  UnityEngine.Profiling.Profiler.GetTotalAllocatedMemoryLong() +
                                  ",\"render_pool\":" + pool.Count + "}");
            string heartbeat =
                $"{{\"utc\":\"{DateTime.UtcNow:O}\",\"frame\":{frame},\"run\":\"{Game.RunId}\",\"phase\":\"{Game.Phase}\",\"stateSeconds\":{Game.StateTime.ToString("F2",System.Globalization.CultureInfo.InvariantCulture)},\"simulationSeconds\":{Game.Time.ToString("F2",System.Globalization.CultureInfo.InvariantCulture)},\"status\":\"{(gateway.IsSimulation ? "simulation" : gateway.Ready ? "website_connected" : "website_disconnected")}\"}}";
            string temp = Path.Combine(output, "heartbeat.tmp");
            File.WriteAllText(temp, heartbeat);
            string dest = Path.Combine(output, "heartbeat.json");
            if (File.Exists(dest))
                File.Replace(temp, dest, null);
            else
                File.Move(temp, dest);
        }
        if (Game.Phase == Phase.Results) {
            menuOpen = true; hasStarted = false;
            if (startMenu != null) startMenu.SetState(this, true, false, Game.Outcome);
        }
        if (Input.GetKeyDown(KeyCode.F3))
            ScreenCapture.CaptureScreenshot(
                Path.Combine(output, "capture-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss") + ".png"));
        if (gateway.IsSimulation && Input.GetKeyDown(KeyCode.F1))
            showSimulator = !showSimulator;
        var cam = Camera.main;
        cam.orthographicSize = showSimulator ? 11.4f : 9;
        cam.transform.position = new Vector3(0, showSimulator ? -1.2f : 0, -10);
        RenderWorld();
        RefreshHud();
        if(profile != null) profile.Frame(Time.unscaledDeltaTime*1000,stepMs,steps,Game,Dispatcher.QueueCount,profileReceipt);
    }
    void OnDestroy()
    {
        if (forestHud != null) forestHud.Dispose();
        if (foxHero != null) foxHero.Dispose();
        if (wardenFrames != null) wardenFrames.Dispose();
        if (enemyModels != null) enemyModels.Dispose();
        if (forestEnvironment != null) forestEnvironment.Dispose();
        if(profile != null) profile.Dispose();
        if (Dispatcher != null)
            Dispatcher.Dispose();
        // Procedurally created assets are not scene objects; explicitly release their native storage.
        foreach (var sprite in art.Values)
        {
            if (sprite != null)
            {
                Destroy(sprite.texture);
                Destroy(sprite);
            }
        }
        art.Clear();
    }
    SpriteRenderer Sprite(string key, Vector2 p, Vector2 scale, Color color, int order,
                          Transform parent = null)
    {
        var go = new GameObject(key);
        if (parent != null)
            go.transform.SetParent(parent);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = art[key];
        sr.color = color;
        sr.sortingOrder = order;
        go.transform.position = p;
        go.transform.localScale = scale;
        return sr;
    }
    void BuildEnvironment()
    {
        var root = new GameObject("Arena · non-colliding environment").transform;
        Sprite("square", Vector2.zero, new Vector2(16, 9), PlaceholderArt.Hex("172c2c"), -1000, root);
        Sprite("square", Vector2.zero, new Vector2(14, 7), PlaceholderArt.Hex("203834"), -999, root);
        var random = new System.Random(901);
        for (int i = 0; i < 230; i++)
        {
            float x = (float)random.NextDouble() * 28 - 14, y = (float)random.NextDouble() * 14 - 7;
            float size = .025f + (float)random.NextDouble() * .065f;
            Sprite(i % 4 == 0 ? "diamond" : "circle", new Vector2(x, y), new Vector2(size, size * .5f),
                   i % 4 == 0 ? new Color(.1f, .45f, .39f, .25f) : new Color(.4f, .5f, .38f, .12f), -998,
                   root);
        }
        for (int i = 0; i < 3; i++)
            Sprite("ring", Vector2.zero, Vector2.one * (1.6f + i * .55f), new Color(.3f, .55f, .48f, .18f),
                   -997, root);
        for (int i = 0; i < 12; i++)
        {
            float a = i * Mathf.PI / 6;
            var sr = Sprite("square", new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 2.3f, new Vector2(.08f, .23f),
                            new Color(.26f, .54f, .47f, .4f), -996, root);
            sr.transform.rotation = Quaternion.Euler(0, 0, a * Mathf.Rad2Deg);
        }
        Sprite("diamond", new Vector2(0, .4f), new Vector2(.4f, .8f), new Color(0, .7f, .7f, .35f), -994,
               root);
        Sprite("diamond", new Vector2(0, .4f), new Vector2(.19f, .5f), new Color(.2f, .9f, .85f, .35f), -993,
               root);
        forestEnvironment = ForestEnvironment.Create(key => art[key], root);
        for (int i = 0; i < 4; i++)
        {
            Vector2 p = new Vector2(i % 2 == 0 ? -12.8f : 12.8f, i < 2 ? -5.8f : 5.8f);
            Sprite("ring", p, Vector2.one * .7f, new Color(.55f, .25f, .57f, .6f), -500, root);
        }
    }
    void Draw(string key, V p, float sx, float sy, Color color, int layer = 0, float rotation = 0)
    {
        DrawSprite(art[key], p, sx, sy, color, layer, rotation);
    }
    void DrawSprite(Sprite sprite, V p, float sx, float sy, Color color, int layer = 0, float rotation = 0)
    {
        SpriteRenderer sr;
        if (used >= pool.Count)
        {
            sr = Sprite("square", Vector2.zero, Vector2.one, Color.white, 0, transform);
            pool.Add(sr);
        }
        else
            sr = pool[used];
        used++;
        sr.enabled = true;
        sr.sprite = sprite;
        sr.color = color;
        sr.sortingOrder = layer == 0 ? 100 - (int)(p.y * 10) : layer;
        sr.transform.position = new Vector3(p.x, p.y, 0);
        sr.transform.localScale = new Vector3(sx, sy, 1);
        sr.transform.rotation = Quaternion.Euler(0, 0, rotation);
    }
    void CosmeticDraw(string key, V p, float sx, float sy, Color color, int layer = 0, float rotation = 0)
    {
        if (cosmeticUsed >= MaxCosmeticSprites)
            return;
        cosmeticUsed++;
        Draw(key, p, sx, sy, color, layer, rotation);
    }
    void CosmeticLine(V a, V b, float width, Color color, int layer = 200)
    {
        if (cosmeticUsed >= MaxCosmeticSprites)
            return;
        cosmeticUsed++;
        Line(a, b, width, color, layer);
    }
    void Line(V a, V b, float width, Color c, int layer = 200)
    {
        V mid = (a + b) * .5f;
        Draw("square", mid, V.Dist(a, b) * .5f, width, c, layer,
             Mathf.Atan2(b.y - a.y, b.x - a.x) * Mathf.Rad2Deg);
    }
    void Arc(V center, float radius, float width, float start, float sweep, Color color, int layer)
    {
        if (radius <= .001f || Mathf.Abs(sweep) <= .001f)
            return;
        int segments = Mathf.Clamp(Mathf.CeilToInt(Mathf.Abs(sweep) * Mathf.Max(radius, .5f) / .28f), 8, 64);
        V previous = center + V.Heading(start) * radius;
        for (int i = 1; i <= segments; i++)
        {
            V next = center + V.Heading(start + sweep * i / segments) * radius;
            Line(previous, next, Mathf.Max(.025f, width), color, layer);
            previous = next;
        }
    }
    static Color WardenStageColor(int stage)
    {
        switch (stage)
        {
            case 3: return PlaceholderArt.Hex("ef6b68");
            case 2: return PlaceholderArt.Hex("ff963f");
            default: return PlaceholderArt.Hex("b06eb3");
        }
    }
    static float Positive(float value, float fallback)
    {
        return value > 0 && !float.IsNaN(value) && !float.IsInfinity(value) ? value : fallback;
    }
    static V DirectionOr(V direction, V fallback)
    {
        return direction.Length > .001f ? direction.Unit : fallback.Unit;
    }
    static string EnemySpriteKey(Enemy enemy)
    {
        if (enemy.kind == EnemyKind.MiniBoss) return "boss";
        if (enemy.champion && enemy.kind == EnemyKind.Boar) return "orange_boar";
        int index = Mathf.Clamp((int)enemy.kind, 0, EnemyArt.Length - 1);
        return EnemyArt[index];
    }

    static Color Rainbow(float hue)
    {
        return Color.HSVToRGB(Mathf.Repeat(hue, 1), .82f, 1);
    }
    static V HeartPoint(float t)
    {
        // The classic parametric heart fits inside roughly one world unit. Petals are drawn
        // individually so this remains a bounded sprite effect with no particle allocations.
        float x = 1.6f * Mathf.Sin(t) * Mathf.Sin(t) * Mathf.Sin(t);
        float y = 1.6f * (13 * Mathf.Cos(t) - 5 * Mathf.Cos(2 * t) -
                          2 * Mathf.Cos(3 * t) - Mathf.Cos(4 * t)) / 16;
        return new V(x, y);
    }
    void DrawEnemyGlow(Enemy enemy, float now, float timer)
    {
        if (timer <= 0 || enemy.dissolve > 0)
            return;
        float pulse = .5f + .5f * Mathf.Sin(now * 5.2f + enemy.id * .47f);
        Color glow = Color.HSVToRGB(Mathf.Repeat(.48f + enemy.id * .071f, 1), .75f, 1);
        glow.a = .13f + pulse * .12f;
        float radius = enemy.kind == EnemyKind.Warden ? 1.55f : enemy.kind == EnemyKind.MiniBoss ? 1.2f :
                       enemy.champion ? .98f : enemy.kind == EnemyKind.Boar ? .78f : .65f;
        CosmeticDraw("circle", enemy.pos + new V(0, .12f), radius * (1.15f + pulse * .12f),
                     radius * (.7f + pulse * .08f), glow, 2);
        Color edge = new Color(glow.r, glow.g, glow.b, .48f + pulse * .24f);
        CosmeticDraw("ring", enemy.pos + new V(0, .12f), radius * (1.12f + pulse * .15f),
                     radius * (.72f + pulse * .1f), edge, 3);
    }
    void DrawPetalHeart(V center, float now, float timer)
    {
        if (timer <= 0) return;
        float progress = Mathf.Clamp01(1 - timer / 6f);
        float breathe = .93f + .07f * Mathf.Sin(now * 4.5f);
        for (int i = 0; i < 14; i++)
        {
            float t = i * Mathf.PI * 2 / 14;
            V point = center + HeartPoint(t) * breathe;
            V next = center + HeartPoint(t + .05f) * breathe;
            float angle = Mathf.Atan2(next.y - point.y, next.x - point.x) * Mathf.Rad2Deg + 90;
            Color petal = new Color(1, .12f + i * .012f, .26f + i * .018f,
                                    .68f + .16f * Mathf.Sin(now * 3 + i));
            CosmeticDraw("diamond", point, .11f + progress * .025f, .2f + progress * .035f,
                         petal, 11, angle);
        }
        CosmeticDraw("circle", center + new V(0, .02f), .54f, .34f,
                     new Color(1, .05f, .16f, .1f + .04f * Mathf.Sin(now * 3)), 8);
        CosmeticDraw("ring", center + new V(0, .02f), .62f, .42f,
                     new Color(1, .18f, .3f, .38f + .16f * Mathf.Sin(now * 4)), 9);
    }
    void DrawMuddyFeet(float now, float timer)
    {
        if (timer <= 0) return;
        V heading = DirectionOr(Game.Direction, new V(1, 0));
        V side = new V(-heading.y, heading.x);
        float pulse = .8f + .2f * Mathf.Sin(now * 2.5f);
        Color mud = new Color(.24f, .12f, .07f, .28f * pulse);
        CosmeticDraw("circle", Game.Fox - heading * .2f - side * .17f + new V(0, -.35f),
                     .16f, .07f, mud, 7, now * 12);
        CosmeticDraw("circle", Game.Fox - heading * .2f + side * .17f + new V(0, -.35f),
                     .16f, .07f, mud, 7, -now * 12);
        CosmeticDraw("circle", Game.Fox - heading * .42f + new V(0, -.36f),
                     .09f, .045f, new Color(.3f, .16f, .08f, .22f * pulse), 7);
    }
    void DrawForceWave(V center, float timer)
    {
        if (timer <= 0) return;
        float progress = 1 - Mathf.Clamp01(timer / .35f);
        float radius = .35f + progress * 4.8f;
        Color wave = new Color(.98f, .62f, .24f, .78f - progress * .6f);
        CosmeticDraw("ring", center, radius, radius, wave, 205);
    }
    void DrawBoarPresentation(Enemy boar, float now)
    {
        bool telling = boar.tell > 0;
        bool charging = boar.charge > 0;
        if (!telling && !charging)
            return;

        V fallback = DirectionOr(Game.Fox - boar.pos, new V(1, 0));
        V aim = telling && boar.tell > .25f ? fallback : DirectionOr(boar.dir, fallback);
        if (telling)
        {
            float targetDistance = Mathf.Max(.9f, V.Dist(boar.pos, Game.Fox));
            float laneLength = Mathf.Min(6.4f, targetDistance);
            V target = boar.pos + aim * laneLength;
            // Aim warnings are gameplay information and must survive a full dust budget.
            Line(boar.pos + new V(0, .03f), target, .055f, new Color(1, .36f, .38f, .5f), 4);
            for (int i = 0; i < 4; i++)
            {
                float along = .18f + i * .22f;
                V p = boar.pos + aim * (laneLength * along) + V.Heading(now * 1.7f + i * 2.1f) * (.05f + i * .025f);
                CosmeticDraw("circle", p, .055f + i * .012f, .035f + i * .01f,
                             new Color(1, .68f, .38f, .4f - i * .055f), 5, now * 35 + i * 17);
            }
            for (int i = 0; i < 3; i++)
            {
                float angle = now * 1.35f + i * 2.1f;
                CosmeticDraw("circle", boar.pos + V.Heading(angle) * (.24f + i * .09f),
                             .25f + i * .035f, .15f + i * .025f,
                             new Color(1, .7f, .4f, .42f - i * .06f), 6, angle * Mathf.Rad2Deg);
                CosmeticDraw("circle", Game.Fox + V.Heading(angle + .8f) * (.2f + i * .07f),
                             .22f + i * .03f, .14f + i * .02f,
                             new Color(1, .7f, .4f, .36f - i * .05f), 6, angle * Mathf.Rad2Deg + 25);
            }
            if (boar.tell <= .25f)
            {
                Draw("ring", target, .3f, .3f, new Color(1, .25f, .55f, .8f), 7);
                CosmeticDraw("diamond", Game.Fox + aim * .34f, .09f, .16f,
                             new Color(1, .38f, .65f, .85f), 7, Mathf.Atan2(aim.y, aim.x) * Mathf.Rad2Deg);
            }
        }
        if (charging)
        {
            V direction = DirectionOr(boar.dir, fallback);
            float duration = Positive(Game.B.chargeDuration, 1);
            float progress = 1 - Mathf.Clamp01(boar.charge / duration);
            float trailLength = Mathf.Clamp(1.1f + duration * .7f, 1.1f, 2.4f);
            for (int i = 0; i < 5; i++)
            {
                float distance = .22f + i * trailLength / 5;
                float alpha = .6f * (1 - i / 6f) * (.78f + progress * .22f);
                V p = boar.pos - direction * distance + V.Heading(now * 2.2f + i * 1.9f) * (.08f + i * .025f);
                CosmeticDraw("circle", p, .21f + i * .03f, .13f + i * .02f,
                             new Color(1, .67f, .36f, alpha), 5, now * 28 + i * 23);
            }
            CosmeticLine(boar.pos - direction * trailLength, boar.pos - direction * .12f, .075f,
                         new Color(1, .42f, .32f, .26f + progress * .18f), 4);
            Draw("ring", boar.pos, .75f + progress * .12f, .75f + progress * .12f,
                         new Color(1, .42f, .35f, .62f), 5);
        }
    }
    void DrawShotWindup(Enemy enemy, float now)
    {
        if (enemy.shotTell <= 0)
            return;
        float progress = 1 - Mathf.Clamp01(enemy.shotTell / EnemyCombat.ShotTell(Game.B, enemy.kind));
        V direction = DirectionOr(enemy.dir, DirectionOr(Game.Fox - enemy.pos, new V(1, 0)));
        Color color = enemy.kind == EnemyKind.Wisp ? Plum : Coral;
        Draw("ring", enemy.pos + direction * .22f, .18f + progress * .28f, .18f + progress * .28f,
                     new Color(color.r, color.g, color.b, .42f + progress * .35f), 7);
        for (int i = 0; i < 3; i++)
        {
            float angle = now * 2.5f + i * Mathf.PI * 2 / 3;
            V p = enemy.pos + direction * (.28f + progress * .18f) + V.Heading(angle) * (.16f + progress * .08f);
            CosmeticDraw("diamond", p, .045f, .085f, new Color(color.r, color.g, color.b, .55f), 7,
                         angle * Mathf.Rad2Deg);
        }
    }
    // A stable, small bolt makes the rush readable on both baked models and fallbacks.
    // The slow pulse provides energy without a distracting rapid strobe or allocations.
    void DrawRushLightning(Enemy enemy, float now)
    {
        float pulse = .72f + .28f * (.5f + .5f * Mathf.Sin(now * 3.2f + enemy.id * .73f));
        float alpha = .72f * pulse;
        float height = enemy.champion ? 1.35f : enemy.kind == EnemyKind.Boar ? 1.15f : .98f;
        float side = (enemy.id & 1) == 0 ? -1 : 1;
        V center = enemy.pos + new V(side * .28f, height);
        V top = center + new V(-.12f, .30f);
        V bend = center + new V(.06f, .05f);
        V notch = center + new V(-.06f, -.06f);
        V bottom = center + new V(.12f, -.32f);
        Color gold = new Color(1f, .78f, .22f, alpha);
        CosmeticLine(top, bend, .075f, gold, 200);
        CosmeticLine(bend, notch, .075f, gold, 200);
        CosmeticLine(notch, bottom, .075f, gold, 200);
        CosmeticDraw("diamond", center + new V(0, -.01f), .055f, .1f,
                     new Color(0f, .95f, .86f, alpha), 200, now * 20f + enemy.id * 13f);
    }

    void DrawBrambleVines(Hazard hazard, float now)
    {
        Color vine = new Color(Coral.r, Coral.g, Coral.b, hazard.tell > 0 ? .58f : .82f);
        float radius = Mathf.Max(.55f, hazard.radius);
        for (int i = 0; i < 6; i++)
        {
            float angle = i * Mathf.PI * 2 / 6 + Mathf.Sin(now * .45f + i) * .08f;
            V heading = V.Heading(angle);
            V root = hazard.pos + heading * radius * .18f;
            V bend = hazard.pos + heading * radius * .58f + V.Heading(angle + Mathf.PI * .5f) * (.12f + i % 2 * .08f);
            V tip = hazard.pos + heading * radius * (1.02f + (i % 3) * .08f);
            CosmeticLine(root, bend, .095f, vine, -197);
            CosmeticLine(bend, tip, .06f, new Color(vine.r, vine.g, vine.b, vine.a * .82f), -197);
            CosmeticDraw("diamond", bend, .09f, .18f, vine, -196, angle * Mathf.Rad2Deg);
            CosmeticDraw("diamond", tip, .075f, .14f, new Color(vine.r, vine.g, vine.b, vine.a * .9f), -196,
                         angle * Mathf.Rad2Deg + 35);
        }
        CosmeticDraw("ring", hazard.pos, radius * .48f, radius * .48f, new Color(vine.r, vine.g, vine.b, .22f), -198);
    }
    void DrawMiniBossPresentation(Enemy mini, float now, bool reverse, float bodySpin, float fade)
    {
        // MiniBoss carries its own WardenState/WardenMove. Sample the supplied Warden clips from
        // that entity directly so a second boss never steals the final Warden's animation state.
        WardenClip clip = WardenAnimation.ClipFor(mini.wardenState, mini.wardenMove);
        double started = mini.wardenState == WardenState.Recover
            ? mini.wardenImpactClock - WardenAnimation.Impact(mini.wardenMove)
            : mini.wardenStateClock;
        float seconds = (float)Math.Max(0, Game.Clock - started);
        V direction = DirectionOr(mini.dir, Game.Fox - mini.pos);
        if (reverse) direction = direction * -1;
        int facingSign = mini.dir.x < -.15f ? -1 : 1;
        if (reverse) facingSign = -facingSign;
        Sprite frame = wardenFrames.Available ? wardenFrames.Sample(clip, seconds, direction) : null;
        if (frame != null)
            DrawSprite(frame, mini.pos, 1.05f * (clip == WardenClip.Walking || clip == WardenClip.Running ? 1 : facingSign),
                       1.05f, new Color(1, .78f, .82f, fade), 100 - (int)(mini.pos.y * 10), bodySpin);
        else
            Draw("boss", mini.pos + new V(0, .22f), 1.3f, 1.3f, new Color(1, .38f, .45f, fade), 0, bodySpin);

        Color tell = mini.wardenState == WardenState.Windup ? Coral : new Color(1, .3f, .45f, .72f);
        if (mini.wardenState == WardenState.Windup)
        {
            float duration = Mathf.Max(.01f, WardenTiming.Windup(mini.wardenMove));
            float progress = Mathf.Clamp01((float)(Game.Clock - mini.wardenStateClock) / duration);
            float radius = mini.wardenMove == WardenMove.Slash
                ? Positive(Game.B.wardenSlashRadius, 2.7f)
                : mini.wardenMove == WardenMove.Spin ? 1.9f : 2.15f;
            Draw("circle", mini.pos, radius * (.72f + progress * .28f), radius * (.42f + progress * .1f),
                 new Color(tell.r, tell.g, tell.b, .08f + progress * .07f), -149);
            Draw("ring", mini.pos, radius * (.72f + progress * .28f), radius * (.42f + progress * .1f),
                 new Color(tell.r, tell.g, tell.b, .5f + progress * .3f), -148);
            if (mini.wardenMove == WardenMove.Slash)
            {
                V attackDirection = DirectionOr(mini.dir, Game.Fox - mini.pos);
                float facing = Mathf.Atan2(attackDirection.y, attackDirection.x);
                float halfAngle = Positive(Game.B.wardenSlashHalfAngle, .85f);
                Arc(mini.pos, radius, .08f, facing - halfAngle, halfAngle * 2,
                    new Color(tell.r, tell.g, tell.b, .55f + progress * .2f), -147);
            }
        }
        else if (mini.wardenState == WardenState.Recover)
        {
            float recovery = Mathf.Max(.01f, WardenTiming.Recovery(mini.wardenMove));
            float progress = Mathf.Clamp01((float)(Game.Clock - mini.wardenImpactClock) / recovery);
            Draw("ring", mini.pos, 1.1f + progress * .65f, .58f + progress * .1f,
                 new Color(1, .25f, .38f, .5f * (1 - progress) + .08f), -148);
        }
    }
    void DrawWardenPresentation()
    {
        var boss = Game.Boss;
        if (boss == null)
            return;

        Color stageColor = WardenStageColor(boss.wardenStage);
        float pulse = .5f + .5f * Mathf.Sin((float)Game.Clock * 4);
        float stageRadius = 1.48f + Mathf.Clamp(boss.wardenStage, 1, 3) * .14f;
        Draw("ring", boss.pos, stageRadius, stageRadius * .62f,
             new Color(stageColor.r, stageColor.g, stageColor.b, .24f + pulse * .08f), -151);

        if (boss.spawnTell > 0)
        {
            float portalTell = Positive(Game.B.portalTell, .8f);
            float progress = 1 - Mathf.Clamp01(boss.spawnTell / portalTell);
            Draw("circle", boss.pos, .7f + progress * .7f, .42f + progress * .3f,
                 new Color(stageColor.r, stageColor.g, stageColor.b, .08f), -152);
            Draw("ring", boss.pos, .75f + progress * .7f, .45f + progress * .3f,
                 new Color(stageColor.r, stageColor.g, stageColor.b, .7f), -150);
        }
        else if (boss.wardenState == WardenState.Windup)
        {
            float duration = Mathf.Max(.01f, WardenTiming.Windup(boss.wardenMove));
            float progress = Mathf.Clamp01((float)(Game.Clock - boss.wardenStateClock) / duration);
            bool slam = boss.wardenMove == WardenMove.Slam;
            float radius = slam ? 2.15f : 1.65f;
            Color tell = boss.wardenMove == WardenMove.Summon ? Teal : stageColor;
            Draw("circle", boss.pos, radius * (.72f + progress * .28f), radius * (.4f + progress * .1f),
                 new Color(tell.r, tell.g, tell.b, .07f + progress * .05f), -149);
            Draw("ring", boss.pos, radius * (.72f + progress * .28f), radius * (.4f + progress * .1f),
                 new Color(tell.r, tell.g, tell.b, .5f + progress * .3f), -148);
            float facing = boss.dir.Length > .01f ? Mathf.Atan2(boss.dir.y, boss.dir.x) : 0;
            if (boss.wardenMove == WardenMove.Slash)
            {
                float slashRadius = Positive(Game.B.wardenSlashRadius, 2.7f);
                float slashHalfAngle = Positive(Game.B.wardenSlashHalfAngle, .85f);
                Arc(boss.pos, slashRadius, .08f, facing - slashHalfAngle, slashHalfAngle * 2,
                    new Color(tell.r, tell.g, tell.b, .55f + progress * .25f), -147);
                Line(boss.pos, boss.pos + V.Heading(facing - slashHalfAngle) * slashRadius, .045f,
                     new Color(tell.r, tell.g, tell.b, .35f), -147);
                Line(boss.pos, boss.pos + V.Heading(facing + slashHalfAngle) * slashRadius, .045f,
                     new Color(tell.r, tell.g, tell.b, .35f), -147);
            }
            else if (boss.wardenMove == WardenMove.Spin)
                Arc(boss.pos, 1.9f, .1f, 0, Mathf.PI * 2,
                    new Color(tell.r, tell.g, tell.b, .45f + progress * .2f), -147);
            else if (slam)
            {
                // The pre-impact warning uses the same fixed safe gap as the slam's shockwave:
                // facing plus a quarter turn, never a time-rotating decorative opening.
                float gapHalfAngle = Positive(Game.B.wardenProjectileGapHalfAngle, .42f);
                float gapAngle = facing + Mathf.PI * .5f;
                Arc(boss.pos, radius * (.48f + progress * .2f), .11f, gapAngle + gapHalfAngle,
                    Mathf.PI * 2 - gapHalfAngle * 2, new Color(tell.r, tell.g, tell.b, .45f), -147);
            }
        }
        else if (boss.wardenState == WardenState.Recover && boss.wardenMove != WardenMove.None)
        {
            float recovery = Mathf.Max(.01f, WardenTiming.Recovery(boss.wardenMove));
            float progress = Mathf.Clamp01((float)(Game.Clock - boss.wardenImpactClock) / recovery);
            Draw("ring", boss.pos, 1.15f + progress * .85f, .58f + progress * .1f,
                 new Color(stageColor.r, stageColor.g, stageColor.b, .45f * (1 - progress) + .08f), -148);
        }

        foreach (var wave in Game.Shockwaves)
        {
            float radius = Mathf.Max(0, wave.radius);
            float maxRadius = Mathf.Max(radius, Positive(wave.maxRadius, 30));
            float progress = maxRadius > .001f ? Mathf.Clamp01(radius / maxRadius) : 1;
            float gap = Mathf.Clamp(Positive(wave.gapHalfAngle, .42f), 0, Mathf.PI * .49f);
            float sweep = Mathf.PI * 2 - gap * 2;
            Color waveColor = new Color(1, .58f, .27f, .78f - progress * .36f);
            float gapAngle = float.IsNaN(wave.gapAngle) || float.IsInfinity(wave.gapAngle) ? 0 : wave.gapAngle;
            Arc(wave.pos, radius, Positive(wave.halfWidth, .42f), gapAngle + gap, sweep, waveColor, -145);
            if (wave.previousRadius > .001f && wave.previousRadius < radius)
                Arc(wave.pos, wave.previousRadius, Positive(wave.halfWidth, .42f) * .45f,
                    gapAngle + gap, sweep, new Color(1, .7f, .35f, .2f), -146);
        }
    }
    internal void RenderEnemyPreview()
    {
        if (!Debug.isDebugBuild) throw new InvalidOperationException("Enemy preview requires a development build.");
        RenderWorld(); RefreshHud();
    }
    internal void RenderCombatPreview()
    {
        if (!Debug.isDebugBuild) throw new InvalidOperationException("Combat preview requires a development build.");
        RenderWorld();
        hudRefreshAt = 0;
        RefreshHud();
    }
    void RenderWorld()
    {
        used = 0;
        cosmeticUsed = 0;
        float now = (float)Game.Clock;
        float enemyGlow = Game.EnemyGlow;
        float reverseFacing = Game.ReverseFacing;
        float enemySpin = Game.EnemySpin;
        float petalHeart = Game.PetalHeart;
        float muddyFeet = Game.MuddyFeet;
        float rainbowShots = Game.RainbowShots;
        float forceWave = Game.ForceWavePulse;
        V petalHeartPosition = Game.PetalHeartPosition;
        V forceWavePosition = Game.Fox;
        bool reverse = reverseFacing > 0;
        wardenAnimation.Update(Game);
        foreach (var h in Game.Hazards)
        {
            Color c = h.star ? Teal : Coral;
            Draw("circle", h.pos, h.radius, h.radius, new Color(c.r, c.g, c.b, h.tell > 0 ? .08f : .22f),
                 -200);
            Draw("ring", h.pos, h.radius, h.radius, c, -199);
            if (h.tell > 0)
            {
                float r =
                    h.radius * (.2f + .8f * (1 - h.tell / (h.star ? Game.B.starTell : Game.B.rootTell)));
                Draw("ring", h.pos, r, r, new Color(c.r, c.g, c.b, .6f), -198);
            }
            if (!h.star)
                DrawBrambleVines(h, now);
        }
        foreach (var p in Game.Pickups)
        {
            float sz = p.heal ? .5f : p.xp > 1 ? .2f : .13f;
            Draw(p.heal ? "pod" : "shard", p.pos + new V(0, Mathf.Sin(now * 3 + p.pos.x) * .05f), sz, sz,
                 Color.white, 5);
        }
        DrawWardenPresentation();
        foreach (var e in Game.Enemies)
        {
            if (e.hp <= 0)
                continue;
            DrawEnemyGlow(e, now, enemyGlow);
            bool primaryWarden = e.kind == EnemyKind.Warden && e == Game.Boss;
            if (primaryWarden && wardenFrames.Available)
                continue;
            if (e.spawnTell > 0)
            {
                var portalColor = e.champion ? PlaceholderArt.Hex("ff963f") : Plum;
                Draw("ring", e.pos, .65f, .4f, portalColor, 4);
                Draw("diamond", e.pos, .15f, .3f, portalColor, 5);
                continue;
            }
            float fade = e.dissolve > 0 ? e.dissolve / 2 : 1;
            float size = e.miniBoss ? 1.3f : e.champion ? 1.08f :
                e.kind == EnemyKind.Warden ? 2.1f : e.kind == EnemyKind.Boar ? .85f : .65f;
            Draw("circle", e.pos + new V(0, -.35f), size * .65f, size * .24f, new Color(0, 0, 0, .22f * fade),
                 3);
            string key = EnemySpriteKey(e);
            V modelPosition;
            float bodySpin = enemySpin > 0
                ? now * 240 + e.id * 37 : 0;
            if (e.kind == EnemyKind.MiniBoss)
            {
                DrawMiniBossPresentation(e, now, reverse, bodySpin, fade);
            }
            else
            {
                var modelFrame = enemyModels.Sample(e, Game.Clock, Game.Fox, reverse, out modelPosition);
                if (modelFrame != null)
                {
                    float modelScale = e.champion ? 1.15f :
                        e.kind == EnemyKind.Sprig || e.kind == EnemyKind.Wisp ? .8f : 1;
                    DrawSprite(modelFrame, modelPosition, modelScale, modelScale, new Color(1, 1, 1, fade),
                        100 - (int)(e.pos.y * 10), bodySpin);
                }
                else
                {
                    float bob = Mathf.Sin(now * (e.charge > 0 ? 20 : 6) + e.id) * .035f;
                    Draw(key, e.pos + new V(0, .25f + bob), size, size, new Color(1, 1, 1, fade),
                         0, bodySpin);
                }
            }
            if (e.champion) Draw("ring", e.pos, .85f, .43f, PlaceholderArt.Hex("ff963f"), 5);
            if (e.miniBoss)
            {
                Draw("ring", e.pos, 1.36f, .76f, new Color(1, .18f, .28f, .9f), 6,
                     now * 18);
                Draw("circle", e.pos + new V(0, .16f), .92f, .55f,
                     new Color(1, .08f, .22f, .09f), 5);
            }
            DrawShotWindup(e, now);
            if (e.tell > 0 || e.charge > 0)
            {
                if (e.kind == EnemyKind.Boar)
                    DrawBoarPresentation(e, now);
                else
                {
                    float lungeReach = EnemyCombat.LungeSpeed(Game.B) * EnemyCombat.LungeDuration(Game.B);
                    Line(e.pos, e.pos + e.dir * lungeReach, .035f, new Color(1, .42f, .4f, .65f), 4);
                    Draw("ring", e.pos, .75f, .75f, Coral, 5);
                }
            }
            if (Game.Rush > 0 && e.kind != EnemyKind.Warden && e.dissolve <= 0)
            {
                Draw("ring", e.pos, .8f, .4f, Plum, 5);
                DrawRushLightning(e, now);
            }
            if (e.hp < e.maxHP && e.kind != EnemyKind.Warden)
            {
                Draw("square", e.pos + new V(0, .95f), .4f, .035f, Dark, 201);
                Draw("square", e.pos + new V((e.hp / e.maxHP - 1) * .4f, .95f), .4f * e.hp / e.maxHP, .035f,
                     Plum, 202);
            }
        }
        if (enemyModels != null)
            enemyModels.EndFrame(Game.Enemies);
        if (wardenFrames.Available && wardenAnimation.Visible)
        {
            var p = wardenAnimation.Position;
            float alpha = wardenAnimation.Alpha;
            Draw("circle", p, 1.1f, .35f, new Color(0, 0, 0, .3f * alpha), 3);
            var tint = wardenAnimation.Hurt ? new Color(1, .65f, .6f, alpha) : new Color(1, 1, 1, alpha);
            float breathe = wardenAnimation.Clip == WardenClip.Idle ? Mathf.Sin(now * 2) * .008f : 0;
            bool directional = wardenAnimation.Clip == WardenClip.Walking ||
                               wardenAnimation.Clip == WardenClip.Running;
            V wardenDirection = reverse ? wardenAnimation.FacingDirection * -1 : wardenAnimation.FacingDirection;
            int wardenFacingSign = reverse ? -wardenAnimation.FacingSign : wardenAnimation.FacingSign;
            float wardenSpin = enemySpin > 0 ? now * 240 : 0;
            DrawSprite(wardenFrames.Sample(wardenAnimation.Clip, wardenAnimation.Seconds,
                       wardenDirection), p,
                       1.4f * (directional ? 1 : wardenFacingSign), 1.4f + breathe, tint, 0, wardenSpin);
        }
        bool movingFox = (Game.Phase == Phase.Combat || Game.Phase == Phase.Boss) &&
                         V.Dist(Game.Fox, previousFox) > .0001f;
        double elapsedFox = Math.Max(0, Game.Clock - previousFoxClock);
        V foxVisualDirection = reverse ? Game.Direction * -1 : Game.Direction;
        var foxFrame = foxHero.Sample(elapsedFox, foxVisualDirection, movingFox);
        previousFox = Game.Fox;
        previousFoxClock = Game.Clock;
        Color foxColor =
            Game.Immune > 0 && ((int)(now * 16) % 2 == 0) ? new Color(1, .65f, .6f) : Color.white;
        if (foxFrame != null)
        {
            Draw("circle", Game.Fox + new V(0, -.04f), .5f, .18f, new Color(0, 0, 0, .35f), 6);
            DrawSprite(foxFrame, Game.Fox, 1.3f, 1.3f, foxColor);
        }
        else
        {
            Draw("circle", Game.Fox + new V(0, -.45f), .55f, .2f, new Color(0, 0, 0, .35f), 6);
            Draw("fox", Game.Fox + new V(0, .3f), foxVisualDirection.x < -.1f ? -1 : 1, 1, foxColor);
        }
        DrawPetalHeart(petalHeartPosition, now, petalHeart);
        DrawMuddyFeet(now, muddyFeet);
        if (Game.Celebration > 0)
        {
            // Fixed sprite count, no combat RNG or per-frame particle allocations.
            float progress = (3 - Game.Celebration) / 3;
            for (int i = 0; i < 24; i++)
            {
                float phase = i * 2.39996f + now * .8f;
                float radius = .6f + ((i % 6) / 6f) * 1.1f + progress * .4f;
                V pos = Game.Fox + new V(Mathf.Cos(phase) * radius, Mathf.Sin(phase) * radius * .65f + .5f);
                float size = .045f + .045f * (.5f + .5f * Mathf.Sin(now * 8 + i));
                CosmeticDraw("diamond", pos, size, size * 1.6f,
                             new Color(0, .9f, .86f, Mathf.Min(1, Game.Celebration * 2)), 215, now * 50 + i * 30);
            }
        }
        if (Game.Upgrades[5] > 0)
        {
            V pos = Game.Fox + V.Heading(now * 3) * 1.3f;
            CosmeticDraw("circle", pos, .12f, .12f, Teal, 203);
            CosmeticDraw("ring", pos, .23f, .23f, new Color(0, .8f, .8f, .4f), 203);
        }
        if (Game.Laser > 0)
        {
            var target = Game.Target();
            if (target != null)
            {
                Line(Game.Fox, target.pos, .13f, new Color(0, .8f, .8f, .25f), 204);
                Line(Game.Fox, target.pos, .035f, Cream, 205);
                Draw("ring", target.pos, .35f, .35f, Teal, 205);
            }
        }
        foreach (var p in Game.Shots)
        {
            if (p.hostile)
                Draw("circle", p.pos, .13f, .13f, Plum, 210,
                     Mathf.Atan2(p.velocity.y, p.velocity.x) * Mathf.Rad2Deg);
            else
            {
                float shotScale = rainbowShots > 0 ? 1.42f : 1;
                Color shotColor = rainbowShots > 0
                    ? Rainbow(now * .2f + p.pos.x * .035f + p.pos.y * .025f)
                    : Cream;
                Draw("diamond", p.pos, .22f * shotScale, .065f * shotScale, shotColor, 210,
                     Mathf.Atan2(p.velocity.y, p.velocity.x) * Mathf.Rad2Deg);
                if (rainbowShots > 0)
                    CosmeticDraw("ring", p.pos, .28f * shotScale, .12f * shotScale,
                                 new Color(shotColor.r, shotColor.g, shotColor.b, .45f), 209);
            }
        }
        DrawForceWave(forceWavePosition, forceWave);
        foreach (var v in Game.Bursts)
        {
            float t = 1 - v.life / v.maxLife;
            Color c = v.kind == 2 ? Plum : Teal;
            c.a = 1 - t;
            CosmeticDraw("ring", v.pos, v.radius * (.3f + t), v.radius * (.3f + t), c, 220);
        }
        for (int i = used; i < pool.Count; i++)
            pool[i].enabled = false;
    }
    void Styles()
    {
        if (label != null)
            return;
        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        forestHud = new ForestHud(font);
        label = new GUIStyle { font = font, fontSize = 18, normal = { textColor = Cream } };
        small = new GUIStyle(label) { fontSize = 12 };
        title = new GUIStyle(label) { fontSize = 34, fontStyle = FontStyle.Bold };
        headlineStyle = new GUIStyle(title) { fontSize = 25 };
        actionSmall = new GUIStyle(small) { fontSize = 10 };
        nameplate = new GUIStyle(small) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold,
            fontSize = 14, richText = false, normal = { textColor = PlaceholderArt.Hex("ffba74") } };
        viewerLabel = new GUIStyle(nameplate) { fontSize = 18, normal = { textColor = Color.white } };
        button = new GUIStyle(GUI.skin.button) { font = font,
                                                 fontSize = 12,
                                                 alignment = TextAnchor.MiddleLeft,
                                                 padding = new RectOffset(14, 10, 8, 8),
                                                 normal = { textColor = Cream, background = white },
                                                 hover = { textColor = Color.white, background = white },
                                                 active = { textColor = Teal, background = white } };
        menuTitle = new GUIStyle(title) { fontSize = 59, normal = { textColor = Cream } };
        menuEyebrow = new GUIStyle(small) { fontSize = 14, fontStyle = FontStyle.Bold,
            normal = { textColor = PlaceholderArt.Hex("80d7c6") } };
        menuBody = new GUIStyle(label) { fontSize = 19, normal = { textColor = PlaceholderArt.Hex("e1e7d9") } };
        menuMuted = new GUIStyle(label) { fontSize = 15, normal = { textColor = PlaceholderArt.Hex("a9bdb1") } };
        menuButton = new GUIStyle(button) { alignment = TextAnchor.MiddleCenter, fontSize = 21,
            fontStyle = FontStyle.Bold, normal = { textColor = Color.white, background = white },
            hover = { textColor = Color.white, background = white }, active = { textColor = Cream, background = white } };
        menuKey = new GUIStyle(small) { fontSize = 13, fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter, normal = { textColor = Cream } };
    }
    void Box(float x, float y, float w, float h, Color c)
    {
        GUI.color = c;
        GUI.DrawTexture(new Rect(x, y, w, h), white);
        GUI.color = Color.white;
    }
    void Text(float x, float y, float w, float h, string text, GUIStyle style = null)
    {
        GUI.Label(new Rect(x, y, w, h), text, style ?? label);
    }
    void DrawMenu()
    {
        // Keep the living forest visible around a dark reading surface.
        Box(0, 0, 1280, 720, new Color(.015f, .05f, .045f, .56f));
        Box(76, 72, 1128, 576, new Color(.016f, .075f, .068f, .96f));
        Box(76, 72, 1128, 2, new Color(.72f, .58f, .35f, .8f));
        Box(76, 646, 1128, 2, new Color(.72f, .58f, .35f, .55f));
        Box(76, 72, 2, 576, new Color(.72f, .58f, .35f, .55f));
        Box(1202, 72, 2, 576, new Color(.72f, .58f, .35f, .55f));
        Box(824, 73, 1, 574, new Color(.55f, .70f, .57f, .22f));
        Box(116, 121, 35, 3, Teal);
        Text(165, 111, 570, 24, "THE FOREST NEEDS ITS GUARDIAN", menuEyebrow);
        Text(110, 160, 680, 78, "FOXFIRE ARENA", menuTitle);
        string subtitle = Game.Phase == Phase.Results
            ? (Game.Outcome == Outcome.Victory ? "The clearing is safe once more." : "The forest awaits your return.")
            : hasStarted ? "Your adventure is paused." : "An enchanted forest. One brave fox.";
        Text(116, 247, 650, 30, subtitle, menuBody);
        Text(116, 287, 650, 28, "Survive the clearing and face the Hollowroot Warden.", menuMuted);
        Box(116, 351, 660, 1, new Color(.60f, .75f, .65f, .22f));
        Text(116, 377, 200, 22, "HOW TO PLAY", menuEyebrow);
        Box(116, 418, 91, 39, new Color(.12f, .20f, .17f));
        Text(116, 418, 91, 39, "W A S D", menuKey);
        Text(222, 424, 255, 26, "Move  /  Arrow keys", menuBody);
        Box(116, 472, 91, 39, new Color(.12f, .20f, .17f));
        Text(116, 472, 91, 39, "AUTO", menuKey);
        Text(222, 478, 350, 26, "Attacks fire automatically", menuBody);
        GUI.backgroundColor = new Color(.04f, .46f, .40f);
        if (GUI.Button(new Rect(116, 550, 310, 60), hasStarted ? "RESUME ADVENTURE" : "ENTER THE FOREST", menuButton))
        {
            StartOrResume();
        }
        if (!hasStarted && GUI.Button(new Rect(453, 550, 310, 60), "AI MODE", menuButton))
            StartAiMode();
        GUI.backgroundColor = Color.white;
        if (hasStarted) Text(453, 571, 290, 25, "ESC to resume", menuMuted);
        Text(860, 122, 280, 22, "GUARDIAN OF THE CLEARING", menuEyebrow);
        // The same directional fox atlas used in the arena is the menu portrait.
        if (foxHero != null && foxHero.Available)
        {
            Sprite portrait = foxHero.Sample(0, new V(0, -1), false);
            if (portrait != null)
            {
                Rect r = portrait.textureRect;
                Rect uv = new Rect(r.x / portrait.texture.width, r.y / portrait.texture.height,
                    r.width / portrait.texture.width, r.height / portrait.texture.height);
                GUI.DrawTextureWithTexCoords(new Rect(833, 196, 330, 330), portrait.texture, uv, true);
            }
        }
        Box(863, 536, 277, 1, new Color(.55f, .70f, .57f, .3f));
        Text(863, 555, 280, 25, "A little courage goes a long way.", menuMuted);
    }
    public void StartOrResume()
    {
        if (!hasStarted) { Game.Reset(); Game.PlayerControlled = true; accumulator = 0; hasStarted = true; }
        menuOpen = false;
        if (startMenu != null) startMenu.SetState(this, false, true, Game.Outcome);
    }
    public void StartAiMode()
    {
        if (hasStarted) return;
        Game.Reset();
        Game.PlayerControlled = false;
        accumulator = 0;
        hasStarted = true;
        menuOpen = false;
        if (startMenu != null) startMenu.SetState(this, false, true, Game.Outcome);
    }
    bool ProjectWorldToGUI(Camera camera, V world, float scale, out Vector2 gui)
    {
        var p = camera.WorldToScreenPoint(new Vector3(world.x, world.y, 0));
        if (p.z <= 0)
        {
            gui = Vector2.zero;
            return false;
        }
        float insetX = (Screen.width - 1280 * scale) / 2;
        float insetY = (Screen.height - 720 * scale) / 2;
        gui = new Vector2((p.x - insetX) / scale, (Screen.height - p.y - insetY) / scale);
        return true;
    }
    void ViewerWorldLabel(Camera camera, V world, string rawName, Color accent, float scale, float yClamp = 634)
    {
        if (string.IsNullOrWhiteSpace(rawName))
            return;
        string viewerName = Actions.ViewerName(rawName);
        Vector2 gui;
        if (!ProjectWorldToGUI(camera, world, scale, out gui))
            return;
        viewerLabel.fontSize = 18;
        viewerLabel.clipping = TextClipping.Clip;
        var content = new GUIContent(viewerName);
        float width = Mathf.Clamp(viewerLabel.CalcSize(content).x + 24, 96, 280);
        while (viewerLabel.fontSize > 10 && viewerLabel.CalcSize(content).x > width - 20)
            viewerLabel.fontSize--;
        float x = Mathf.Clamp(gui.x, width / 2 + 8, 1280 - width / 2 - 8);
        float y = Mathf.Clamp(gui.y - 12, 102, yClamp);
        Box(x - width / 2 + 3, y + 3, width, 24, new Color(0, 0, 0, .35f));
        Box(x - width / 2, y, width, 24, new Color(.035f, .045f, .05f, .94f));
        Box(x - width / 2, y, 3, 24, accent);
        viewerLabel.normal.textColor = accent;
        Text(x - width / 2 + 8, y - 1, width - 12, 26, viewerName, viewerLabel);
    }
    void OnGUI()
    {
        if (Game == null)
            return;
        Styles();
        float scale = Mathf.Min(Screen.width / 1280f, Screen.height / 720f);
        GUI.matrix = Matrix4x4.TRS(
            new Vector3((Screen.width - 1280 * scale) / 2, (Screen.height - 720 * scale) / 2, 0),
            Quaternion.identity, Vector3.one * scale);
        if (menuOpen)
        {
            if (startMenu == null) DrawMenu();
            return;
        }
        // Project the actual world position into the letterboxed GUI coordinate system.
        var camera = Camera.main;
        foreach (var enemy in Game.Enemies)
        {
            if (!enemy.champion || enemy.hp <= 0 || enemy.dissolve > 0 || camera == null) continue;
            Vector2 gui;
            if (!ProjectWorldToGUI(camera, enemy.pos + new V(0, 1.55f), scale, out gui)) continue;
            float width = Mathf.Clamp((enemy.displayName ?? "MuxyVisitor").Length * 9 + 20, 110, 260);
            float x = Mathf.Clamp(gui.x, width / 2 + 8, 1280 - width / 2 - 8);
            float y = Mathf.Clamp(gui.y, 102, showSimulator ? 530 : 634);
            Box(x - width / 2, y, width, 25, new Color(.06f, .035f, .02f, .92f));
            Box(x - width / 2, y + 24, width, 2, PlaceholderArt.Hex("ff963f"));
            Text(x - width / 2, y, width, 24, enemy.displayName ?? "MuxyVisitor", nameplate);
        }
        if (camera != null)
        {
            foreach (var hazard in Game.Hazards)
            {
                if (hazard.star && !string.IsNullOrWhiteSpace(hazard.viewerName))
                    ViewerWorldLabel(camera, hazard.pos, hazard.viewerName, Teal, scale, showSimulator ? 530 : 634);
                else if (hazard.viewer && !string.IsNullOrWhiteSpace(hazard.viewerName))
                    ViewerWorldLabel(camera, hazard.pos, hazard.viewerName, HotPink, scale, showSimulator ? 530 : 634);
            }
            foreach (var burst in Game.Bursts)
            {
                // Kind 1 is the Star Strike impact; keep Warden impact rings unlabelled.
                if (burst.kind == 1 && !string.IsNullOrWhiteSpace(burst.viewerName))
                    ViewerWorldLabel(camera, burst.pos, burst.viewerName, Teal, scale, showSimulator ? 530 : 634);
            }
        }
        forestHud.Draw(Game, levelText, hpText, gateway.IsSimulation);
        forestHud.DrawNotices(Game, showSimulator);
        if (Game.Phase == Phase.Intro && !Game.PlayerControlled) forestHud.DrawIntro(resultText, countdownText);
        if (Game.Phase == Phase.Results) forestHud.DrawResults(Game, countdownText);
        if (showSimulator)
        {
            Box(0, 562, 1280, 158, new Color(.04f, .075f, .08f, .96f));
            Text(28, 576, 480, 22, "LOCAL ACTION SIMULATOR   /   FREE DEMO ACTIONS", small);
            Text(600, 576, 640, 22, "SIMULATED EVENTS ONLY  ·  MUXY DISCONNECTED  ·  F1 TO HIDE", small);
            for (int i = 0; i < Actions.Count; i++)
            {
                var kind = (ActionKind)i;
                float x = 28 + i * (1224f / Actions.Count);
                string reason = Dispatcher.Availability(kind);
                GUI.enabled = reason == null;
                GUI.backgroundColor = Actions.Category(kind) == "help" ? new Color(.06f, .28f, .27f) : new Color(.27f, .13f, .28f);
                if (GUI.Button(new Rect(x, 606, 1224f / Actions.Count - 8, 40), $"{i+1}   {Actions.Label(kind)}", button))
                    simulator.Submit(Game, kind);
                GUI.backgroundColor = Color.white;
                GUI.enabled = true;
                string sub = reason == null ? (Actions.Category(kind).ToUpperInvariant())
                             : Dispatcher.Cooldown(kind) > 0 ? $"RECHARGING  {Dispatcher.Cooldown(kind):0}s"
                                                             : reason.Replace('_', ' ').ToUpperInvariant();
                Text(x + 5, 650, 1224f / Actions.Count - 10, 20, sub, actionSmall);
            }
        }
        if (showSimulator)
        {
            Text(28, 686, 530, 22, lastStatus, small);
            Text(638, 686, 430, 22, footerText, small);
            Text(1095, 686, 170, 22, speedText, small);
        }
        if (showSimulator && gateway.IsSimulation && Event.current.type == EventType.KeyDown)
        {
            for (int i = 0; i < Actions.Count; i++)
                if (Event.current.keyCode == (KeyCode)((int)KeyCode.Alpha1 + i))
                {
                    simulator.Submit(Game, (ActionKind)i);
                    Event.current.Use();
                }
            if (Event.current.keyCode == KeyCode.F2)
            {
                speed = speed == 1 ? 10 : 1;
                Event.current.Use();
            }
        }
    }
}
}
