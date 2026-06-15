using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using VeloxapEDGEWpfLib.Models;

namespace VeloxapEDGEWpfLib.Pages
{
    /// <summary>
    /// Interaction logic for ValidationRulesView.xaml
    /// </summary>
    public partial class ValidationRulesView : UserControl
    {
        public ValidationRulesView()
            : this(null)
        {
        }

        internal ValidationRulesView(IEnumerable<Rule> validationRules)
        {
            InitializeComponent();
            LoadRules(validationRules);
        }

        internal void LoadRules(IEnumerable<Rule> validationRules)
        {
            var ruleItems = (validationRules ?? Enumerable.Empty<Rule>())
                .Select((rule, index) => BuildRuleItem(index + 1, rule))
                .Where(rule => rule != null)
                .ToList();

            dgValidationRules.ItemsSource = ruleItems;
            txtRuleCount.Text = ruleItems.Count + " kural";
            emptyState.Visibility = ruleItems.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        internal void ShowLoading()
        {
            dgValidationRules.ItemsSource = null;
            txtRuleCount.Text = "Yükleniyor...";
            emptyState.Visibility = Visibility.Collapsed;
        }

        internal void ShowError(string message)
        {
            dgValidationRules.ItemsSource = null;
            txtRuleCount.Text = "Hata";
            emptyState.Visibility = Visibility.Visible;
            emptyStateText.Text = string.IsNullOrWhiteSpace(message)
                ? "Validasyon kuralları yüklenemedi."
                : message;
        }

        internal void ShowTrace(string message)
        {
            txtTrace.Text = message ?? string.Empty;
            txtTrace.Visibility = string.IsNullOrWhiteSpace(message)
                ? Visibility.Collapsed
                : Visibility.Visible;
        }

        private static ValidationRuleListItem BuildRuleItem(int sequence, Rule rule)
        {
            if (rule == null)
                return null;

            string ruleText = Normalize(rule.RuleText);
            string triggerNodeType;
            string whenExpression;
            string checkExpression;

            string fallbackName = "Kural " + sequence;
            string fallbackDefinition = "Kural formatı tanımlı validasyon diline göre listeleniyor.";

            if (TryParseRuleParts(ruleText, out triggerNodeType, out whenExpression, out checkExpression))
            {
                fallbackName = triggerNodeType + " Kuralı";
                fallbackDefinition = BuildRuleDefinition(triggerNodeType, whenExpression, checkExpression);
            }

            return new ValidationRuleListItem
            {
                Id = rule.RuleId.ToString(),
                RuleName = string.IsNullOrWhiteSpace(rule.RuleName) ? fallbackName : rule.RuleName.Trim(),
                RuleDefinition = string.IsNullOrWhiteSpace(rule.RuleDefinition)
                    ? fallbackDefinition
                    : rule.RuleDefinition.Trim(),
                RuleText = ruleText
            };
        }

        private static string Normalize(string value)
        {
            return value == null ? string.Empty : value.Trim();
        }

        private static bool TryParseRuleParts(
            string ruleText,
            out string triggerNodeType,
            out string whenExpression,
            out string checkExpression)
        {
            triggerNodeType = string.Empty;
            whenExpression = string.Empty;
            checkExpression = string.Empty;

            if (string.IsNullOrWhiteSpace(ruleText))
                return false;

            const string whenPrefix = "WHEN ";
            const string checkMarker = " CHECK ";

            if (!ruleText.StartsWith(whenPrefix, StringComparison.OrdinalIgnoreCase))
                return false;

            int checkIndex = ruleText.IndexOf(checkMarker, StringComparison.OrdinalIgnoreCase);
            if (checkIndex < 0)
                return false;

            string whenPart = ruleText.Substring(whenPrefix.Length, checkIndex - whenPrefix.Length).Trim();
            checkExpression = ruleText.Substring(checkIndex + checkMarker.Length).Trim();

            if (string.IsNullOrWhiteSpace(whenPart) || string.IsNullOrWhiteSpace(checkExpression))
                return false;

            int firstSpaceIndex = whenPart.IndexOf(' ');
            if (firstSpaceIndex <= 0)
                return false;

            triggerNodeType = whenPart.Substring(0, firstSpaceIndex).Trim();
            whenExpression = whenPart.Substring(firstSpaceIndex + 1).Trim();

            return !string.IsNullOrWhiteSpace(triggerNodeType)
                && !string.IsNullOrWhiteSpace(whenExpression);
        }

        private static string BuildRuleDefinition(
            string triggerNodeType,
            string whenExpression,
            string checkExpression)
        {
            return triggerNodeType
                + " nesnesinde "
                + whenExpression
                + " koşulu sağlandığında "
                + checkExpression
                + " kontrol edilir.";
        }

        private sealed class ValidationRuleListItem
        {
            public string Id { get; set; }

            public string RuleName { get; set; }

            public string RuleDefinition { get; set; }

            public string RuleText { get; set; }
        }
    }
}
