using System.Text;

namespace apiprojnew.Services.News
{
    public static class NewsPhotoFormats
    {
        public const int MaxPhotos = 4;

        public static string? GetContentType(string fileName) =>
            Path.GetExtension(fileName).ToLowerInvariant() switch
            {
                ".jpg" or ".jpeg" => "image/jpeg",
                ".png" => "image/png",
                ".webp" => "image/webp",
                ".gif" => "image/gif",
                _ => null
            };

        public static bool HasValidSignature(byte[] bytes, string contentType) => contentType switch
        {
            "image/jpeg" => bytes.Length >= 3 && bytes[0] == 0xFF && bytes[1] == 0xD8 && bytes[2] == 0xFF,
            "image/png" => bytes.Length >= 8 && bytes.AsSpan(0, 8).SequenceEqual(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }),
            "image/webp" => bytes.Length >= 12 && Encoding.ASCII.GetString(bytes, 0, 4) == "RIFF" && Encoding.ASCII.GetString(bytes, 8, 4) == "WEBP",
            "image/gif" => bytes.Length >= 6 && (Encoding.ASCII.GetString(bytes, 0, 6) is "GIF87a" or "GIF89a"),
            _ => false
        };
    }
}
