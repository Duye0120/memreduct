using System.ComponentModel;
using System.Diagnostics;

namespace MemReduct.Fluent;

public sealed record ProcessMemoryEntry(string Name, int ProcessId, long WorkingSetBytes)
{
    public string MemoryText => MemorySnapshot.Bytes((ulong)Math.Max(0, WorkingSetBytes));
    public string IdText => "PID " + ProcessId;
}

internal sealed record ProcessMemorySnapshot(IReadOnlyList<ProcessMemoryEntry> Entries, int Unavailable);

internal static class ProcessMemory
{
    internal static ProcessMemorySnapshot Read()
    {
        var processes = Process.GetProcesses();
        var entries = new List<ProcessMemoryEntry>();
        var unavailable = 0;
        try
        {
            foreach (var process in processes)
            {
                try { entries.Add(new(process.ProcessName, process.Id, process.WorkingSet64)); }
                // Processes can exit or deny inspection while the snapshot is being read.
                catch (Exception error) when (error is Win32Exception or InvalidOperationException or NotSupportedException)
                { unavailable++; }
            }
            return new(SelectTop(entries), unavailable);
        }
        finally { foreach (var process in processes) process.Dispose(); }
    }

    internal static ProcessMemoryEntry[] SelectTop(IEnumerable<ProcessMemoryEntry> entries) => entries
        .Where(entry => entry.WorkingSetBytes > 0)
        .OrderByDescending(entry => entry.WorkingSetBytes).ThenBy(entry => entry.ProcessId).Take(8).ToArray();
}
