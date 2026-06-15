using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace VeloxapEDGEWpfLib.Models
{
    public class RootObject
    {
        [JsonPropertyName("data")]
        public DataObject Data { get; set; }

        [JsonPropertyName("success")]
        public bool Success { get; set; }

        [JsonPropertyName("message")]
        public string Message { get; set; }
    }

    public class DataObject
    {
        [JsonPropertyName("modelId")]
        public int ModelId { get; set; }

        [JsonPropertyName("policies")]
        public List<Policy> Policies { get; set; }

        [JsonPropertyName("cName")]
        public string CName { get; set; }

        [JsonPropertyName("cLongId")]
        public string CLongId { get; set; }

        [JsonPropertyName("resolvedLibraryId")]
        public int ResolvedLibraryId { get; set; }

        [JsonPropertyName("policy")]
        public Policy Policy { get; set; }
    }

    public class Policy
    {
        [JsonPropertyName("policyId")]
        public int PolicyId { get; set; }

        [JsonPropertyName("policyName")]
        public string PolicyName { get; set; }

        [JsonPropertyName("policyDescription")]
        public string PolicyDescription { get; set; }

        [JsonPropertyName("rules")]
        public List<Rule> Rules { get; set; }
    }

    public class Rule
    {
        [JsonPropertyName("ruleId")]
        public int RuleId { get; set; }

        [JsonPropertyName("typeId")]
        public int TypeId { get; set; }

        [JsonPropertyName("technologyId")]
        public int TechnologyId { get; set; }

        [JsonPropertyName("objectId")]
        public int ObjectId { get; set; }

        [JsonPropertyName("ruleName")]
        public string RuleName { get; set; }

        [JsonPropertyName("ruleDefinition")]
        public string RuleDefinition { get; set; }

        [JsonPropertyName("messageTypesId")]
        public int MessageTypesId { get; set; }

        [JsonPropertyName("message")]
        public string Message { get; set; }

        [JsonPropertyName("status")]
        public int Status { get; set; }

        [JsonPropertyName("createdBy")]
        public string CreatedBy { get; set; }

        [JsonPropertyName("createdDate")]
        public DateTime? CreatedDate { get; set; }

        [JsonPropertyName("modifiedBy")]
        public string ModifiedBy { get; set; }

        [JsonPropertyName("modifiedDate")]
        public DateTime? ModifiedDate { get; set; }

        [JsonPropertyName("rule")]
        public string RuleText { get; set; }
    }
}
