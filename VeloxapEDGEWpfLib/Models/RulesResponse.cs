using System;
using System.Collections.Generic;

namespace VeloxapEDGEWpfLib.Models
{
    public class RootObject
    {
        public DataObject Data { get; set; }

        public bool Success { get; set; }

        public string Message { get; set; }
    }

    public class DataObject
    {
        public int ModelId { get; set; }

        public List<Policy> Policies { get; set; }

        public string CName { get; set; }

        public string CLongId { get; set; }

        public int ResolvedLibraryId { get; set; }

        public Policy Policy { get; set; }
    }

    public class Policy
    {
        public int PolicyId { get; set; }

        public string PolicyName { get; set; }

        public string PolicyDescription { get; set; }

        public List<Rule> Rules { get; set; }
    }

    public class Rule
    {
        public int RuleId { get; set; }

        public int TypeId { get; set; }

        public int TechnologyId { get; set; }

        public int ObjectId { get; set; }

        public string RuleName { get; set; }

        public string RuleDefinition { get; set; }

        public int MessageTypesId { get; set; }

        public string Message { get; set; }

        public int Status { get; set; }

        public string CreatedBy { get; set; }

        public DateTime? CreatedDate { get; set; }

        public string ModifiedBy { get; set; }

        public DateTime? ModifiedDate { get; set; }

        public string RuleText { get; set; }
    }
}
