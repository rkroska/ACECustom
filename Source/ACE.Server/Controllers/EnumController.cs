using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace ACE.Server.Web.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class EnumController : BaseController
    {
        [HttpGet("list")]
        public IActionResult GetEnumList()
        {
            if (!IsAdmin)
                return Forbid();

            try
            {
                // Use the optimized static type cache and pre-mapped link set
                var linkedNames = ReflectionCache.LinkedEnumNames;
                var types = ReflectionCache.AllEntityTypes;

                // Single-pass construction of the list
                var enumTypes = new List<EnumListItem>();
                foreach (var t in types)
                {
                    if (t.IsEnum && t.IsPublic && 
                        t.Namespace != null && 
                        t.Namespace.StartsWith("ACE.Entity.Enum") && 
                        !t.Namespace.Contains("Properties"))
                    {
                        enumTypes.Add(new EnumListItem
                        {
                            Name = t.Name,
                            // O(1) lookup instead of Any() loop
                            IsLinked = linkedNames.Contains(t.Name)
                        });
                    }
                }

                return Ok(enumTypes.OrderBy(item => item.Name).ToList());
            }
            catch (Exception ex)
            {
                var correlationId = Guid.NewGuid().ToString();
                Log.Error($"[Correlation ID: {correlationId}] Failed to fetch enum list", ex);
                return StatusCode(500, new { Message = "An unexpected error occurred while fetching the enum list.", CorrelationId = correlationId });
            }
        }

        [HttpGet("detail/{typeName}")]
        public IActionResult GetEnumDetail(string typeName)
        {
            if (!IsAdmin)
                return Forbid();

            try
            {
                // Use the static type cache
                var types = ReflectionCache.AllEntityTypes;
                var enumType = types.FirstOrDefault(t => t.IsEnum && t.Name.Equals(typeName, StringComparison.OrdinalIgnoreCase));

                if (enumType == null)
                    return NotFound($"Enum type '{typeName}' not found.");

                var isFlags = enumType.GetCustomAttribute<FlagsAttribute>() != null;
                var underlyingType = Enum.GetUnderlyingType(enumType);
                
                // Smart Join: Identify the primary property that uses this enum
                string? primaryProperty = null;
                var linkedNames = ReflectionCache.LinkedEnumNames;
                
                if (linkedNames.Contains(typeName))
                {
                    // If it is linked, find WHICH property enum uses it
                    // This part only runs when a table is actually used, so we can afford a targeted search
                    var propertyEnums = types.Where(t => t.IsEnum && t.Namespace != null && t.Namespace.Contains("ACE.Entity.Enum.Properties"));
                    foreach (var propEnum in propertyEnums)
                    {
                        if (propEnum.Name.Equals(typeName, StringComparison.OrdinalIgnoreCase) || 
                            Enum.GetNames(propEnum).Any(n => n.Equals(typeName, StringComparison.OrdinalIgnoreCase)))
                        {
                            primaryProperty = $"{propEnum.Name}.{typeName}";
                            break; 
                        }
                    }
                }

                // Use GetNames to preserve all aliases (NPC, Protected, etc.)
                underlyingType = Enum.GetUnderlyingType(enumType);
                var isUnsigned = underlyingType == typeof(byte) ||
                                 underlyingType == typeof(ushort) ||
                                 underlyingType == typeof(uint) ||
                                 underlyingType == typeof(ulong);

                var names = Enum.GetNames(enumType);
                var values = new List<EnumValueMetadata>();
                foreach (var name in names)
                {
                    var value = Enum.Parse(enumType, name);
                    object numericValue;
                    
                    if (isUnsigned)
                        numericValue = Convert.ToUInt64(value);
                    else
                        numericValue = Convert.ToInt64(value);

                    values.Add(new EnumValueMetadata
                    {
                        Id = numericValue,
                        Name = name,
                        HexValue = isFlags ? (isUnsigned ? $"0x{Convert.ToUInt64(numericValue):X2}" : $"0x{Convert.ToInt64(numericValue):X2}") : null
                    });
                }

                return Ok(new EnumDetailResponse {
                    IsFlags = isFlags,
                    UnderlyingType = GetCSharpAlias(underlyingType),
                    Values = values.OrderBy(v => 
                    {
                        if (isUnsigned) return (decimal)Convert.ToUInt64(v.Id);
                        return (decimal)Convert.ToInt64(v.Id);
                    }).ThenBy(v => v.Name).ToList(),
                    PrimaryProperty = primaryProperty
                });
            }
            catch (Exception ex)
            {
                var correlationId = Guid.NewGuid().ToString();
                Log.Error($"[Correlation ID: {correlationId}] Failed to fetch values for enum {typeName}", ex);
                return StatusCode(500, new { Message = $"An unexpected error occurred while fetching details for '{typeName}'.", CorrelationId = correlationId });
            }
        }

        private static string GetCSharpAlias(Type type)
        {
            return type.Name switch
            {
                "Boolean" => "bool",
                "Byte" => "byte",
                "Char" => "char",
                "Decimal" => "decimal",
                "Double" => "double",
                "Int16" => "short",
                "Int32" => "int",
                "Int64" => "long",
                "Object" => "object",
                "SByte" => "sbyte",
                "Single" => "float",
                "String" => "string",
                "UInt16" => "ushort",
                "UInt32" => "uint",
                "UInt64" => "ulong",
                _ => type.Name
            };
        }

        public class EnumListItem
        {
            public string Name { get; set; } = "";
            public bool IsLinked { get; set; }
        }

        public class EnumDetailResponse
        {
            public bool IsFlags { get; set; }
            public string UnderlyingType { get; set; } = "int";
            public List<EnumValueMetadata> Values { get; set; } = new();
            public string? PrimaryProperty { get; set; }
        }

        public class EnumValueMetadata
        {
            public object Id { get; set; } = 0;
            public string Name { get; set; } = "";
            public string? HexValue { get; set; }
        }
    }
}
