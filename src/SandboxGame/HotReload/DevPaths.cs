using System;
using System.IO;

namespace SandboxGame.HotReload;

public static class DevPaths
{
    public static string FindProjectRoot(string projectFileName)
    {
        // Start from the output folder (bin/Debug/netX.Y/)
        var dir = new DirectoryInfo(AppContext.BaseDirectory);

        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, projectFileName);
            if (File.Exists(candidate))
                return dir.FullName;

            dir = dir.Parent;
        }

        // Fallback: output directory
        return AppContext.BaseDirectory;
    }
}
