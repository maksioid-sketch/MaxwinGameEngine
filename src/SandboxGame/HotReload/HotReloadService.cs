using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;

namespace SandboxGame.HotReload;

public sealed class HotReloadService : IDisposable
{
    private readonly FileSystemWatcher _watcher;
    private readonly ConcurrentDictionary<string, long> _lastEventTicks = new();
    private readonly ConcurrentQueue<string> _changedPaths = new();

    private readonly TimeSpan _debounce = TimeSpan.FromMilliseconds(250);

    public HotReloadService(string directoryToWatch, params string[] filters)
    {
        if (!Directory.Exists(directoryToWatch))
            throw new DirectoryNotFoundException($"HotReload watch directory not found: {directoryToWatch}");

        _watcher = new FileSystemWatcher(directoryToWatch)
        {
            IncludeSubdirectories = true,
            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.CreationTime | NotifyFilters.Size
        };

        // We can't set multiple filters on FileSystemWatcher directly,
        // so we watch everything and filter ourselves.
        _watcher.Changed += (_, e) => OnEvent(e.FullPath, filters);
        _watcher.Created += (_, e) => OnEvent(e.FullPath, filters);
        _watcher.Renamed += (_, e) => OnEvent(e.FullPath, filters);
        _watcher.Deleted += (_, e) => OnEvent(e.FullPath, filters);

        _watcher.EnableRaisingEvents = true;
    }

    private void OnEvent(string fullPath, string[] filters)
    {
        // Only care about specific extensions/names
        if (!Matches(fullPath, filters))
            return;

        var now = DateTime.UtcNow.Ticks;

        // Debounce per-file
        var last = _lastEventTicks.GetOrAdd(fullPath, 0);
        if (new TimeSpan(now - last) < _debounce)
            return;

        _lastEventTicks[fullPath] = now;
        _changedPaths.Enqueue(fullPath);
    }

    private static bool Matches(string fullPath, string[] filters)
    {
        // filters example: "atlas.json", ".scene.json"
        var fileName = Path.GetFileName(fullPath);

        foreach (var f in filters)
        {
            if (f.StartsWith(".", StringComparison.Ordinal))
            {
                if (fileName.EndsWith(f, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            else
            {
                if (string.Equals(fileName, f, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Drains file changes recorded since last call.
    /// </summary>
    public List<string> ConsumeChanges()
    {
        var list = new List<string>();
        while (_changedPaths.TryDequeue(out var p))
            list.Add(p);
        return list;
    }

    public void Dispose()
    {
        _watcher.EnableRaisingEvents = false;
        _watcher.Dispose();
    }
}
