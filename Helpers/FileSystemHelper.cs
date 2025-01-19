namespace DiskSpaceAnalyzer.Helpers
{
    public static class FileSystemHelper
    {
        public static string FormatSize(long bytes)
        {
            string[] sizes = { "B", "KB", "MB", "GB", "TB" };
            double len = bytes;
            int order = 0;
            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len = len / 1024;
            }
            return $"{len:0.##} {sizes[order]}";
        }

        public static string GetFileType(string extension)
        {
            if (string.IsNullOrEmpty(extension)) return "No Extension";

            return extension.ToLower() switch
            {
                ".txt" => "Text Document",
                ".doc" or ".docx" => "Word Document",
                ".xls" or ".xlsx" => "Excel Spreadsheet",
                ".pdf" => "PDF Document",
                ".jpg" or ".jpeg" or ".png" or ".gif" => "Image",
                ".mp3" or ".wav" or ".wma" => "Audio",
                ".mp4" or ".avi" or ".mkv" => "Video",
                ".zip" or ".rar" or ".7z" => "Archive",
                ".exe" or ".msi" => "Application",
                _ => extension.ToUpper()[1..] + " File"
            };
        }
    }
}
