using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using ACE.Common;

namespace ACE.Server.Services
{
    public class CurationItemDto
    {
        public uint CreatureWcid { get; set; }
        public string CreatureName { get; set; }
        public uint TextureId { get; set; }
        public uint PaletteId { get; set; }
        public int Rating { get; set; } // 1 = Approved, -1 = Blacklisted, 0 = Unrated
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }

    public class CurationDataFile
    {
        public string Version { get; set; } = "1.0";
        public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
        public List<CurationItemDto> Curations { get; set; } = new List<CurationItemDto>();
    }

    public static class CurationService
    {
        private static string GetPersistentFilePath()
        {
            var currentDir = AppDomain.CurrentDomain.BaseDirectory;
            var dir = new DirectoryInfo(currentDir);
            
            while (dir != null)
            {
                if (File.Exists(Path.Combine(dir.FullName, "ACE.Server.csproj")))
                {
                    return Path.Combine(dir.FullName, "curated_textures.json");
                }
                dir = dir.Parent;
            }
            
            return Path.Combine(currentDir, "curated_textures.json");
        }

        private static readonly string CurationFilePath = GetPersistentFilePath();
        private static readonly object FileLock = new object();
        private static readonly ConcurrentDictionary<string, CurationItemDto> CurationCache = new ConcurrentDictionary<string, CurationItemDto>();

        private static bool _isDirty = false;
        private static readonly System.Threading.Timer _saveTimer;

        static CurationService()
        {
            LoadCurations();
            _saveTimer = new System.Threading.Timer(_ => FlushCurationsIfNeeded(), null, TimeSpan.FromSeconds(3), TimeSpan.FromSeconds(3));
        }

        private static void FlushCurationsIfNeeded()
        {
            if (!_isDirty) return;
            SaveCurations();
            _isDirty = false;
        }

        private static string GetKey(uint wcid, uint textureId, uint paletteId)
        {
            return $"{wcid}_{textureId}_{paletteId}";
        }

        private static void LoadCurations()
        {
            lock (FileLock)
            {
                if (File.Exists(CurationFilePath))
                {
                    MergeFromFile(CurationFilePath);
                }

                // Check Release build path to merge existing bulk approvals/blacklists
                var baseDir = AppDomain.CurrentDomain.BaseDirectory;
                var releasePath = Path.Combine(baseDir, "bin", "Release", "net10.0", "curated_textures.json");
                if (!File.Exists(releasePath))
                {
                    releasePath = Path.Combine(Directory.GetParent(baseDir)?.FullName ?? "", "Release", "net10.0", "curated_textures.json");
                }

                if (File.Exists(releasePath))
                {
                    MergeFromFile(releasePath);
                    _isDirty = true;
                }
            }
        }

        private static void MergeFromFile(string filePath)
        {
            try
            {
                var json = File.ReadAllText(filePath);
                var data = JsonSerializer.Deserialize<CurationDataFile>(json);
                if (data?.Curations != null)
                {
                    foreach (var item in data.Curations)
                    {
                        var key = GetKey(item.CreatureWcid, item.TextureId, item.PaletteId);
                        if (!CurationCache.ContainsKey(key))
                        {
                            CurationCache[key] = item;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                log4net.LogManager.GetLogger(typeof(CurationService)).Error($"Failed to merge curated_textures.json from {filePath}", ex);
            }
        }

        private static void SaveCurations()
        {
            lock (FileLock)
            {
                try
                {
                    var fileData = new CurationDataFile
                    {
                        Version = "1.0",
                        LastUpdated = DateTime.UtcNow,
                        Curations = CurationCache.Values.ToList()
                    };

                    var json = JsonSerializer.Serialize(fileData, new JsonSerializerOptions { WriteIndented = true });
                    File.WriteAllText(CurationFilePath, json);
                }
                catch (Exception ex)
                {
                    log4net.LogManager.GetLogger(typeof(CurationService)).Error("Failed to save curated_textures.json", ex);
                }
            }
        }

        public static CurationItemDto AddOrUpdateCuration(uint wcid, string creatureName, uint textureId, uint paletteId, int rating)
        {
            var key = GetKey(wcid, textureId, paletteId);
            var item = new CurationItemDto
            {
                CreatureWcid = wcid,
                CreatureName = creatureName ?? $"WCID {wcid}",
                TextureId = textureId,
                PaletteId = paletteId,
                Rating = rating,
                Timestamp = DateTime.UtcNow
            };

            CurationCache[key] = item;
            _isDirty = true;
            return item;
        }

        public static CurationItemDto GetCuration(uint wcid, uint textureId, uint paletteId)
        {
            var key = GetKey(wcid, textureId, paletteId);
            CurationCache.TryGetValue(key, out var item);
            return item;
        }

        public static List<CurationItemDto> GetCurationsForCreature(uint wcid)
        {
            return CurationCache.Values.Where(x => x.CreatureWcid == wcid).ToList();
        }

        public static List<CurationItemDto> GetAllCurations()
        {
            return CurationCache.Values.ToList();
        }
    }
}
