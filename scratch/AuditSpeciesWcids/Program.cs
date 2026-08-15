using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using ACE.Common;
using ACE.Database;
using ACE.Database.Models.World;
using ACE.DatLoader;
using ACE.DatLoader.FileTypes;
using ACE.Entity.Enum;
using ACE.Entity.Enum.Properties;

namespace AuditSpeciesWcids
{
    public class Program
    {
        public class SpeciesEntry
        {
            public uint CreatureTypeId { get; set; }
            public string SpeciesName { get; set; } = "";
            public uint DefaultWcid { get; set; }
            public string WeenieName { get; set; } = "";
            public string SetupIdHex { get; set; } = "";
        }

        public static void Main(string[] args)
        {
            Console.WriteLine("=== ACE World Database Creature Species Scanner ===");

            string configPath = @"Source\ACE.Server\bin\Debug\net10.0\Config.js";
            if (!File.Exists(configPath))
                configPath = @"Source\ACE.Server\config.json";
            if (!File.Exists(configPath))
                configPath = @"C:\Scripting\ACECustom\Source\ACE.Server\config.json";

            Console.WriteLine($"Using config path: {Path.GetFullPath(configPath)}");
            ConfigManager.Initialize(configPath);

            var datPath = @"C:\ACE\Dats";
            DatManager.Initialize(datPath, false, false, false);
            DatabaseManager.Initialize();

            Console.WriteLine("DatabaseManager initialized. Scanning weenies...");

            var allWeenies = DatabaseManager.World.GetAllWeenies();
            Console.WriteLine($"Loaded {allWeenies.Count} total weenie templates.");

            // Excluded pseudo-types / non-monsters
            var excludedTypes = new HashSet<CreatureType>
            {
                CreatureType.Invalid,
                CreatureType.Player,
                CreatureType.QuestPlayer,
                CreatureType.AttackAll,
                CreatureType.Unknown
            };

            var discoveredSpecies = new Dictionary<CreatureType, SpeciesEntry>();

            var sortedWeenies = allWeenies.OrderBy(w => w.ClassId).ToList();

            foreach (var weenie in sortedWeenies)
            {
                if (weenie.Type != (int)WeenieType.Creature)
                    continue;

                // Check CreatureType int property
                var cTypeVal = weenie.GetProperty(PropertyInt.CreatureType);
                if (!cTypeVal.HasValue)
                    continue;

                var cType = (CreatureType)cTypeVal.Value;
                if (excludedTypes.Contains(cType))
                    continue;

                if (discoveredSpecies.ContainsKey(cType))
                    continue;

                // Check Setup DID property
                var setupId = weenie.GetProperty(PropertyDataId.Setup);
                if (!setupId.HasValue || setupId.Value == 0)
                    continue;

                // Validate setup model exists in portal.dat
                var setup = DatManager.PortalDat.ReadFromDat<SetupModel>(setupId.Value);
                if (setup == null || setup.Parts == null || setup.Parts.Count == 0)
                    continue;

                // First valid WCID for this species found!
                var speciesName = Enum.GetName(typeof(CreatureType), cType) ?? cType.ToString();
                var entry = new SpeciesEntry
                {
                    CreatureTypeId = (uint)cType,
                    SpeciesName = speciesName,
                    DefaultWcid = weenie.ClassId,
                    WeenieName = weenie.ClassName ?? "",
                    SetupIdHex = $"0x{setupId.Value:X8}"
                };

                discoveredSpecies[cType] = entry;
                Console.WriteLine($"[FOUND SPECIES] {speciesName} (ID: {(uint)cType}) -> WCID {weenie.ClassId} ({weenie.ClassName}) | Setup {entry.SetupIdHex}");
            }

            Console.WriteLine($"\n=== Scan Complete! Discovered {discoveredSpecies.Count} unique creature species. ===");

            var resultList = discoveredSpecies.Values.OrderBy(s => s.SpeciesName).ToList();

            var jsonOptions = new JsonSerializerOptions { WriteIndented = true };
            var json = JsonSerializer.Serialize(resultList, jsonOptions);
            File.WriteAllText(@"scratch\discovered_species_wcids.json", json);

            // Generate TypeScript Constant
            var tsLines = new List<string>
            {
                "export interface SpeciesOption {",
                "  name: string;",
                "  wcid: number;",
                "  creatureTypeId: number;",
                "  setupId: string;",
                "}",
                "",
                "export const speciesList: SpeciesOption[] = ["
            };

            foreach (var item in resultList)
            {
                tsLines.Add($"  {{ name: '{item.SpeciesName}', wcid: {item.DefaultWcid}, creatureTypeId: {item.CreatureTypeId}, setupId: '{item.SetupIdHex}' }},");
            }
            tsLines.Add("];");

            File.WriteAllLines(@"scratch\speciesList.ts", tsLines);
            Console.WriteLine("Wrote results to scratch\\discovered_species_wcids.json and scratch\\speciesList.ts");
        }
    }
}
