using System;

namespace MarkLocal.Models;

public class DraftSnapshot
{
    public string Id { get; set; } = string.Empty;
    public string? OriginalPath { get; set; }
    public string? Title { get; set; }
    public int ProcessId { get; set; }
    public DateTime SessionStartUtc { get; set; }
    public DateTime LastModifiedUtc { get; set; }
    public string ContentPath { get; set; } = string.Empty;
    public string MetadataPath { get; set; } = string.Empty;
    public long ContentLength { get; set; }
}
