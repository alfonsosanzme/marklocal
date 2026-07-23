using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using MarkLocal.Models;

namespace MarkLocal.Core;

public class RecoveryService
{
    public string DraftsDirectory { get; }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public RecoveryService()
        : this(Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "MarkLocal", "drafts"))
    {
    }

    public RecoveryService(string draftsDirectory)
    {
        DraftsDirectory = draftsDirectory;
        try { Directory.CreateDirectory(DraftsDirectory); } catch { }
    }

    public DraftSnapshot CreateSessionDraft(int processId, DateTime sessionStartLocal)
    {
        string id = $"session-{processId}-{sessionStartLocal:yyyyMMddHHmmss}";
        return new DraftSnapshot
        {
            Id = id,
            ProcessId = processId,
            SessionStartUtc = sessionStartLocal.ToUniversalTime(),
            LastModifiedUtc = DateTime.UtcNow,
            ContentPath = Path.Combine(DraftsDirectory, id + ".draft.md"),
            MetadataPath = Path.Combine(DraftsDirectory, id + ".draft.json")
        };
    }

    public async Task SaveAsync(DraftSnapshot snapshot, string content)
    {
        snapshot.LastModifiedUtc = DateTime.UtcNow;
        snapshot.ContentLength = Encoding.UTF8.GetByteCount(content);
        await File.WriteAllTextAsync(snapshot.ContentPath, content, new UTF8Encoding(false));
        string json = JsonSerializer.Serialize(snapshot, JsonOptions);
        await File.WriteAllTextAsync(snapshot.MetadataPath, json, new UTF8Encoding(false));
    }

    public void Delete(DraftSnapshot snapshot)
    {
        TryDelete(snapshot.ContentPath);
        TryDelete(snapshot.MetadataPath);
    }

    public List<DraftSnapshot> ListOrphans(int currentProcessId)
    {
        var orphans = new List<DraftSnapshot>();
        if (!Directory.Exists(DraftsDirectory)) return orphans;

        foreach (var metaPath in Directory.GetFiles(DraftsDirectory, "*.draft.json"))
        {
            DraftSnapshot? snap = TryReadSnapshot(metaPath);
            if (snap == null) continue;

            snap.MetadataPath = metaPath;
            if (string.IsNullOrEmpty(snap.ContentPath) || !File.Exists(snap.ContentPath))
            {
                snap.ContentPath = Path.Combine(DraftsDirectory, snap.Id + ".draft.md");
            }
            if (!File.Exists(snap.ContentPath))
            {
                TryDelete(metaPath);
                continue;
            }
            if (snap.ProcessId == currentProcessId) continue;
            if (IsProcessAlive(snap.ProcessId)) continue;
            orphans.Add(snap);
        }
        return orphans.OrderByDescending(o => o.LastModifiedUtc).ToList();
    }

    private static DraftSnapshot? TryReadSnapshot(string metaPath)
    {
        try
        {
            byte[] bytes = File.ReadAllBytes(metaPath);
            int start = 0;
            if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF) start = 3;
            var span = new ReadOnlySpan<byte>(bytes, start, bytes.Length - start);
            return JsonSerializer.Deserialize<DraftSnapshot>(span, JsonOptions);
        }
        catch
        {
            return null;
        }
    }

    public void DeleteOrphan(DraftSnapshot snapshot) => Delete(snapshot);

    public void CleanupStaleEmptyDrafts(int currentProcessId)
    {
        // Borra metadatos huérfanos sin contenido para evitar acumulación
        if (!Directory.Exists(DraftsDirectory)) return;
        foreach (var metaPath in Directory.GetFiles(DraftsDirectory, "*.draft.json"))
        {
            var snap = TryReadSnapshot(metaPath);
            if (snap == null) continue;
            if (snap.ProcessId == currentProcessId) continue;
            if (IsProcessAlive(snap.ProcessId)) continue;
            string contentPath = Path.Combine(DraftsDirectory, snap.Id + ".draft.md");
            if (!File.Exists(contentPath))
            {
                TryDelete(metaPath);
            }
        }
    }

    private static bool IsProcessAlive(int pid)
    {
        if (pid <= 0) return false;
        try
        {
            using var p = Process.GetProcessById(pid);
            if (p == null || p.HasExited) return false;
            string name = p.ProcessName ?? string.Empty;
            return name.Equals("MarkLocal", StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    private static void TryDelete(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); } catch { }
    }
}
