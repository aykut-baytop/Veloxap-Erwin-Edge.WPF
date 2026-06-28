using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using Veloxap.AddIn.Erwin.Models;

namespace Veloxap.AddIn.Erwin.Services
{
    internal sealed class RuleService
    {
        private readonly HttpClient httpClient;

        public RuleService(HttpClient httpClient)
        {
            this.httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        }

        public async Task<List<Rule>> GetRulesAsync(string serviceUrl)
        {
            if (string.IsNullOrWhiteSpace(serviceUrl))
                throw new ArgumentException("Kural servis URL'i bos olamaz.", nameof(serviceUrl));

            ApiTraceLogger.Info(
                "RULE REQUEST" + Environment.NewLine +
                "Url: " + serviceUrl);

            var response = await httpClient.GetAsync(serviceUrl).ConfigureAwait(false);
            string json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

            ApiTraceLogger.Info(
                "RULE RESPONSE" + Environment.NewLine +
                "Url: " + serviceUrl + Environment.NewLine +
                "Status: " + (int)response.StatusCode + " " + response.ReasonPhrase + Environment.NewLine +
                "BodyLength: " + (json == null ? 0 : json.Length) + Environment.NewLine +
                "BodyPreview: " + ApiTraceLogger.Truncate(json, 2000));

            response.EnsureSuccessStatusCode();

            var rules = ParseRules(json);

            ApiTraceLogger.Info(
                "RULE PARSE" + Environment.NewLine +
                "Url: " + serviceUrl + Environment.NewLine +
                "ParsedRuleCount: " + rules.Count);

            return rules;
        }

        public async Task<string> GetAlterDdlAsync(
            string serviceUrl,
            string path,
            int sourceVNo,
            int targetVNo)
        {
            if (string.IsNullOrWhiteSpace(serviceUrl))
                throw new ArgumentException("Alter DDL servis URL'i bos olamaz.", nameof(serviceUrl));

            if (string.IsNullOrWhiteSpace(path))
                throw new ArgumentException("Alter DDL path degeri bos olamaz.", nameof(path));

            var serializer = new JavaScriptSerializer
            {
                MaxJsonLength = int.MaxValue
            };

            string payload = serializer.Serialize(new Dictionary<string, object>
            {
                { "path", path },
                { "sourceVNo", sourceVNo },
                { "targetVNo", targetVNo }
            });

            ApiTraceLogger.Info(
                "ALTER DDL REQUEST" + Environment.NewLine +
                "Url: " + serviceUrl + Environment.NewLine +
                "Body: " + payload);

            using (var content = new StringContent(payload, Encoding.UTF8, "application/json"))
            {
                var response = await httpClient.PostAsync(serviceUrl, content).ConfigureAwait(false);
                string json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

                ApiTraceLogger.Info(
                    "ALTER DDL RESPONSE" + Environment.NewLine +
                    "Url: " + serviceUrl + Environment.NewLine +
                    "Status: " + (int)response.StatusCode + " " + response.ReasonPhrase + Environment.NewLine +
                    "BodyLength: " + (json == null ? 0 : json.Length) + Environment.NewLine +
                    "BodyPreview: " + ApiTraceLogger.Truncate(json, 2000));

                response.EnsureSuccessStatusCode();

                string ddl = ExtractDdl(json);

                ApiTraceLogger.Info(
                    "ALTER DDL PARSE" + Environment.NewLine +
                    "Url: " + serviceUrl + Environment.NewLine +
                    "DdlLength: " + (ddl == null ? 0 : ddl.Length));

                return ddl ?? string.Empty;
            }
        }

        public async Task<string> StartApprovalByCatalogAsync(
            string serviceUrl,
            string cName,
            string cLongId,
            int versionId,
            int targetVersionId,
            string description,
            string alterDdl)
        {
            if (string.IsNullOrWhiteSpace(serviceUrl))
                throw new ArgumentException("Approval servis URL'i bos olamaz.", nameof(serviceUrl));

            var serializer = new JavaScriptSerializer
            {
                MaxJsonLength = int.MaxValue
            };

            string payload = serializer.Serialize(new Dictionary<string, object>
            {
                { "cName", cName ?? string.Empty },
                { "cLongId", cLongId ?? string.Empty },
                { "versionId", versionId },
                { "targetVersionId", targetVersionId },
                { "description", description ?? string.Empty },
                { "alterDDL", alterDdl ?? string.Empty }
            });

            ApiTraceLogger.Info(
                "APPROVAL START REQUEST" + Environment.NewLine +
                "Url: " + serviceUrl + Environment.NewLine +
                "BodyLength: " + payload.Length + Environment.NewLine +
                "BodyPreview: " + ApiTraceLogger.Truncate(payload, 2000));

            using (var content = new StringContent(payload, Encoding.UTF8, "application/json"))
            {
                var response = await httpClient.PostAsync(serviceUrl, content).ConfigureAwait(false);
                string json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

                ApiTraceLogger.Info(
                    "APPROVAL START RESPONSE" + Environment.NewLine +
                    "Url: " + serviceUrl + Environment.NewLine +
                    "Status: " + (int)response.StatusCode + " " + response.ReasonPhrase + Environment.NewLine +
                    "BodyLength: " + (json == null ? 0 : json.Length) + Environment.NewLine +
                    "BodyPreview: " + ApiTraceLogger.Truncate(json, 2000));

                response.EnsureSuccessStatusCode();
                return json ?? string.Empty;
            }
        }

        public async Task<List<CatalogLockInfo>> GetCatalogLocksAsync(
            string serviceUrl,
            string cName,
            string cLongId)
        {
            if (string.IsNullOrWhiteSpace(serviceUrl))
                throw new ArgumentException("Catalog lock servis URL'i bos olamaz.", nameof(serviceUrl));

            string requestUrl = BuildCatalogQueryUrl(serviceUrl, cName, cLongId);

            ApiTraceLogger.Info(
                "CATALOG LOCKS REQUEST" + Environment.NewLine +
                "Url: " + requestUrl);

            var response = await httpClient.GetAsync(requestUrl).ConfigureAwait(false);
            string json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

            ApiTraceLogger.Info(
                "CATALOG LOCKS RESPONSE" + Environment.NewLine +
                "Url: " + requestUrl + Environment.NewLine +
                "Status: " + (int)response.StatusCode + " " + response.ReasonPhrase + Environment.NewLine +
                "BodyLength: " + (json == null ? 0 : json.Length) + Environment.NewLine +
                "BodyPreview: " + ApiTraceLogger.Truncate(json, 2000));

            response.EnsureSuccessStatusCode();

            var locks = ParseCatalogLocks(json);

            ApiTraceLogger.Info(
                "CATALOG LOCKS PARSE" + Environment.NewLine +
                "Url: " + requestUrl + Environment.NewLine +
                "ParsedLockCount: " + locks.Count);

            return locks;
        }

        public async Task<CatalogApprovalStatus> GetApprovalStatusByCatalogAsync(
            string serviceUrl,
            string cName,
            string cLongId)
        {
            if (string.IsNullOrWhiteSpace(serviceUrl))
                throw new ArgumentException("Approval status servis URL'i bos olamaz.", nameof(serviceUrl));

            string requestUrl = BuildCatalogQueryUrl(serviceUrl, cName, cLongId);

            ApiTraceLogger.Info(
                "APPROVAL STATUS REQUEST" + Environment.NewLine +
                "Url: " + requestUrl);

            var response = await httpClient.GetAsync(requestUrl).ConfigureAwait(false);
            string json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

            ApiTraceLogger.Info(
                "APPROVAL STATUS RESPONSE" + Environment.NewLine +
                "Url: " + requestUrl + Environment.NewLine +
                "Status: " + (int)response.StatusCode + " " + response.ReasonPhrase + Environment.NewLine +
                "BodyLength: " + (json == null ? 0 : json.Length) + Environment.NewLine +
                "BodyPreview: " + ApiTraceLogger.Truncate(json, 2000));

            response.EnsureSuccessStatusCode();

            var status = ParseApprovalStatus(json);

            ApiTraceLogger.Info(
                "APPROVAL STATUS PARSE" + Environment.NewLine +
                "Url: " + requestUrl + Environment.NewLine +
                "Status: " + (status == null ? string.Empty : status.Status) + Environment.NewLine +
                "StepCount: " + (status == null || status.Steps == null ? 0 : status.Steps.Count));

            return status ?? new CatalogApprovalStatus();
        }

        public async Task<CatalogVersionOwnerInfo> GetCatalogVersionOwnerAsync(
            string catalogsUrl,
            string catalogVersionsUrl,
            string username,
            string cLongId,
            string versionName,
            string versionNumber)
        {
            if (string.IsNullOrWhiteSpace(catalogsUrl))
                throw new ArgumentException("Mart catalog servis URL'i bos olamaz.", nameof(catalogsUrl));

            if (string.IsNullOrWhiteSpace(catalogVersionsUrl))
                throw new ArgumentException("Mart catalog version servis URL'i bos olamaz.", nameof(catalogVersionsUrl));

            if (string.IsNullOrWhiteSpace(username))
                throw new ArgumentException("Mart catalog kullanici adi bos olamaz.", nameof(username));

            if (string.IsNullOrWhiteSpace(cLongId))
                throw new ArgumentException("cLongId bos olamaz.", nameof(cLongId));

            string catalogsRequestUrl = BuildSingleQueryUrl(catalogsUrl, "username", username);

            ApiTraceLogger.Info(
                "MART CATALOGS REQUEST" + Environment.NewLine +
                "Url: " + catalogsRequestUrl);

            var catalogsResponse = await httpClient.GetAsync(catalogsRequestUrl).ConfigureAwait(false);
            string catalogsJson = await catalogsResponse.Content.ReadAsStringAsync().ConfigureAwait(false);

            ApiTraceLogger.Info(
                "MART CATALOGS RESPONSE" + Environment.NewLine +
                "Url: " + catalogsRequestUrl + Environment.NewLine +
                "Status: " + (int)catalogsResponse.StatusCode + " " + catalogsResponse.ReasonPhrase + Environment.NewLine +
                "BodyLength: " + (catalogsJson == null ? 0 : catalogsJson.Length) + Environment.NewLine +
                "BodyPreview: " + ApiTraceLogger.Truncate(catalogsJson, 2000));

            catalogsResponse.EnsureSuccessStatusCode();

            CatalogItem catalog = FindCatalogByLongId(DeserializeJson(catalogsJson), cLongId);
            if (catalog == null || string.IsNullOrWhiteSpace(catalog.Id))
                throw new InvalidOperationException("Secili cLongId ile eslesen mart catalog bulunamadi.");

            ApiTraceLogger.Info(
                "MART CATALOG MATCH" + Environment.NewLine +
                "CatalogId: " + catalog.Id + Environment.NewLine +
                "CatalogName: " + (catalog.Name ?? string.Empty));

            string versionsRequestUrl = BuildCatalogVersionsUrl(catalogVersionsUrl, catalog.Id);

            ApiTraceLogger.Info(
                "MART CATALOG VERSIONS REQUEST" + Environment.NewLine +
                "CatalogId: " + catalog.Id + Environment.NewLine +
                "Url: " + versionsRequestUrl);

            var versionsResponse = await httpClient.GetAsync(versionsRequestUrl).ConfigureAwait(false);
            string versionsJson = await versionsResponse.Content.ReadAsStringAsync().ConfigureAwait(false);

            ApiTraceLogger.Info(
                "MART CATALOG VERSIONS RESPONSE" + Environment.NewLine +
                "Url: " + versionsRequestUrl + Environment.NewLine +
                "Status: " + (int)versionsResponse.StatusCode + " " + versionsResponse.ReasonPhrase + Environment.NewLine +
                "BodyLength: " + (versionsJson == null ? 0 : versionsJson.Length) + Environment.NewLine +
                "BodyPreview: " + ApiTraceLogger.Truncate(versionsJson, 2000));

            versionsResponse.EnsureSuccessStatusCode();

            List<CatalogVersionInfo> versions = ResolveCatalogVersions(DeserializeJson(versionsJson)).ToList();
            CatalogVersionInfo version = FindCatalogVersion(versions, cLongId, versionName, versionNumber);
            if (version == null)
                throw new InvalidOperationException("Secili version icin mart catalog version kaydi bulunamadi.");

            ApiTraceLogger.Info(
                "MART CATALOG VERSION OWNER PARSE" + Environment.NewLine +
                "CatalogId: " + catalog.Id + Environment.NewLine +
                "VersionId: " + (version.Id ?? string.Empty) + Environment.NewLine +
                "VersionName: " + (version.Name ?? string.Empty) + Environment.NewLine +
                "CreatedBy: " + (version.CreatedBy ?? string.Empty));

            return new CatalogVersionOwnerInfo
            {
                CatalogId = catalog.Id,
                CatalogName = catalog.Name,
                VersionId = version.Id,
                VersionName = version.Name,
                VersionNumber = version.VersionNumber,
                CLongId = version.CLongId,
                CreatedBy = version.CreatedBy
            };
        }

        private static string BuildCatalogQueryUrl(string baseUrl, string cName, string cLongId)
        {
            string separator = baseUrl.Contains("?")
                ? (baseUrl.EndsWith("?") || baseUrl.EndsWith("&") ? string.Empty : "&")
                : "?";

            return baseUrl
                + separator
                + "cName="
                + Uri.EscapeDataString(cName ?? string.Empty)
                + "&cLongId="
                + Uri.EscapeDataString(cLongId ?? string.Empty);
        }

        private static string BuildSingleQueryUrl(string baseUrl, string key, string value)
        {
            string separator = baseUrl.Contains("?")
                ? (baseUrl.EndsWith("?") || baseUrl.EndsWith("&") ? string.Empty : "&")
                : "?";

            return baseUrl
                + separator
                + Uri.EscapeDataString(key ?? string.Empty)
                + "="
                + Uri.EscapeDataString(value ?? string.Empty);
        }

        private static string BuildCatalogVersionsUrl(string catalogVersionsUrl, string catalogId)
        {
            string safeCatalogId = (catalogId ?? string.Empty).Trim();

            return (catalogVersionsUrl ?? string.Empty)
                .Replace("{catalogId}", safeCatalogId)
                .Replace("{0}", safeCatalogId);
        }

        private static string ExtractDdl(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
                return string.Empty;

            try
            {
                var serializer = new JavaScriptSerializer
                {
                    MaxJsonLength = int.MaxValue
                };

                object payload = serializer.DeserializeObject(json);
                var directDdl = payload as string;
                if (!string.IsNullOrWhiteSpace(directDdl))
                    return directDdl;

                string ddl = ResolveDdl(payload);
                return ddl ?? string.Empty;
            }
            catch (ArgumentException)
            {
                return ExtractDdlFromRawJson(json);
            }
            catch (InvalidOperationException)
            {
                return ExtractDdlFromRawJson(json);
            }
        }

        private static string ResolveDdl(object payload)
        {
            var dictionary = payload as Dictionary<string, object>;
            if (dictionary != null)
            {
                object ddlValue = GetDictionaryValue(
                    dictionary,
                    "ddl",
                    "DDL",
                    "alterDdl",
                    "AlterDDL");

                if (ddlValue != null)
                    return Convert.ToString(ddlValue);

                foreach (var value in dictionary.Values)
                {
                    string ddl = ResolveDdl(value);
                    if (!string.IsNullOrWhiteSpace(ddl))
                        return ddl;
                }
            }

            var array = payload as object[];
            if (array != null)
            {
                foreach (var value in array)
                {
                    string ddl = ResolveDdl(value);
                    if (!string.IsNullOrWhiteSpace(ddl))
                        return ddl;
                }
            }

            return null;
        }

        private static string ExtractDdlFromRawJson(string json)
        {
            var match = Regex.Match(
                json ?? string.Empty,
                @"""ddl""\s*:\s*""(?<ddl>(?:\\.|[^""\\])*)""",
                RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.CultureInvariant);

            return match.Success
                ? DecodeJsonStringValue(match.Groups["ddl"].Value)
                : string.Empty;
        }

        private static string DecodeJsonStringValue(string value)
        {
            if (string.IsNullOrEmpty(value))
                return string.Empty;

            var builder = new StringBuilder(value.Length);

            for (int i = 0; i < value.Length; i++)
            {
                char current = value[i];
                if (current != '\\' || i == value.Length - 1)
                {
                    builder.Append(current);
                    continue;
                }

                char next = value[++i];
                switch (next)
                {
                    case '"':
                        builder.Append('"');
                        break;
                    case '\\':
                        builder.Append('\\');
                        break;
                    case '/':
                        builder.Append('/');
                        break;
                    case 'b':
                        builder.Append('\b');
                        break;
                    case 'f':
                        builder.Append('\f');
                        break;
                    case 'n':
                        builder.Append('\n');
                        break;
                    case 'r':
                        builder.Append('\r');
                        break;
                    case 't':
                        builder.Append('\t');
                        break;
                    case 'u':
                        if (i + 4 < value.Length &&
                            int.TryParse(
                                value.Substring(i + 1, 4),
                                NumberStyles.HexNumber,
                                CultureInfo.InvariantCulture,
                                out int codePoint))
                        {
                            builder.Append((char)codePoint);
                            i += 4;
                        }
                        else
                        {
                            builder.Append("\\u");
                        }

                        break;
                    default:
                        builder.Append('\\');
                        builder.Append(next);
                        break;
                }
            }

            return builder.ToString();
        }

        private static List<CatalogLockInfo> ParseCatalogLocks(string json)
        {
            object payload = DeserializeJson(json);
            return ResolveCatalogLockItems(payload)
                .Select(ToCatalogLockInfo)
                .Where(item => item != null)
                .ToList();
        }

        private static IEnumerable<object> ResolveCatalogLockItems(object payload)
        {
            var array = payload as object[];
            if (array != null)
            {
                var directLocks = array
                    .OfType<Dictionary<string, object>>()
                    .Where(IsCatalogLockDictionary)
                    .Cast<object>()
                    .ToList();

                if (directLocks.Count > 0)
                    return directLocks;

                var nestedLocks = new List<object>();
                foreach (var value in array)
                    nestedLocks.AddRange(ResolveCatalogLockItems(value));

                return nestedLocks;
            }

            var dictionary = payload as Dictionary<string, object>;
            if (dictionary != null)
            {
                if (IsCatalogLockDictionary(dictionary))
                    return new[] { payload };

                object locksValue = GetDictionaryValue(dictionary, "getCatalogLocks", "locks", "catalogLocks");
                var locks = ResolveCatalogLockItems(locksValue).ToList();
                if (locks.Count > 0)
                    return locks;

                foreach (var value in dictionary.Values)
                {
                    var nestedLocks = ResolveCatalogLockItems(value).ToList();
                    if (nestedLocks.Count > 0)
                        return nestedLocks;
                }

                return Enumerable.Empty<object>();
            }

            var nestedPayload = DeserializeNestedJson(payload as string);
            return nestedPayload == null
                ? Enumerable.Empty<object>()
                : ResolveCatalogLockItems(nestedPayload);
        }

        private static bool IsCatalogLockDictionary(Dictionary<string, object> dictionary)
        {
            return GetDictionaryValue(dictionary, "lockId", "lock", "session", "sessionId", "time") != null;
        }

        private static CatalogLockInfo ToCatalogLockInfo(object item)
        {
            var dictionary = item as Dictionary<string, object>;
            if (dictionary == null)
                return null;

            var lockInfo = new CatalogLockInfo
            {
                LockId = GetString(dictionary, "lockId", "id"),
                Session = GetString(dictionary, "session", "user", "userName"),
                Lock = GetString(dictionary, "lock", "lockType", "type"),
                Time = GetString(dictionary, "time", "createdAt", "createdDate"),
                SessionId = GetString(dictionary, "sessionId")
            };

            return string.IsNullOrWhiteSpace(lockInfo.LockId)
                && string.IsNullOrWhiteSpace(lockInfo.Session)
                && string.IsNullOrWhiteSpace(lockInfo.Lock)
                && string.IsNullOrWhiteSpace(lockInfo.Time)
                ? null
                : lockInfo;
        }

        private static CatalogApprovalStatus ParseApprovalStatus(string json)
        {
            object payload = DeserializeJson(json);
            var status = new CatalogApprovalStatus
            {
                Status = ResolveApprovalStatusText(payload),
                Message = ResolveApprovalMessageText(payload),
                StepText = ResolveApprovalStepText(payload)
            };

            foreach (var step in ResolveApprovalStepItems(payload)
                .Select(ToApprovalStep)
                .Where(step => step != null))
            {
                step.StepNumber = status.Steps.Count + 1;
                if (string.IsNullOrWhiteSpace(step.StepName))
                    step.StepName = "Step " + step.StepNumber;

                status.Steps.Add(step);
            }

            AddCurrentApprovalStep(status, payload);

            return status;
        }

        private static CatalogItem FindCatalogByLongId(object payload, string cLongId)
        {
            var dictionary = payload as Dictionary<string, object>;
            if (dictionary != null)
            {
                string itemLongId = GetString(dictionary, "cLongId", "CLongId");
                if (LongIdsMatch(itemLongId, cLongId))
                {
                    return new CatalogItem
                    {
                        Id = GetString(dictionary, "id", "catalogId", "modelId"),
                        Name = GetString(dictionary, "name", "catalogName"),
                        CLongId = itemLongId
                    };
                }

                foreach (string key in new[] { "data", "subFolders", "children", "items", "folders" })
                {
                    CatalogItem nestedItem = FindCatalogByLongId(GetDictionaryValue(dictionary, key), cLongId);
                    if (nestedItem != null)
                        return nestedItem;
                }

                foreach (var value in dictionary.Values)
                {
                    CatalogItem nestedItem = FindCatalogByLongId(value, cLongId);
                    if (nestedItem != null)
                        return nestedItem;
                }

                return null;
            }

            var array = payload as object[];
            if (array != null)
            {
                foreach (var value in array)
                {
                    CatalogItem nestedItem = FindCatalogByLongId(value, cLongId);
                    if (nestedItem != null)
                        return nestedItem;
                }
            }

            var nestedPayload = DeserializeNestedJson(payload as string);
            return nestedPayload == null
                ? null
                : FindCatalogByLongId(nestedPayload, cLongId);
        }

        private static IEnumerable<CatalogVersionInfo> ResolveCatalogVersions(object payload)
        {
            var array = payload as object[];
            if (array != null)
            {
                foreach (var value in array)
                {
                    foreach (CatalogVersionInfo version in ResolveCatalogVersions(value))
                        yield return version;
                }

                yield break;
            }

            var dictionary = payload as Dictionary<string, object>;
            if (dictionary != null)
            {
                if (IsCatalogVersionDictionary(dictionary))
                {
                    yield return ToCatalogVersionInfo(dictionary);
                    yield break;
                }

                foreach (string key in new[] { "data", "versions", "items", "result" })
                {
                    foreach (CatalogVersionInfo version in ResolveCatalogVersions(GetDictionaryValue(dictionary, key)))
                        yield return version;
                }

                yield break;
            }

            var nestedPayload = DeserializeNestedJson(payload as string);
            if (nestedPayload == null)
                yield break;

            foreach (CatalogVersionInfo version in ResolveCatalogVersions(nestedPayload))
                yield return version;
        }

        private static bool IsCatalogVersionDictionary(Dictionary<string, object> dictionary)
        {
            return GetDictionaryValue(dictionary, "versionNumber", "createdBy") != null
                && GetDictionaryValue(dictionary, "id", "name", "cLongId") != null;
        }

        private static CatalogVersionInfo ToCatalogVersionInfo(Dictionary<string, object> dictionary)
        {
            return new CatalogVersionInfo
            {
                Id = GetString(dictionary, "id", "versionId"),
                Name = GetString(dictionary, "name", "versionName"),
                CreatedBy = GetString(dictionary, "createdBy", "creator", "owner"),
                CreatedDate = GetString(dictionary, "createdDate"),
                Path = GetString(dictionary, "path"),
                VersionNumber = GetString(dictionary, "versionNumber", "versionNo"),
                CLongId = GetString(dictionary, "cLongId", "CLongId")
            };
        }

        private static CatalogVersionInfo FindCatalogVersion(
            IEnumerable<CatalogVersionInfo> versions,
            string cLongId,
            string versionName,
            string versionNumber)
        {
            var versionList = (versions ?? Enumerable.Empty<CatalogVersionInfo>())
                .Where(version => version != null)
                .ToList();

            CatalogVersionInfo match = versionList.FirstOrDefault(
                version => LongIdsMatch(version.CLongId, cLongId));

            if (match != null)
                return match;

            string normalizedVersionNumber = NormalizeText(versionNumber);
            if (!string.IsNullOrWhiteSpace(normalizedVersionNumber))
            {
                match = versionList.FirstOrDefault(version =>
                    string.Equals(
                        NormalizeText(version.VersionNumber),
                        normalizedVersionNumber,
                        StringComparison.OrdinalIgnoreCase));

                if (match != null)
                    return match;
            }

            string normalizedVersionName = NormalizeText(versionName);
            if (!string.IsNullOrWhiteSpace(normalizedVersionName))
            {
                match = versionList.FirstOrDefault(version =>
                    string.Equals(
                        NormalizeText(version.Name),
                        normalizedVersionName,
                        StringComparison.OrdinalIgnoreCase));

                if (match != null)
                    return match;
            }

            return null;
        }

        private static bool LongIdsMatch(string left, string right)
        {
            string normalizedLeft = NormalizeLongId(left);
            string normalizedRight = NormalizeLongId(right);

            if (string.IsNullOrWhiteSpace(normalizedLeft) ||
                string.IsNullOrWhiteSpace(normalizedRight))
            {
                return false;
            }

            return string.Equals(normalizedLeft, normalizedRight, StringComparison.OrdinalIgnoreCase);
        }

        private static string NormalizeLongId(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return string.Empty;

            return new string(value.Where(character => !char.IsWhiteSpace(character)).ToArray());
        }

        private static string NormalizeText(string value)
        {
            return string.IsNullOrWhiteSpace(value)
                ? string.Empty
                : value.Trim();
        }

        private static string ResolveApprovalStatusText(object payload)
        {
            var dictionary = payload as Dictionary<string, object>;
            if (dictionary != null)
            {
                string status = GetString(
                    dictionary,
                    "approvalStatus",
                    "approvalState",
                    "statusName",
                    "approvalProcessText",
                    "currentStatus",
                    "state",
                    "status");

                if (!string.IsNullOrWhiteSpace(status))
                    return status;

                foreach (var value in dictionary.Values)
                {
                    status = ResolveApprovalStatusText(value);
                    if (!string.IsNullOrWhiteSpace(status))
                        return status;
                }
            }

            var array = payload as object[];
            if (array != null)
            {
                foreach (var value in array)
                {
                    string status = ResolveApprovalStatusText(value);
                    if (!string.IsNullOrWhiteSpace(status))
                        return status;
                }
            }

            var nestedPayload = DeserializeNestedJson(payload as string);
            return nestedPayload == null
                ? string.Empty
                : ResolveApprovalStatusText(nestedPayload);
        }

        private static IEnumerable<object> ResolveApprovalStepItems(object payload)
        {
            var array = payload as object[];
            if (array != null)
            {
                var steps = new List<object>();
                foreach (var value in array)
                    steps.AddRange(ResolveApprovalStepItems(value));

                return steps;
            }

            var dictionary = payload as Dictionary<string, object>;
            if (dictionary != null)
            {
                if (IsApprovalStepDictionary(dictionary))
                    return new[] { payload };

                var directSteps = new List<object>();
                foreach (var key in new[]
                {
                    "steps",
                    "approvalSteps",
                    "history",
                    "approvalHistory",
                    "pendingApprovals",
                    "approvers",
                    "items",
                    "getApprovalStatus",
                    "getApprovalSteps"
                })
                {
                    directSteps.AddRange(ResolveApprovalStepItems(GetDictionaryValue(dictionary, key)));
                }

                if (directSteps.Count > 0)
                    return directSteps;

                foreach (var pair in dictionary)
                {
                    if (string.Equals(pair.Key, "currentApprovers", StringComparison.OrdinalIgnoreCase))
                        continue;

                    var nestedSteps = ResolveApprovalStepItems(pair.Value).ToList();
                    if (nestedSteps.Count > 0)
                        return nestedSteps;
                }

                return Enumerable.Empty<object>();
            }

            var nestedPayload = DeserializeNestedJson(payload as string);
            return nestedPayload == null
                ? Enumerable.Empty<object>()
                : ResolveApprovalStepItems(nestedPayload);
        }

        private static bool IsApprovalStepDictionary(Dictionary<string, object> dictionary)
        {
            return GetDictionaryValue(
                dictionary,
                "actionDate",
                "approvedBy",
                "rejectedBy",
                "pendingApprover",
                "pendingApproverName",
                "approver",
                "approverName",
                "assignee",
                "assigneeName",
                "user",
                "userName") != null;
        }

        private static CatalogApprovalStep ToApprovalStep(object item)
        {
            var dictionary = item as Dictionary<string, object>;
            if (dictionary == null)
                return null;

            var step = new CatalogApprovalStep
            {
                StepName = GetString(dictionary, "stepName", "step", "level", "sequence", "order", "currentApprovalOrder"),
                ApproverName = GetString(
                    dictionary,
                    "pendingApprover",
                    "pendingApproverName",
                    "approver",
                    "approverName",
                    "assignee",
                    "assigneeName",
                    "user",
                    "userName",
                    "username",
                    "displayName",
                    "fullName",
                    "approvedBy",
                    "rejectedBy"),
                Status = GetString(dictionary, "status", "state", "approvalStatus", "statusName"),
                GroupName = GetString(dictionary, "groupName", "currentGroupName", "approvalGroupName"),
                Message = GetString(dictionary, "message", "waitingAtText", "description", "comment", "actionDescription")
            };

            if (string.IsNullOrWhiteSpace(step.ApproverName))
                step.ApproverName = GetString(dictionary, "name");

            if (string.IsNullOrWhiteSpace(step.StepName))
                step.StepName = GetString(dictionary, "stepText");

            return string.IsNullOrWhiteSpace(step.StepName)
                && string.IsNullOrWhiteSpace(step.ApproverName)
                && string.IsNullOrWhiteSpace(step.Status)
                && string.IsNullOrWhiteSpace(step.GroupName)
                && string.IsNullOrWhiteSpace(step.Message)
                ? null
                : step;
        }

        private static void AddCurrentApprovalStep(CatalogApprovalStatus status, object payload)
        {
            if (status == null)
                return;

            var dictionary = FindApprovalDataDictionary(payload);
            if (dictionary == null)
                return;

            string currentMessage = GetString(dictionary, "waitingAtText", "message");
            string currentGroup = GetString(dictionary, "currentGroupName", "groupName");
            string currentStatus = GetString(dictionary, "statusName", "status", "approvalStatus");
            string currentOrder = GetString(dictionary, "currentApprovalOrder", "stepText");
            string approvers = BuildApproverNames(GetDictionaryValue(dictionary, "currentApprovers"));

            bool hasCurrentStep =
                !string.IsNullOrWhiteSpace(currentMessage) ||
                !string.IsNullOrWhiteSpace(currentGroup) ||
                !string.IsNullOrWhiteSpace(approvers);

            if (!hasCurrentStep)
                return;

            bool alreadyExists = status.Steps.Any(step =>
                step != null &&
                string.Equals(step.Message, currentMessage, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(step.GroupName, currentGroup, StringComparison.OrdinalIgnoreCase));

            if (alreadyExists)
                return;

            var currentStep = new CatalogApprovalStep
            {
                StepNumber = status.Steps.Count + 1,
                StepName = string.IsNullOrWhiteSpace(currentOrder) ? "Current Step" : "Step " + currentOrder,
                ApproverName = approvers,
                Status = currentStatus,
                GroupName = currentGroup,
                Message = currentMessage
            };

            status.Steps.Add(currentStep);
        }

        private static Dictionary<string, object> FindApprovalDataDictionary(object payload)
        {
            var dictionary = payload as Dictionary<string, object>;
            if (dictionary != null)
            {
                if (GetDictionaryValue(
                    dictionary,
                    "approvalProcessStarted",
                    "currentApprovalOrder",
                    "currentApprovers",
                    "workflowId") != null)
                {
                    return dictionary;
                }

                object data = GetDictionaryValue(dictionary, "data", "result", "approval");
                var dataDictionary = FindApprovalDataDictionary(data);
                if (dataDictionary != null)
                    return dataDictionary;

                foreach (var value in dictionary.Values)
                {
                    dataDictionary = FindApprovalDataDictionary(value);
                    if (dataDictionary != null)
                        return dataDictionary;
                }
            }

            var array = payload as object[];
            if (array != null)
            {
                foreach (var value in array)
                {
                    var dataDictionary = FindApprovalDataDictionary(value);
                    if (dataDictionary != null)
                        return dataDictionary;
                }
            }

            var nestedPayload = DeserializeNestedJson(payload as string);
            return nestedPayload == null
                ? null
                : FindApprovalDataDictionary(nestedPayload);
        }

        private static string ResolveApprovalMessageText(object payload)
        {
            var dictionary = FindApprovalDataDictionary(payload);
            if (dictionary == null)
                return string.Empty;

            return GetString(dictionary, "waitingAtText", "message", "approvalProcessText");
        }

        private static string ResolveApprovalStepText(object payload)
        {
            var dictionary = FindApprovalDataDictionary(payload);
            if (dictionary == null)
                return string.Empty;

            return GetString(dictionary, "stepText");
        }

        private static string BuildApproverNames(object value)
        {
            var names = ResolveApproverNames(value)
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            return names.Count == 0
                ? string.Empty
                : string.Join(", ", names);
        }

        private static IEnumerable<string> ResolveApproverNames(object value)
        {
            var array = value as object[];
            if (array != null)
            {
                foreach (var item in array)
                {
                    foreach (string name in ResolveApproverNames(item))
                        yield return name;
                }

                yield break;
            }

            var dictionary = value as Dictionary<string, object>;
            if (dictionary != null)
            {
                string name = GetString(dictionary, "displayName", "username", "userName", "name", "fullName");
                if (!string.IsNullOrWhiteSpace(name))
                    yield return name;

                yield break;
            }

            string text = value == null ? string.Empty : Convert.ToString(value);
            if (!string.IsNullOrWhiteSpace(text))
                yield return text;
        }

        private static object DeserializeJson(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
                return null;

            try
            {
                var serializer = new JavaScriptSerializer
                {
                    MaxJsonLength = int.MaxValue
                };

                return serializer.DeserializeObject(json);
            }
            catch (ArgumentException)
            {
                return DeserializeNestedJson(json);
            }
            catch (InvalidOperationException)
            {
                return DeserializeNestedJson(json);
            }
        }

        private static object DeserializeNestedJson(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return null;

            string json = value.Trim();
            int pipeIndex = json.IndexOf('|');
            if (pipeIndex >= 0 && pipeIndex < json.Length - 1)
                json = json.Substring(pipeIndex + 1).Trim();

            if (!json.StartsWith("{", StringComparison.Ordinal) &&
                !json.StartsWith("[", StringComparison.Ordinal))
            {
                return null;
            }

            try
            {
                var serializer = new JavaScriptSerializer
                {
                    MaxJsonLength = int.MaxValue
                };

                return serializer.DeserializeObject(json);
            }
            catch (ArgumentException)
            {
                return null;
            }
            catch (InvalidOperationException)
            {
                return null;
            }
        }

        private static List<Rule> ParseRules(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
                return new List<Rule>();

            var serializer = new JavaScriptSerializer
            {
                MaxJsonLength = int.MaxValue
            };

            object payload = serializer.DeserializeObject(json);

            return ResolveRuleItems(payload)
                .Select(ToRule)
                .Where(rule => rule != null)
                .ToList();
        }

        private static IEnumerable<object> ResolveRuleItems(object payload)
        {
            var array = payload as object[];
            if (array != null)
            {
                if (array.OfType<Dictionary<string, object>>().Any(IsRuleDictionary))
                    return array;

                foreach (var value in array)
                {
                    var nestedItems = ResolveRuleItems(value).ToList();
                    if (nestedItems.Count > 0)
                        return nestedItems;
                }

                return Enumerable.Empty<object>();
            }

            var dictionary = payload as Dictionary<string, object>;
            if (dictionary == null)
                return Enumerable.Empty<object>();

            if (IsRuleDictionary(dictionary))
                return new[] { payload };

            object wrappedValue = GetDictionaryValue(dictionary, "rules", "items", "value");

            array = wrappedValue as object[];
            if (array != null)
                return array;

            var directItems = ResolveRuleItems(wrappedValue).ToList();
            if (directItems.Count > 0)
                return directItems;

            foreach (var key in new[] { "policy", "policies", "data", "result" })
            {
                var nestedValue = GetDictionaryValue(dictionary, key);
                var nestedItems = ResolveRuleItems(nestedValue).ToList();
                if (nestedItems.Count > 0)
                    return nestedItems;
            }

            return Enumerable.Empty<object>();
        }

        private static bool IsRuleDictionary(Dictionary<string, object> dictionary)
        {
            return GetDictionaryValue(
                dictionary,
                "ruleText",
                "RuleText",
                "rule",
                "Rule",
                "ruleId",
                "RuleId",
                "ruleName",
                "RuleName") != null;
        }

        private static Rule ToRule(object item)
        {
            var dictionary = item as Dictionary<string, object>;
            if (dictionary == null)
                return null;

            return new Rule
            {
                RuleId = GetInt(dictionary, "ruleId", "RuleId", "id", "Id"),
                TypeId = GetInt(dictionary, "typeId", "TypeId"),
                TechnologyId = GetInt(dictionary, "technologyId", "TechnologyId"),
                ObjectId = GetInt(dictionary, "objectId", "ObjectId"),
                RuleName = GetString(dictionary, "ruleName", "RuleName", "name", "Name"),
                RuleDefinition = GetString(dictionary, "ruleDefinition", "RuleDefinition", "definition", "Definition"),
                MessageTypesId = GetInt(dictionary, "messageTypesId", "MessageTypesId"),
                Message = GetString(dictionary, "message", "Message"),
                Status = GetInt(dictionary, "status", "Status"),
                CreatedBy = GetString(dictionary, "createdBy", "CreatedBy"),
                ModifiedBy = GetString(dictionary, "modifiedBy", "ModifiedBy"),
                RuleText = GetString(dictionary, "ruleText", "RuleText", "rule", "Rule")
            };
        }

        private static int GetInt(Dictionary<string, object> dictionary, params string[] keys)
        {
            object value = GetDictionaryValue(dictionary, keys);
            if (value == null)
                return 0;

            try
            {
                return Convert.ToInt32(value, CultureInfo.InvariantCulture);
            }
            catch (FormatException)
            {
                return 0;
            }
            catch (InvalidCastException)
            {
                return 0;
            }
            catch (OverflowException)
            {
                return 0;
            }
        }

        private static string GetString(Dictionary<string, object> dictionary, params string[] keys)
        {
            object value = GetDictionaryValue(dictionary, keys);
            return value == null ? string.Empty : Convert.ToString(value);
        }

        private static object GetDictionaryValue(Dictionary<string, object> dictionary, params string[] keys)
        {
            if (dictionary == null || keys == null)
                return null;

            foreach (var pair in dictionary)
            {
                if (keys.Any(key => string.Equals(pair.Key, key, StringComparison.OrdinalIgnoreCase)))
                    return pair.Value;
            }

            return null;
        }
    }

    internal sealed class CatalogLockInfo
    {
        public string LockId { get; set; }

        public string Session { get; set; }

        public string Lock { get; set; }

        public string Time { get; set; }

        public string SessionId { get; set; }
    }

    internal sealed class CatalogVersionOwnerInfo
    {
        public string CatalogId { get; set; }

        public string CatalogName { get; set; }

        public string VersionId { get; set; }

        public string VersionName { get; set; }

        public string VersionNumber { get; set; }

        public string CLongId { get; set; }

        public string CreatedBy { get; set; }
    }

    internal sealed class CatalogItem
    {
        public string Id { get; set; }

        public string Name { get; set; }

        public string CLongId { get; set; }
    }

    internal sealed class CatalogVersionInfo
    {
        public string Id { get; set; }

        public string Name { get; set; }

        public string CreatedBy { get; set; }

        public string CreatedDate { get; set; }

        public string Path { get; set; }

        public string VersionNumber { get; set; }

        public string CLongId { get; set; }
    }

    internal sealed class CatalogApprovalStatus
    {
        public CatalogApprovalStatus()
        {
            Steps = new List<CatalogApprovalStep>();
        }

        public string Status { get; set; }

        public string Message { get; set; }

        public string StepText { get; set; }

        public List<CatalogApprovalStep> Steps { get; private set; }
    }

    internal sealed class CatalogApprovalStep
    {
        public int StepNumber { get; set; }

        public string StepName { get; set; }

        public string ApproverName { get; set; }

        public string Status { get; set; }

        public string GroupName { get; set; }

        public string Message { get; set; }
    }
}
