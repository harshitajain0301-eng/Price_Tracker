using System;
using System.Configuration;
using PriceDropCatcher.Properties;

namespace PriceDropCatcher.Services
{
    /// <summary>
    /// Resolves SerpAPI key: environment variable (SERPAPI_KEY), then user settings, then App.config.
    /// </summary>
    public static class SerpApiKeyResolver
    {
        public const string EnvVarPrimary = "SERPAPI_KEY";
        public const string EnvVarAlt = "SerpApiKey";

        public static string Resolve()
        {
            var env = Environment.GetEnvironmentVariable(EnvVarPrimary)
                      ?? Environment.GetEnvironmentVariable(EnvVarAlt);
            if (!string.IsNullOrWhiteSpace(env))
                return env.Trim();

            try
            {
                var user = Settings.Default.SerpApiKey;
                if (!string.IsNullOrWhiteSpace(user))
                    return user.Trim();
            }
            catch
            {
                // settings file missing / first run
            }

            var app = ConfigurationManager.AppSettings["SerpApiKey"];
            return string.IsNullOrWhiteSpace(app) ? "" : app.Trim();
        }

        public static bool IsFromEnvironment()
        {
            var env = Environment.GetEnvironmentVariable(EnvVarPrimary)
                      ?? Environment.GetEnvironmentVariable(EnvVarAlt);
            return !string.IsNullOrWhiteSpace(env);
        }
    }
}
