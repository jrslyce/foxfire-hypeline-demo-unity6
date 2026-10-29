using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using UnityEngine;
using Unity.Profiling;
namespace Foxfire
{
// Opt-in instrumentation. CSV frame intervals include rendering/pacing; tick time excludes pacing.
public sealed class WorkloadProfile : IDisposable
{
    readonly StreamWriter writer;
    readonly string scenario;
    readonly double duration;
    readonly Stopwatch clock = Stopwatch.StartNew();
    long previousAllocation;
    double flushAt;
    bool allocationsSupported = true;
    ProfilerRecorder allocationRecorder;
    public WorkloadProfile(string output, string scenario, double duration)
    {
        this.scenario = scenario; this.duration = duration;
        writer = new StreamWriter(Path.Combine(output, "frames.csv"), false, System.Text.Encoding.UTF8, 65536);
        writer.WriteLine("elapsed_s,frame_ms,simulation_ms,ticks,managed_allocated_bytes,gc_collections,enemies,shots,pickups,effects,queue,run,phase,submissions");
        try
        {
            long before=GC.GetAllocatedBytesForCurrentThread();
            var probe=new byte[1024];
            previousAllocation=GC.GetAllocatedBytesForCurrentThread();
            GC.KeepAlive(probe);
            allocationsSupported=previousAllocation>before;
        }
        catch (NotImplementedException) { allocationsSupported = false; }
        if(!allocationsSupported)
            allocationRecorder=ProfilerRecorder.StartNew(ProfilerCategory.Memory,"GC Allocated In Frame",1);
        File.WriteAllText(Path.Combine(output,"profile-host.txt"),
            "Unity="+Application.unityVersion+"\nOS="+SystemInfo.operatingSystem+
            "\nCPU="+SystemInfo.processorType+"\nLogicalCPUs="+SystemInfo.processorCount+
            "\nRAM_MB="+SystemInfo.systemMemorySize+"\nGPU="+SystemInfo.graphicsDeviceName+
            "\nGraphicsAPI="+SystemInfo.graphicsDeviceType+"\nScenario="+scenario+
            "\nAllocationCounter="+(allocationsSupported?"main-thread bytes between Updates":
                allocationRecorder.Valid?"Unity ProfilerRecorder: GC Allocated In Frame (previous frame)":"unavailable (-1)")+
            "\nGC counter is cumulative; does not measure incremental GC slice duration.\n");
    }
    public static long Timestamp() { return Stopwatch.GetTimestamp(); }
    public static double Milliseconds(long start) { return (Stopwatch.GetTimestamp()-start)*1000.0/Stopwatch.Frequency; }
    public void Frame(float frameMs, double simulationMs, int ticks, Simulation game, int queue, int submissions)
    {
        long allocated=-1;
        if (allocationsSupported)
        {
            long current=GC.GetAllocatedBytesForCurrentThread();
            allocated=current-previousAllocation; previousAllocation=current;
        }
        else if(allocationRecorder.Valid && allocationRecorder.Count>0) allocated=allocationRecorder.LastValue;
        writer.Write(clock.Elapsed.TotalSeconds.ToString("F6",CultureInfo.InvariantCulture)); writer.Write(',');
        writer.Write(frameMs.ToString("F6",CultureInfo.InvariantCulture)); writer.Write(',');
        writer.Write(simulationMs.ToString("F6",CultureInfo.InvariantCulture)); writer.Write(',');
        writer.Write(ticks); writer.Write(','); writer.Write(allocated); writer.Write(',');
        writer.Write(GC.CollectionCount(0)); writer.Write(','); writer.Write(game.Enemies.Count); writer.Write(',');
        writer.Write(game.Shots.Count); writer.Write(','); writer.Write(game.Pickups.Count); writer.Write(',');
        writer.Write(game.Bursts.Count); writer.Write(','); writer.Write(queue); writer.Write(',');
        writer.Write(game.RunNumber); writer.Write(','); writer.Write((int)game.Phase); writer.Write(','); writer.WriteLine(submissions);
        if(clock.Elapsed.TotalSeconds>=flushAt) { writer.Flush(); flushAt=clock.Elapsed.TotalSeconds+1; }
        if(duration>0 && clock.Elapsed.TotalSeconds>=duration) Application.Quit();
    }
    public void Dispose() { writer.Dispose(); allocationRecorder.Dispose(); }

    // Conservative synthetic occupancy fixture, not a claim this combination occurs in normal play.
    // Invulnerability and replenishment keep AI, collision, particles and rendering busy at 1x.
    public static void Saturate(Simulation game)
    {
        game.HP=game.MaxHP;
        game.Laser=game.Rush=8;
        while(game.Enemies.Count<game.B.enemyCap)
        {
            int i=game.Enemies.Count;
            game.AddEnemy((EnemyKind)(i%3),new V(i%10-5,i/10-2),false);
        }
        foreach(var e in game.Enemies) { e.hp=1000000; e.maxHP=2000000; }
        while(game.Shots.Count<game.B.projectileCap)
        {
            int i=game.Shots.Count;
            game.Shots.Add(new Shot { pos=new V(-12,i%12-6),velocity=new V(3,0),life=12,
                hostile=i%2==0,damage=0,pierce=2 });
        }
        while(game.Pickups.Count<game.B.xpCap) game.DropXP(new V(12,game.Pickups.Count%12-6),1);
        while(game.Bursts.Count<game.B.vfxCap) game.Effect(game.Fox,3,0,.5f);
        if(game.Hazards.Count==0)
        {
            game.Hazards.Add(new Hazard {pos=new V(1,0),radius=3,star=true,tell=1});
            game.Hazards.Add(new Hazard {pos=new V(-1,0),radius=2,life=6,damage=0,viewer=true});
        }
    }
}
}
