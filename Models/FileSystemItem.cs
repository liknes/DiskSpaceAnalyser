namespace DiskSpaceAnalyzer.Models
{
    public class FileSystemItem
    {
        public string Name { get; set; }
        public string FullPath { get; set; }
        public long Size { get; set; }
        public DateTime Created { get; set; }
        public DateTime Modified { get; set; }
        public DateTime Accessed { get; set; }
        public FileAttributes Attributes { get; set; }
        public string Extension { get; set; }
        public bool IsDirectory { get; set; }
        public string FileType { get; set; }
    }
}
