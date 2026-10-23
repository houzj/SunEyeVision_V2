using System.IO;

namespace SunEyeVision.UI.Services.Thumbnail;

public static class ImageFileImport
{
    private static readonly HashSet<string> Extensions = new(StringComparer.OrdinalIgnoreCase)
        { ".jpg", ".jpeg", ".png", ".bmp", ".tiff", ".tif" };

    public static Task<string[]> DirectoryFilesAsync(string directory, CancellationToken token) => Task.Run(() =>
        Directory.EnumerateFiles(directory, "*", SearchOption.TopDirectoryOnly)
            .Where(path => { token.ThrowIfCancellationRequested(); return Extensions.Contains(System.IO.Path.GetExtension(path)); })
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase).ThenBy(path => path, StringComparer.Ordinal).ToArray(), token);

    public static Task<string[]> ExistingFilesAsync(IEnumerable<string> paths, CancellationToken token) => Task.Run(() =>
        paths.Where(path => { token.ThrowIfCancellationRequested(); return !string.IsNullOrWhiteSpace(path) && File.Exists(path); })
            .Distinct(StringComparer.OrdinalIgnoreCase).ToArray(), token);
}
