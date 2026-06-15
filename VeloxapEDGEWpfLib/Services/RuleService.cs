using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using VeloxapEDGEWpfLib.Models;

namespace VeloxapEDGEWpfLib.Services
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
                throw new ArgumentException("Kural servis URL'i boş olamaz.", nameof(serviceUrl));

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

            var result = JsonSerializer.Deserialize<RootObject>(json);

            //var rules = ParseRules(json);

            ApiTraceLogger.Info(
                "RULE PARSE" + Environment.NewLine +
                "Url: " + serviceUrl + Environment.NewLine +
                "ParsedRuleCount: " + result.Data.Policy.Rules.Count);

            return result.Data.Policy.Rules;
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
                return array;

            var dictionary = payload as Dictionary<string, object>;
            if (dictionary == null)
                return Enumerable.Empty<object>();

            if (GetDictionaryValue(dictionary, "ruleText", "RuleText", "rule", "Rule") != null)
                return new[] { payload };

            object wrappedValue = GetDictionaryValue(
                dictionary,
                "rules",
                "data",
                "result",
                "items",
                "value");

            array = wrappedValue as object[];
            if (array != null)
                return array;

            return ResolveRuleItems(wrappedValue);
        }

        private static Rule ToRule(object item)
        {
            var dictionary = item as Dictionary<string, object>;
            if (dictionary == null)
                return null;

            return new Rule
            {
                RuleId = int.Parse(GetString(dictionary, "ruleId", "RuleId", "id", "Id")),
                RuleName = GetString(dictionary, "ruleName", "RuleName", "name", "Name"),
                RuleDefinition = GetString(dictionary, "ruleDefinition", "RuleDefinition", "definition", "Definition"),
                RuleText = GetString(dictionary, "ruleText", "RuleText", "rule", "Rule")
            };
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
