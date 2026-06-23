using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

public static class RulePropertyExtractor
{
    // Sunlari yakalar:
    // SELF.Name
    // PARENT(Entity).Name
    // ROOT(Model).Model.Logical.MODEL_LEVEL_KVKK
    // DESCENDANT(Attribute).Physical_Data_Type
    private static readonly Regex PropertyRegex = new Regex(
        @"\b(?:SELF|PARENT\(\w+\)|ROOT\(\w+\)|DESCENDANT\(\w+\))\.([A-Za-z0-9_\.]+)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public static HashSet<string> ExtractProperties(IEnumerable<string> rules)
    {
        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (rules == null)
            return result;

        foreach (var rule in rules)
        {
            ExtractFromRule(rule, result);
        }

        return result;
    }

    public static HashSet<string> ExtractProperties(params string[] rules)
    {
        return ExtractProperties((IEnumerable<string>)rules);
    }

    private static void ExtractFromRule(string rule, HashSet<string> result)
    {
        if (string.IsNullOrWhiteSpace(rule))
            return;

        var matches = PropertyRegex.Matches(rule);

        foreach (Match match in matches)
        {
            if (!match.Success)
                continue;

            string propertyName = match.Groups[1].Value?.Trim();

            if (!string.IsNullOrWhiteSpace(propertyName))
            {
                result.Add(propertyName);
            }
        }
    }
}

/*
 * 
 * 
 * var rules = new List<string>
{
    "WHEN Attribute ROOT(Model).Model.Logical.MODEL_LEVEL_KVKK = 5 CHECK SELF.Attribute.Logical.KVKK_UDP !=",
    "WHEN Index SELF.Name != CHECK SELF.Name = CONCAT(IN_, PARENT(Entity).Name)",
    "WHEN Entity SELF.Entity.Physical.Tablo_Tipi = Fact CHECK EXISTS DESCENDANT(Attribute) ( SELF.Name = CREATE_DATE AND SELF.Physical_Data_Type = DATE )"
};

var properties = RulePropertyExtractor.ExtractProperties(rules);

foreach (var prop in properties)
{
    Console.WriteLine(prop);
}

Model.Logical.MODEL_LEVEL_KVKK
Attribute.Logical.KVKK_UDP
Name
Entity.Physical.Tablo_Tipi
Physical_Data_Type
 */