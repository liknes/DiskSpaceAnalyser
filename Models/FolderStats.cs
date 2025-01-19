public class FolderStats
{
    public long TotalSize { get; set; }
    public int FileCount { get; set; }
    public int SubfolderCount { get; set; }
    public Dictionary<string, long> FileTypeStats { get; set; } = new();
} 