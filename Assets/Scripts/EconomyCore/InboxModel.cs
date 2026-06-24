using System;
using System.Collections.Generic;

/// <summary>Persisted per-letter state. Content (text/sender/reward) is looked up by
/// letterId from the authored LetterCatalogSO — only state lives here.</summary>
[Serializable]
public class InboxEntry
{
    public string letterId;
    public long receivedUtcTicks;
    public bool read;
    public bool claimed;
}

/// <summary>Pure inbox state machine: deliver letters, track read/claimed, expose
/// newest-first ordering. Wrapped by InboxManager (which persists + grants rewards).</summary>
public class InboxModel
{
    private readonly List<InboxEntry> entries = new List<InboxEntry>();

    /// <summary>Newest delivered letter first.</summary>
    public IReadOnlyList<InboxEntry> Entries
    {
        get
        {
            var copy = new List<InboxEntry>(entries);
            copy.Reverse();
            return copy;
        }
    }

    public void Deliver(string letterId, long nowTicks)
    {
        if (string.IsNullOrEmpty(letterId)) return;
        entries.Add(new InboxEntry
        {
            letterId = letterId,
            receivedUtcTicks = nowTicks,
            read = false,
            claimed = false
        });
    }

    public int UnreadCount()
    {
        int n = 0;
        foreach (var e in entries) if (!e.read) n++;
        return n;
    }

    public bool MarkRead(string letterId)
    {
        var e = Find(letterId);
        if (e == null || e.read) return false;
        e.read = true;
        return true;
    }

    public bool Claim(string letterId)
    {
        var e = Find(letterId);
        if (e == null || e.claimed) return false;
        e.claimed = true;
        return true;
    }

    public void Load(InboxEntry[] saved)
    {
        entries.Clear();
        if (saved == null) return;
        foreach (var e in saved)
            if (e != null && !string.IsNullOrEmpty(e.letterId)) entries.Add(e);
    }

    public InboxEntry[] ToArray() => entries.ToArray();

    private InboxEntry Find(string letterId)
    {
        if (string.IsNullOrEmpty(letterId)) return null;
        foreach (var e in entries) if (e.letterId == letterId) return e;
        return null;
    }
}
