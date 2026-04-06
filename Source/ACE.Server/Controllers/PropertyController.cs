using Microsoft.AspNetCore.Mvc;
using ACE.Entity.Enum.Properties;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace ACE.Server.Web.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class PropertyController : BaseController
    {
        private static HashSet<string>? _availableEnums;
        private static readonly object _syncRoot = new object();

        // Optimized, O(1) lookup set for game-wide enums
        private static HashSet<string> AvailableEnums
        {
            get
            {
                if (_availableEnums == null)
                {
                    lock (_syncRoot)
                    {
                        if (_availableEnums == null)
                        {
                            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                            foreach (var t in ReflectionCache.AllEntityTypes)
                            {
                                if (t.IsEnum && t.IsPublic && 
                                    t.Namespace != null && 
                                    t.Namespace.StartsWith("ACE.Entity.Enum") && 
                                    !t.Namespace.Contains("Properties"))
                                {
                                    names.Add(t.Name);
                                }
                            }
                            _availableEnums = names;
                        }
                    }
                }
                return _availableEnums;
            }
        }

        [HttpGet("metadata")]
        public IActionResult GetPropertyMetadata()
        {
            if (!IsAdmin)
                return Forbid();

            try
            {
                var metadata = new List<PropertyMetadata>();

                // Single-pass construction from authoritative enums
                // These calls are now O(N) instead of O(N*M)
                metadata.AddRange(GetEnumMetadata<PropertyBool>("Bool"));
                metadata.AddRange(GetEnumMetadata<PropertyInt>("Int"));
                metadata.AddRange(GetEnumMetadata<PropertyFloat>("Float"));
                metadata.AddRange(GetEnumMetadata<PropertyString>("String"));
                metadata.AddRange(GetEnumMetadata<PropertyDataId>("DataId"));
                metadata.AddRange(GetEnumMetadata<PropertyInstanceId>("InstanceId"));
                metadata.AddRange(GetEnumMetadata<PropertyInt64>("Int64"));

                return Ok(metadata.OrderBy(x => x.Name).ToList());
            }
            catch (Exception ex)
            {
                var correlationId = Guid.NewGuid();
                Log.Error($"[CID:{correlationId}] Failed to fetch property metadata", ex);
                return StatusCode(500, new { 
                    message = "An unexpected error occurred while fetching property metadata.", 
                    correlationId 
                });
            }
        }

        private IEnumerable<PropertyMetadata> GetEnumMetadata<TEnum>(string category) where TEnum : Enum
        {
            var enums = AvailableEnums;
            var values = Enum.GetValues(typeof(TEnum)).Cast<TEnum>();
            
            var result = new List<PropertyMetadata>();
            foreach (var e in values)
            {
                var name = e.ToString();
                // O(1) Smart Join: Instantly check if this property name matches a game Enum
                string? linkedEnum = enums.Contains(name) ? name : null;
                
                result.Add(new PropertyMetadata
                {
                    Id = Convert.ToInt32(e),
                    Name = name,
                    Type = category,
                    LinkedEnum = linkedEnum
                });
            }
            return result;
        }

        public class PropertyMetadata
        {
            public int Id { get; set; }
            public string Name { get; set; } = "";
            public string Type { get; set; } = "";
            public string? LinkedEnum { get; set; }
        }
    }
}
