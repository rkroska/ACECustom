using Microsoft.AspNetCore.Mvc;
using ACE.Server.Managers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace ACE.Server.Web.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ServerParamController : BaseController
    {
        [HttpGet("list")]
        public IActionResult GetServerParams()
        {
            if (!IsAdmin)
                return Forbid();

            try
            {
                // In the structural merger, we have direct access to ServerConfig.
                // No more gross reflection or assembly searching!
                var properties = typeof(ServerConfig).GetProperties(BindingFlags.Public | BindingFlags.Static);
                var result = new List<ServerParamMetadata>();

                foreach (var prop in properties)
                {
                    // Cleanly identify ConfigProperty<T> using the local type
                    if (prop.PropertyType.IsGenericType && 
                        prop.PropertyType.GetGenericTypeDefinition() == typeof(ConfigProperty<>))
                    {
                        var genericType = prop.PropertyType.GetGenericArguments()[0];
                        
                        // Use dynamic for easy access to the generic members without knowing T
                        dynamic? configProp = prop.GetValue(null);
                        if (configProp == null) continue;

                        result.Add(new ServerParamMetadata
                        {
                            Name = prop.Name,
                            Type = GetCSharpAlias(genericType),
                            Description = configProp.Description,
                            DefaultValue = configProp.Default?.ToString() ?? "",
                            CurrentValue = configProp.Value?.ToString() ?? "",
                            IsSet = configProp.HasValue
                        });
                    }
                }

                return Ok(result.OrderBy(p => p.Name).ToList());
            }
            catch (Exception ex)
            {
                var correlationId = Guid.NewGuid().ToString();
                Log.Error($"[Correlation ID: {correlationId}] Failed to fetch server params", ex);
                return StatusCode(500, new 
                { 
                    Message = "An unexpected error occurred while processing your request.",
                    CorrelationId = correlationId 
                });
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

        public class ServerParamMetadata
        {
            public string Name { get; set; } = "";
            public string Type { get; set; } = "";
            public string Description { get; set; } = "";
            public string DefaultValue { get; set; } = "";
            public string CurrentValue { get; set; } = "";
            public bool IsSet { get; set; }
        }
    }
}
