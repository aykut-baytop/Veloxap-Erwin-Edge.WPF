using System;
using System.Collections.Generic;
using System.Configuration;

namespace VeloxapEDGEWpfLib.Services
{
    internal static class RuleApiSettings
    {
        private const string ApiBaseUrlKey = "ApiBaseUrl";
        private const string AuthLoginUrlKey = "AuthLoginUrl";
        private const string AuthUsernameKey = "AuthUsername";
        private const string AuthPasswordKey = "AuthPassword";
        private const string RulesByModelUrlKey = "ValidationRulesByModelUrl";
        private const string AlterDdlUrlKey = "AlterDdlUrl";
        private const string ApprovalStartByCatalogUrlKey = "ApprovalStartByCatalogUrl";

        private const string DefaultApiBaseUrl = "http://localhost:8181/api/";
        private const string DefaultAuthLoginUrl = "auth/login";
        private const string DefaultAuthUsername = "mds";
        private const string DefaultAuthPassword = "Mdsap1234";
        private const string DefaultRulesByModelUrl = "rules/by-model";
        private const string DefaultAlterDdlUrl = "compare/alterDDL";
        private const string DefaultApprovalStartByCatalogUrl = "approval/start-by-catalog";

        public static string GetApiBaseUrl()
        {
            return EnsureTrailingSlash(GetSetting(ApiBaseUrlKey, DefaultApiBaseUrl));
        }

        public static string GetAuthLoginUrl()
        {
            return GetServiceUrl(AuthLoginUrlKey, DefaultAuthLoginUrl);
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
            return GetServiceUrl(RulesByModelUrlKey, DefaultRulesByModelUrl);
        }

        public static string GetAlterDdlUrl()
        {
            return GetServiceUrl(AlterDdlUrlKey, DefaultAlterDdlUrl);
        }

        public static string GetApprovalStartByCatalogUrl()
        {
            return GetServiceUrl(ApprovalStartByCatalogUrlKey, DefaultApprovalStartByCatalogUrl);
        }

        public static List<KeyValuePair<string, string>> GetDefaultAppSettings()
        {
            return new List<KeyValuePair<string, string>>
            {
                new KeyValuePair<string, string>(ApiBaseUrlKey, DefaultApiBaseUrl),
                new KeyValuePair<string, string>(AuthLoginUrlKey, DefaultAuthLoginUrl),
                new KeyValuePair<string, string>(AuthUsernameKey, DefaultAuthUsername),
                new KeyValuePair<string, string>(AuthPasswordKey, DefaultAuthPassword),
                new KeyValuePair<string, string>(RulesByModelUrlKey, DefaultRulesByModelUrl),
                new KeyValuePair<string, string>(AlterDdlUrlKey, DefaultAlterDdlUrl),
                new KeyValuePair<string, string>(ApprovalStartByCatalogUrlKey, DefaultApprovalStartByCatalogUrl)
            };
        }

        private static string GetServiceUrl(string key, string defaultEndpoint)
        {
            string configuredValue = GetSetting(key, defaultEndpoint);

            if (IsAbsoluteUrl(configuredValue))
                return configuredValue.Trim();

            return CombineUrl(GetApiBaseUrl(), configuredValue);
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

        private static string CombineUrl(string baseUrl, string endpoint)
        {
            baseUrl = EnsureTrailingSlash(baseUrl);
            endpoint = (endpoint ?? string.Empty).Trim();

            while (endpoint.StartsWith("/", StringComparison.Ordinal))
                endpoint = endpoint.Substring(1);

            if (string.IsNullOrWhiteSpace(endpoint))
                return baseUrl;

            return baseUrl + endpoint;
        }

        private static string EnsureTrailingSlash(string url)
        {
            url = string.IsNullOrWhiteSpace(url)
                ? DefaultApiBaseUrl
                : url.Trim();

            return url.EndsWith("/", StringComparison.Ordinal)
                ? url
                : url + "/";
        }

        private static bool IsAbsoluteUrl(string value)
        {
            Uri uri;
            return Uri.TryCreate(value, UriKind.Absolute, out uri)
                && !string.IsNullOrWhiteSpace(uri.Scheme)
                && (string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase));
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
