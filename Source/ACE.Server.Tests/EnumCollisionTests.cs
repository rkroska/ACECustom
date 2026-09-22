using System;
using System.Collections.Generic;
using ACE.Entity.Enum.Properties;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ACE.Server.Tests
{
    [TestClass]
    public class EnumCollisionTests
    {
        [TestMethod]
        public void PropertyEnums_CustomRange_HaveNoDuplicateIds()
        {
            var propertyEnums = new[]
            {
                typeof(PropertyInt),
                typeof(PropertyFloat),
                typeof(PropertyString),
                typeof(PropertyBool),
                typeof(PropertyDataId),
                typeof(PropertyInstanceId),
                typeof(PropertyInt64),
                typeof(PropertyAttribute),
                typeof(PropertyAttribute2nd),
            };

            var failures = new List<string>();

            foreach (var enumType in propertyEnums)
            {
                var names = Enum.GetNames(enumType);
                var valueMap = new Dictionary<ulong, List<string>>();

                foreach (var name in names)
                {
                    var val = Convert.ToUInt64(Enum.Parse(enumType, name));
                    if (val >= 9000)
                    {
                        if (!valueMap.TryGetValue(val, out var list))
                        {
                            list = new List<string>();
                            valueMap[val] = list;
                        }
                        list.Add(name);
                    }
                }

                foreach (var kvp in valueMap)
                {
                    if (kvp.Value.Count > 1)
                    {
                        failures.Add($"{enumType.Name}: ID {kvp.Key} is duplicated by [{string.Join(", ", kvp.Value)}]");
                    }
                }
            }

            Assert.AreEqual(0, failures.Count, $"Custom property ID collisions found:\n{string.Join("\n", failures)}");
        }
    }
}
