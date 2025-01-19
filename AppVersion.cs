namespace DiskSpaceAnalyzer
{
    public static class AppVersion
    {
        public const string Version = "1.0.0";
        public const string BuildDate = "2024-03-19";
        public const string Copyright = "© 2024";
        
        public static string VersionString => $"v{Version}";
        public static string FullVersionString => $"{VersionString} ({BuildDate})";
    }
} 