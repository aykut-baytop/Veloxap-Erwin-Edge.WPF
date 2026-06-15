using System;
using System.Configuration;

namespace VeloxapEDGEWpfLib.Services
{
    internal static class RuleApiSettings
    {
        private const string AuthLoginUrlKey = "AuthLoginUrl";
        private const string AuthUsernameKey = "AuthUsername";
        private const string AuthPasswordKey = "AuthPassword";
        private const string RulesByModelUrlKey = "ValidationRulesByModelUrl";

        private const string DefaultAuthLoginUrl = "http://localhost:8181/api/auth/login";
        private const string DefaultAuthUsername = "mds";
        private const string DefaultAuthPassword = "Mdsap1234";
        private const string DefaultRulesByModelUrl = "http://localhost:8181/api/rules/by-model";

        public static string GetAuthLoginUrl()
        {
            return GetSetting(AuthLoginUrlKey, DefaultAuthLoginUrl);
        }

        public static string GetAuthUsername()
        {
            return GetSetting(AuthUsernameKey, DefaultAuthUsername);
        }

        public static string GetAuthPassword()
        {
            return GetSetting(AuthPasswordKey, DefaultAuthPassword);
        }

        public static string GetRulesByModelUrl()
        {
            return GetSetting(RulesByModelUrlKey, DefaultRulesByModelUrl);
        }

        private static string GetSetting(string key, string defaultValue)
        {
            string configuredValue = ReadHostAppSetting(key);

            if (string.IsNullOrWhiteSpace(configuredValue))
                configuredValue = ReadAssemblyAppSetting(key);

            return string.IsNullOrWhiteSpace(configuredValue)
                ? defaultValue
                : configuredValue.Trim();
        }

        private static string ReadHostAppSetting(string key)
        {
            try
            {
                return ConfigurationManager.AppSettings[key];
            }
            catch (ConfigurationErrorsException)
            {
                return null;
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static string ReadAssemblyAppSetting(string key)
        {
            try
            {
                var config = ConfigurationManager.OpenExeConfiguration(typeof(RuleApiSettings).Assembly.Location);
                var setting = config.AppSettings.Settings[key];
                return setting == null ? null : setting.Value;
            }
            catch (ConfigurationErrorsException)
            {
                return null;
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}
