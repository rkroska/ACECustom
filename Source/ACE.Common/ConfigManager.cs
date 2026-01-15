using log4net;
using System;
using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace ACE.Common
{
    public static class ConfigManager
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(ConfigManager));
        public static MasterConfiguration Config { get; private set; }

        /// <summary>
        /// initializes from a preloaded configuration
        /// </summary>
        public static void Initialize(MasterConfiguration configuration)
        {
            Config = configuration;
        }

        /// <summary>
        /// initializes from a Config.js file specified by the path
        /// </summary>
        public static void Initialize(string path = @"Config.js")
        {
            var directoryName = Path.GetDirectoryName(path);
            var fileName = Path.GetFileName(path) ?? "Config.js";

            string pathToUse;

            // If no directory was specified, try both the current directory and the startup directory
            if (string.IsNullOrWhiteSpace(directoryName))
            {
                directoryName = Environment.CurrentDirectory;

                pathToUse = Path.Combine(directoryName, fileName);

                if (!File.Exists(pathToUse))
                {
                    // File not found in Environment.CurrentDirectory
                    // Lets try the ExecutingAssembly Location
                    var executingAssemblyLocation = System.Reflection.Assembly.GetExecutingAssembly().Location;

                    directoryName = Path.GetDirectoryName(executingAssemblyLocation);

                    if (directoryName != null)
                        pathToUse = Path.Combine(directoryName, fileName);
                }
            }
            else
            {
                pathToUse = path;
            }

            try
            {
                if (!File.Exists(pathToUse))
                {
                    log.Error("Configuration file is missing.  Please copy the file Config.js.example to Config.js and edit it to match your needs before running ACE.");
                    throw new Exception("missing configuration file");
                }

                log.Info($"Reading file at {pathToUse}");
                string fileText = File.ReadAllText(pathToUse);

                string redactedFileText = fileText;
                {
                    // In this block, we remove whitespace lines and comment lines. Pattern Breakdown:
                    // ^\s*(?://|#).*$\n? : Matches lines starting with whitespace + // or #
                    // |                  : OR
                    // ^\s*$\n?           : Matches lines containing only whitespace
                    // \n?                : Optionally matches the trailing newline to collapse the gap
                    string pattern = @"^\s*(?://|#).*$\n?|^\s*$\n?";
                    redactedFileText = Regex.Replace(redactedFileText, pattern, "", RegexOptions.Multiline);
                }
                {
                    // Redact secrets from the logs.
                    string secretKeys = "Password|ApiToken|DiscordToken|WebhookURL";
                    string pattern = $@"^(\s*[""']?(?:{secretKeys})[""']?\s*[:=]\s*[""']).*([""']\s*,?)$";
                    string replacement = @"$1******$2";
                    redactedFileText = Regex.Replace(redactedFileText, pattern, replacement, RegexOptions.Multiline);
                }
                log.Info($"Config file contents (trimmed blank lines and comments):\n{redactedFileText}");

                Config = JsonSerializer.Deserialize<MasterConfiguration>(fileText, SerializerOptions);
            }
            catch (Exception exception)
            {
                log.Error("An exception occured while loading the configuration file!", exception);
                // environment.exit swallows this exception for testing purposes.  we want to expose it.
                throw;
            }
        }

        public static JsonSerializerOptions SerializerOptions = new JsonSerializerOptions
        {
            AllowTrailingCommas = true,
            NumberHandling = JsonNumberHandling.AllowReadingFromString,
            ReadCommentHandling = JsonCommentHandling.Skip,
            WriteIndented = true
        };
    }
}
