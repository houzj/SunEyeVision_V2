using System.IO;

namespace SunEyeVision.UI.Services.Thumbnail;

public static class ImageFileImport
{
    private static readonly HashSet<string> Extensions = new(StringComparer.OrdinalIgnoreCase)
        { ".jpg", ".jpeg", ".png", ".bmp", ".tiff", ".tif" };

    public static Task<string[]> DirectoryFilesAsync(string directory, CancellationToken token) => Task.Run(() =>
    {
        var files = new List<string>();
        void Visit(string folder, int depth)
        {
            token.ThrowIfCancellationRequested();
            // VM's manual folder import sorts all entries together before visiting each
            // immediate child; native directory subscription has different semantics.
            var entries = Directory.EnumerateFileSystemEntries(folder)
                .Select(path => { token.ThrowIfCancellationRequested(); return path; })
                .OrderBy(path => path.Length).ThenBy(path => path, StringComparer.CurrentCulture).ToArray();
            foreach (var path in entries)
            {
                token.ThrowIfCancellationRequested();
                if (File.Exists(path))
                {
                    if (Extensions.Contains(Path.GetExtension(path))) files.Add(path);
                }
                else if (depth == 0 && Directory.Exists(path)) Visit(path, depth + 1);
            }
        }
        Visit(directory, 0);
        token.ThrowIfCancellationRequested();
        return files.ToArray();
    }, token);

    public static Task<string[]> ExistingFilesAsync(IEnumerable<string> paths, CancellationToken token) => Task.Run(() =>
        paths.Where(path => { token.ThrowIfCancellationRequested(); return !string.IsNullOrWhiteSpace(path) && File.Exists(path); })
            .Distinct(StringComparer.OrdinalIgnoreCase).ToArray(), token);
}
