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
}
