using System.Collections.Concurrent;
using DirectumMcp.Core.Models;
using DirectumMcp.Core.Parsers;

namespace DirectumMcp.Core.Cache;

/// <summary>
/// Thread-safe LRU-like cache for parsed .mtd metadata files.
/// Uses FileSystemWatcher for auto-invalidation on file changes.
/// Registered as Singleton in DI.
/// </summary>
public sealed class MetadataCache : IMetadataCache, IDisposable
{
    private readonly string _solutionPath;
    private readonly FileSystemWatcher? _watcher;
    private readonly SemaphoreSlim _loadLock = new(1, 1);

    // Caches: path → (metadata, lastWriteUtc)
    private readonly ConcurrentDictionary<string, (EntityMetadata meta, DateTime lastWrite)> _entities = new();
    private readonly ConcurrentDictionary<string, (ModuleMetadata meta, DateTime lastWrite)> _modules = new();

    private volatile bool _entitiesLoaded;
    private volatile bool _modulesLoaded;

    public MetadataCache(string solutionPath)
    {
        _solutionPath = solutionPath;

        if (!Directory.Exists(solutionPath))
            return;

        try
        {
            _watcher = new FileSystemWatcher(solutionPath, "*.mtd")
            {
                IncludeSubdirectories = true,
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName
            };
            _watcher.Changed += OnFileChanged;
            _watcher.Created += OnFileChanged;
            _watcher.Deleted += OnFileDeleted;
            _watcher.Renamed += OnFileRenamed;
            _watcher.EnableRaisingEvents = true;
        }
        catch
        {
            // FileSystemWatcher may fail on some mounts; cache still works, just no auto-invalidation
        }
    }

    public int CachedEntityCount => _entities.Count;
    public int CachedModuleCount => _modules.Count;

    public async Task<IReadOnlyList<EntityMetadata>> GetAllEntitiesAsync(CancellationToken ct = default)
    {
        if (!_entitiesLoaded)
            await LoadAllAsync(ct);
        return _entities.Values.Select(v => v.meta).ToList();
    }

    public async Task<IReadOnlyList<ModuleMetadata>> GetAllModulesAsync(CancellationToken ct = default)
    {
        if (!_modulesLoaded)
            await LoadAllAsync(ct);
        return _modules.Values.Select(v => v.meta).ToList();
    }

    public async Task<EntityMetadata?> FindEntityAsync(string name, CancellationToken ct = default)
    {
        if (!_entitiesLoaded)
            await LoadAllAsync(ct);
        return _entities.Values
            .FirstOrDefault(v => v.meta.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
            .meta;
    }

    public async Task<EntityMetadata?> FindEntityByGuidAsync(string guid, CancellationToken ct = default)
    {
        if (!_entitiesLoaded)
            await LoadAllAsync(ct);
        return _entities.Values
            .FirstOrDefault(v => v.meta.NameGuid.Equals(guid, StringComparison.OrdinalIgnoreCase))
            .meta;
    }

    public async Task<ModuleMetadata?> FindModuleAsync(string name, CancellationToken ct = default)
    {
        if (!_modulesLoaded)
            await LoadAllAsync(ct);
        return _modules.Values
            .FirstOrDefault(v => v.meta.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
            .meta;
    }

    public async Task<IReadOnlyList<MetadataSearchResult>> SearchAsync(string query, int maxResults = 50, CancellationToken ct = default)
    {
        if (!_entitiesLoaded || !_modulesLoaded)
            await LoadAllAsync(ct);

        var q = query.ToLowerInvariant();
        var results = new List<MetadataSearchResult>();

        // Search entities
        foreach (var (path, (meta, _)) in _entities)
        {
            if (ct.IsCancellationRequested) break;

            bool match = meta.Name.Contains(q, StringComparison.OrdinalIgnoreCase)
                || meta.NameGuid.StartsWith(q, StringComparison.OrdinalIgnoreCase)
                || meta.Properties.Any(p => p.Name.Contains(q, StringComparison.OrdinalIgnoreCase));

            if (match)
            {
                results.Add(new MetadataSearchResult
                {
                    FilePath = path,
                    Name = meta.Name,
                    Type = "Entity",
                    NameGuid = meta.NameGuid,
                    BaseGuid = meta.BaseGuid,
                    PropertyCount = meta.Properties.Count
                });
            }

            if (results.Count >= maxResults) break;
        }

        // Search modules
        if (results.Count < maxResults)
        {
            foreach (var (path, (meta, _)) in _modules)
            {
                if (ct.IsCancellationRequested) break;

                bool match = meta.Name.Contains(q, StringComparison.OrdinalIgnoreCase)
                    || meta.NameGuid.StartsWith(q, StringComparison.OrdinalIgnoreCase);

                if (match)
                {
                    results.Add(new MetadataSearchResult
                    {
                        FilePath = path,
                        Name = meta.Name,
                        Type = "Module",
                        NameGuid = meta.NameGuid
                    });
                }

                if (results.Count >= maxResults) break;
            }
        }

        return results;
    }

    public void Invalidate(string? filePath = null)
    {
        if (filePath is null)
        {
            _entities.Clear();
            _modules.Clear();
            _entitiesLoaded = false;
            _modulesLoaded = false;
        }
        else
        {
            _entities.TryRemove(filePath, out _);
            _modules.TryRemove(filePath, out _);
        }
    }

    private async Task LoadAllAsync(CancellationToken ct)
    {
        await _loadLock.WaitAsync(ct);
        try
        {
            if (_entitiesLoaded && _modulesLoaded)
                return;

            if (!Directory.Exists(_solutionPath))
                return;

            var mtdFiles = Directory.EnumerateFiles(_solutionPath, "*.mtd", SearchOption.AllDirectories)
                .Where(f => !f.Contains("/obj/") && !f.Contains("/bin/") &&
                           !f.Contains("\\obj\\") && !f.Contains("\\bin\\"))
                .ToList();

            foreach (var file in mtdFiles)
            {
                ct.ThrowIfCancellationRequested();
                await TryLoadFileAsync(file, ct);
            }

            _entitiesLoaded = true;
            _modulesLoaded = true;

            Console.Error.WriteLine(
                $"[MetadataCache] Loaded {_entities.Count} entities, {_modules.Count} modules from {mtdFiles.Count} .mtd files");
        }
        finally
        {
            _loadLock.Release();
        }
    }

    private async Task TryLoadFileAsync(string filePath, CancellationToken ct)
    {
        try
        {
            var lastWrite = File.GetLastWriteTimeUtc(filePath);
            var fileName = Path.GetFileName(filePath);

            if (fileName.Equals("Module.mtd", StringComparison.OrdinalIgnoreCase))
            {
                // Check if already cached and up to date
                if (_modules.TryGetValue(filePath, out var cached) && cached.lastWrite >= lastWrite)
                    return;

                var module = await MtdParser.ParseModuleAsync(filePath, ct);
                _modules[filePath] = (module, lastWrite);
            }
            else if (fileName.EndsWith(".mtd", StringComparison.OrdinalIgnoreCase))
            {
                if (_entities.TryGetValue(filePath, out var cached) && cached.lastWrite >= lastWrite)
                    return;

                try
                {
                    var entity = await MtdParser.ParseEntityAsync(filePath, ct);
                    if (!string.IsNullOrEmpty(entity.Name))
                        _entities[filePath] = (entity, lastWrite);
                }
                catch
                {
                    // Not all .mtd files are entity files (some are Report.mtd, etc.)
                    // Skip silently
                }
            }
        }
        catch
        {
            // File locked, corrupted, etc. — skip
        }
    }

    private void OnFileChanged(object sender, FileSystemEventArgs e)
    {
        Invalidate(e.FullPath);
    }

    private void OnFileDeleted(object sender, FileSystemEventArgs e)
    {
        _entities.TryRemove(e.FullPath, out _);
        _modules.TryRemove(e.FullPath, out _);
    }

    private void OnFileRenamed(object sender, RenamedEventArgs e)
    {
        _entities.TryRemove(e.OldFullPath, out _);
        _modules.TryRemove(e.OldFullPath, out _);
        Invalidate(e.FullPath);
    }

    public void Dispose()
    {
        _watcher?.Dispose();
        _loadLock.Dispose();
    }
}
