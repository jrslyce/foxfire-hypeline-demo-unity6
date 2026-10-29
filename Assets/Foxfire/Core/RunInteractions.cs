using System;
using System.Collections.Generic;

namespace Foxfire
{
// Aggregates only current-run gameplay requests. No request payloads or credentials are retained.
// Dispatcher receipt/queue deduplication happens before Sent; terminal replay cannot add totals.
public sealed class RunInteractions
{
    readonly int[] sent = new int[Actions.Count], played = new int[Actions.Count];
    readonly HashSet<string> names = new HashSet<string>(StringComparer.Ordinal);
    readonly List<string> viewers = new List<string>();
    public int TotalSent { get; private set; }
    public int TotalPlayed { get; private set; }
    public bool Sealed { get; private set; }
    public int ViewerCount { get { return viewers.Count; } }
    public string ViewerAt(int index) { return viewers[index]; }
    public int SentCount(ActionKind kind) { return sent[(int)kind]; }
    public int PlayedCount(ActionKind kind) { return played[(int)kind]; }
    public void Sent(ActionKind kind, string name)
    {
        if (Sealed) return;
        sent[(int)kind]++; TotalSent++;
        string displayName = Actions.ViewerName(name);
        if (names.Add(displayName)) viewers.Add(displayName);
    }
    public void Played(ActionKind kind)
    {
        if (Sealed) return;
        played[(int)kind]++; TotalPlayed++;
    }
    public void Seal()
    {
        if (Sealed) return;
        viewers.Sort(StringComparer.OrdinalIgnoreCase);
        Sealed = true;
    }
}
}
