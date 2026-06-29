using System;
using System.Collections.Generic;
using System.Configuration;

namespace Veloxap.AddIn.Erwin.Services
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
        private const string CatalogLocksUrlKey = "CatalogLocksUrl";
        private const string CatalogUnlockUrlKey = "CatalogUnlockUrl";
        private const string ApprovalStatusByCatalogUrlKey = "ApprovalStatusByCatalogUrl";
        private const string MartCatalogsUrlKey = "MartCatalogsUrl";
        private const string MartCatalogVersionsUrlKey = "MartCatalogVersionsUrl";
        private const string EnableModelComparisonKey = "EnableModelComparison";

        public static string GetApiBaseUrl()
        {
            return EnsureTrailingSlash(GetRequiredSetting(ApiBaseUrlKey));
        }

        public static string GetAuthLoginUrl()
        {
            return GetServiceUrl(AuthLoginUrlKey);
        }

        public static string GetAuthUsername()
        {
            return GetOptionalSetting(AuthUsernameKey);
        }

        public static string GetAuthPassword()
        {
            string configuredPassword = GetOptionalSetting(AuthPasswordKey);

            if (string.IsNullOrWhiteSpace(configuredPassword))
                return string.Empty;

            string plainPassword;
            return CryptoHelper.TryDecrypt(configuredPassword, out plainPassword)
                ? plainPassword
                : configuredPassword;
        }

        public static bool AreAuthCredentialsConfigured()
        {
            return !string.IsNullOrWhiteSpace(GetAuthUsername())
                && !string.IsNullOrWhiteSpace(GetAuthPassword());
        }

        public static string GetRulesByModelUrl()
        {
            return GetServiceUrl(RulesByModelUrlKey);
        }

        public static string GetAlterDdlUrl()
        {
            return GetServiceUrl(AlterDdlUrlKey);
        }

        public static string GetApprovalStartByCatalogUrl()
        {
            return GetServiceUrl(ApprovalStartByCatalogUrlKey);
        }

        public static string GetCatalogLocksUrl()
        {
            return GetServiceUrl(CatalogLocksUrlKey);
        }

        public static string GetCatalogUnlockUrl()
        {
            return GetServiceUrl(CatalogUnlockUrlKey);
        }

        public static string GetApprovalStatusByCatalogUrl()
        {
            return GetServiceUrl(ApprovalStatusByCatalogUrlKey);
        }

        public static string GetMartCatalogsUrl()
        {
            return GetServiceUrl(MartCatalogsUrlKey);
        }

        public static string GetMartCatalogVersionsUrl(string catalogId)
        {
            string serviceUrl = GetMartCatalogVersionsUrlTemplate();
            string safeCatalogId = (catalogId ?? string.Empty).Trim();

            return serviceUrl
                .Replace("{catalogId}", safeCatalogId)
                .Replace("{0}", safeCatalogId);
        }

        public static string GetMartCatalogVersionsUrlTemplate()
        {
            return GetServiceUrl(MartCatalogVersionsUrlKey);
        }

        public static List<string> GetAppSettingKeys()
        {
            return new List<string>
            {
                ApiBaseUrlKey,
                AuthLoginUrlKey,
                AuthUsernameKey,
                AuthPasswordKey,
                RulesByModelUrlKey,
                AlterDdlUrlKey,
                ApprovalStartByCatalogUrlKey,
                CatalogLocksUrlKey,
                CatalogUnlockUrlKey,
                ApprovalStatusByCatalogUrlKey,
                MartCatalogsUrlKey,
                MartCatalogVersionsUrlKey,
                EnableModelComparisonKey
            };
        }

        public static bool IsModelComparisonEnabled()
        {
            string configured = GetOptionalSetting(EnableModelComparisonKey);
            bool parsed;
            if (bool.TryParse(configured, out parsed))
                return parsed;

            return false;
        }

        private static string GetServiceUrl(string key)
        {
            string configuredValue = GetRequiredSetting(key);

            if (IsAbsoluteUrl(configuredValue))
                return configuredValue.Trim();

            return CombineUrl(GetApiBaseUrl(), configuredValue);
        }

        private static string GetRequiredSetting(string key)
        {
            string configuredValue = GetConfiguredSetting(key);

            if (string.IsNullOrWhiteSpace(configuredValue))
                throw new ConfigurationErrorsException("App.config appSettings '" + key + "' degeri eksik veya bos.");

            return configuredValue.Trim();
        }

        private static string GetOptionalSetting(string key)
        {
            string configuredValue = GetConfiguredSetting(key);
            return string.IsNullOrWhiteSpace(configuredValue)
                ? string.Empty
                : configuredValue.Trim();
        }

        private static string GetConfiguredSetting(string key)
        {
            string configuredValue = ReadHostAppSetting(key);

            if (string.IsNullOrWhiteSpace(configuredValue))
                configuredValue = ReadAssemblyAppSetting(key);

            return configuredValue;
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
            url = (url ?? string.Empty).Trim();

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
