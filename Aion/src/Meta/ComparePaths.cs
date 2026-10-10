using System;
using System.IO;

namespace Aion.Meta;

public static class ComparePaths
{
    public static bool AreEqual(string path1, string path2)
    {
        var full1 = Path.GetFullPath(path1);
        var full2 = Path.GetFullPath(path2);

        // meta: Windows is case-insensitive, Linux/macOS are typically case-sensitive.
        var comparison =
            OperatingSystem.IsWindows()
                ? StringComparison.OrdinalIgnoreCase
                : StringComparison.Ordinal;

        return string.Equals
        (
            full1.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
            full2.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
            comparison
        );
    }
}