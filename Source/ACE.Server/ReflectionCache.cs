using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using ACE.Entity.Enum;

namespace ACE.Server.Web
{
    public static class ReflectionCache
    {
        private static readonly Assembly EntityAssembly = typeof(Skill).Assembly;
        private static IReadOnlyList<Type>? _allEntityTypes;
        private static IReadOnlyCollection<string>? _linkedEnumNames;
        private static readonly object _syncRoot = new object();

        public static IReadOnlyList<Type> AllEntityTypes
        {
            get
            {
                if (_allEntityTypes == null)
                {
                    lock (_syncRoot)
                    {
                        if (_allEntityTypes == null)
                        {
                            try
                            {
                                _allEntityTypes = EntityAssembly.GetTypes().ToList().AsReadOnly();
                            }
                            catch (ReflectionTypeLoadException ex)
                            {
                                _allEntityTypes = ex.Types.Where(t => t != null).Cast<Type>().ToList().AsReadOnly();
                            }
                        }
                    }
                }
                return _allEntityTypes;
            }
        }

        public static IReadOnlyCollection<string> LinkedEnumNames
        {
            get
            {
                if (_linkedEnumNames == null)
                {
                    lock (_syncRoot)
                    {
                        if (_linkedEnumNames == null)
                        {
                            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                            var propertyEnums = AllEntityTypes
                                .Where(t => t.IsEnum && t.Namespace != null && t.Namespace.Contains("ACE.Entity.Enum.Properties"))
                                .ToList();

                            foreach (var pe in propertyEnums)
                            {
                                foreach (var name in Enum.GetNames(pe))
                                {
                                    names.Add(name);
                                }
                            }
                            _linkedEnumNames = names;
                        }
                    }
                }
                return _linkedEnumNames;
            }
        }
    }
}
