// Veloxap erwin cross-validation engine, revision 2.
// Replace the previous engine file; do not add both files to the same project.
// C# 7.3-compatible source. Uses the existing ModelInfo/ModelObject/ObjectProperty DTOs.
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Veloxap.AddIn.Erwin.Models;

namespace Veloxap.AddIn.Erwin.Services
{
    internal enum CrossIssueKind { RuleViolation, RuleError }
    internal enum CrossRuleStatus { Passed, Failed, Skipped, Error }
    internal enum CrossCheckScope { Self, Descendant, Parent, Root }

    internal sealed class CrossValidationOptions
    {
        public bool TextCaseSensitive { get; set; } = true;
        public bool RegexCaseSensitive { get; set; } = true;
        public TimeSpan RegexTimeout { get; set; } = TimeSpan.FromMilliseconds(250);
        public int MaxRegexPatternLength { get; set; } = 4096;
        public int MaxRegexInputLength { get; set; } = 1000000;
        public int MaxCachedRegexes { get; set; } = 512;
        public int MaxRuleLength { get; set; } = 32768;
        public int MaxExpressionDepth { get; set; } = 128;
        public int MaxModelDepth { get; set; } = 256;
        public bool MissingCheckTargetIsError { get; set; } = true;
        public bool RunParallel { get; set; } = true;
        public int? MaxParallelism { get; set; }

        internal CrossValidationOptions Snapshot()
        {
            var copy = (CrossValidationOptions)MemberwiseClone();
            if (copy.RegexTimeout.TotalMilliseconds < 1 ||
                copy.RegexTimeout.TotalMilliseconds > int.MaxValue - 1)
                throw new ArgumentOutOfRangeException(nameof(RegexTimeout), "Use a finite timeout of at least 1 ms.");
            if (copy.MaxRegexPatternLength < 1 || copy.MaxRegexInputLength < 1 ||
                copy.MaxCachedRegexes < 1 || copy.MaxRuleLength < 1 ||
                copy.MaxExpressionDepth < 8 || copy.MaxExpressionDepth > 256 ||
                copy.MaxModelDepth < 1 || copy.MaxModelDepth > 512)
                throw new ArgumentOutOfRangeException(nameof(CrossValidationOptions), "Invalid validation limits.");
            if (copy.MaxParallelism.HasValue && copy.MaxParallelism.Value < 1)
                throw new ArgumentOutOfRangeException(nameof(MaxParallelism));
            return copy;
        }
    }

    internal sealed class CrossConditionDetail
    {
        public string Expression { get; set; }
        public string ObjectPath { get; set; }
        public string PropertyName { get; set; }
        public string Operator { get; set; }
        public string ExpectedValue { get; set; }
        public string ActualValue { get; set; }
        public string Message { get; set; }
        internal ValidationNode ContextNode { get; set; }
    }

    internal sealed class CrossValidationIssue
    {
        // Original result fields are retained for existing UI bindings.
        public string RuleText { get; set; }
        public string TriggerNodeType { get; set; }
        public string TriggerObjectId { get; set; }
        public string TriggerObjectName { get; set; }
        public string TriggerObjectPath { get; set; }
        public string CheckNodeType { get; set; }
        public string CheckObjectId { get; set; }
        public string CheckObjectName { get; set; }
        public string CheckObjectPath { get; set; }
        public string CheckParentNodeType { get; set; }
        public string CheckParentObjectId { get; set; }
        public string CheckParentObjectName { get; set; }
        public string CheckParentObjectPath { get; set; }
        public string CheckOwnerEntityId { get; set; }
        public string CheckOwnerEntityName { get; set; }
        public string CheckOwnerEntityPath { get; set; }
        public string PropertyName { get; set; }
        public string Operator { get; set; }
        public string ExpectedValue { get; set; }
        public string ActualValue { get; set; }
        public string Message { get; set; }

        public int RuleNumber { get; set; }
        public CrossIssueKind Kind { get; set; }
        public string ErrorCode { get; set; }
        public string Phase { get; set; }
        public string FailedExpression { get; set; }
        // CHECK target can differ from the property owner, e.g. PARENT(Entity).Name.
        public string EvaluationTargetPath { get; set; }
        public IReadOnlyList<CrossConditionDetail> ConditionDetails { get; set; }
    }

    internal sealed class CrossRuleExecutionResult
    {
        private readonly List<CrossValidationIssue> _issues = new List<CrossValidationIssue>();
        public int RuleNumber { get; internal set; }
        public string RuleText { get; internal set; }
        public CrossRuleStatus Status { get; internal set; }
        public int TriggerCandidates { get; internal set; }
        public int EvaluatedTriggers { get; internal set; }
        public int MatchedTriggers { get; internal set; }
        public int UnmatchedTriggers { get; internal set; }
        public int MissingTargetTriggers { get; internal set; }
        public int PassedChecks { get; internal set; }
        public int FailedChecks { get; internal set; }
        public int ErrorCount { get; internal set; }
        public bool Aborted { get; internal set; }
        public string SkipReason { get; internal set; }
        public int CompletedChecks => PassedChecks + FailedChecks;
        public IReadOnlyList<CrossValidationIssue> Issues => _issues.AsReadOnly();
        internal void Add(CrossValidationIssue issue) { _issues.Add(issue); }
        internal void Finish()
        {
            Status = ErrorCount > 0 ? CrossRuleStatus.Error :
                FailedChecks > 0 ? CrossRuleStatus.Failed :
                PassedChecks > 0 ? CrossRuleStatus.Passed : CrossRuleStatus.Skipped;
        }
    }

    internal sealed class CrossValidationReport
    {
        public IReadOnlyList<CrossRuleExecutionResult> Rules { get; }
        public IReadOnlyList<CrossValidationIssue> Issues { get; }
        public bool HasErrors => Rules.Any(x => x.ErrorCount > 0);
        public bool HasViolations => Rules.Any(x => x.FailedChecks > 0);
        public bool HasSkippedRules => Rules.Any(x => x.Status == CrossRuleStatus.Skipped);
        public bool HasSkippedTargets => Rules.Any(x => x.MissingTargetTriggers > 0);
        public int CompletedChecks => Rules.Sum(x => x.CompletedChecks);
        // IsSuccessful says completed checks succeeded; inspect skips separately.
        public bool IsSuccessful => CompletedChecks > 0 && !HasErrors && !HasViolations;
        // A deliberately conservative gate. No rules / all skipped is never approval.
        public bool AllRulesPassed => Rules.Count > 0 && !HasSkippedTargets &&
            Rules.All(x => x.Status == CrossRuleStatus.Passed);
        internal CrossValidationReport(IEnumerable<CrossRuleExecutionResult> rules)
        {
            Rules = rules.OrderBy(x => x.RuleNumber).ToList().AsReadOnly();
            Issues = Rules.SelectMany(x => x.Issues).ToList().AsReadOnly();
        }
    }

    internal sealed class CrossRuleException : Exception
    {
        public string Code { get; }
        public CrossConditionDetail Detail { get; }
        public CrossRuleException(string code, string message, CrossConditionDetail detail = null,
            Exception inner = null) : base(message, inner)
        { Code = code; Detail = detail; }
    }

    internal sealed class ValidationNode
    {
        private readonly List<ValidationNode> _children = new List<ValidationNode>();
        private readonly Dictionary<string, string> _properties =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private readonly Lazy<IReadOnlyDictionary<string, IReadOnlyList<ValidationNode>>> _descendants;
        private bool _frozen;
        public string NodeType { get; }
        public string ObjectId { get; }
        public string Name { get; }
        public string Path { get; }
        public ValidationNode Parent { get; private set; }
        public ValidationNode Root { get; private set; }
        public IReadOnlyList<ValidationNode> Children { get; }
        public IReadOnlyDictionary<string, string> Properties { get; }

        public ValidationNode(string nodeType, string objectId, string name, string path)
        {
            if (string.IsNullOrWhiteSpace(nodeType)) throw new ArgumentException("NodeType is required.");
            NodeType = nodeType; ObjectId = objectId ?? ""; Name = name ?? ""; Path = path ?? "";
            Children = _children.AsReadOnly();
            Properties = new ReadOnlyDictionary<string, string>(_properties);
            _descendants = new Lazy<IReadOnlyDictionary<string, IReadOnlyList<ValidationNode>>>(
                BuildDescendants, LazyThreadSafetyMode.ExecutionAndPublication);
        }
        internal void AddChild(ValidationNode child)
        {
            if (_frozen) throw new InvalidOperationException("The model snapshot is read-only.");
            if (child == null || child._frozen || child.Parent != null)
                throw new ArgumentException("Child must be a new, unattached node.");
            for (var p = this; p != null; p = p.Parent)
                if (ReferenceEquals(p, child)) throw new ArgumentException("Model cycle detected.");
            child.Parent = this;
            _children.Add(child);
        }
        internal void SetProperty(string name, string value)
        {
            if (_frozen) throw new InvalidOperationException("The model snapshot is read-only.");
            if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Property name is required.");
            _properties[name] = value ?? "";
        }
        internal void Freeze(ValidationNode root, int depth, int maxDepth)
        {
            if (depth > maxDepth) throw new ArgumentException("Model depth limit exceeded.");
            if (_frozen)
            {
                if (!ReferenceEquals(Root, root)) throw new ArgumentException("Snapshot root mismatch.");
                return;
            }
            Root = root;
            foreach (var child in _children) child.Freeze(root, depth + 1, maxDepth);
            _frozen = true;
        }
        public bool TryGetValue(string propertyName, out string value)
        {
            if (string.Equals(propertyName, "Name", StringComparison.OrdinalIgnoreCase))
            { value = Name; return true; }
            return _properties.TryGetValue(propertyName, out value);
        }
        public string GetValue(string propertyName)
        {
            string value;
            if (TryGetValue(propertyName, out value)) return value;
            throw new CrossRuleException("MISSING_PROPERTY", "Property '" + propertyName + "' is missing on " + Path,
                new CrossConditionDetail { ContextNode = this, ObjectPath = Path, PropertyName = propertyName });
        }
        public IReadOnlyList<ValidationNode> GetDescendantsByType(string nodeType)
        {
            if (!_frozen) throw new InvalidOperationException("Freeze/index the model before querying descendants.");
            IReadOnlyList<ValidationNode> nodes;
            return _descendants.Value.TryGetValue(nodeType, out nodes) ? nodes : Array.Empty<ValidationNode>();
        }
        public ValidationNode GetParentByType(string nodeType)
        {
            for (var p = Parent; p != null; p = p.Parent)
                if (string.Equals(p.NodeType, nodeType, StringComparison.OrdinalIgnoreCase)) return p;
            return null;
        }
        public ValidationNode GetNearestAncestorByType(string nodeType) => GetParentByType(nodeType);
        public ValidationNode GetRootByType(string nodeType) => Root != null &&
            (string.IsNullOrWhiteSpace(nodeType) || string.Equals(Root.NodeType, nodeType, StringComparison.OrdinalIgnoreCase)) ? Root : null;
        private IReadOnlyDictionary<string, IReadOnlyList<ValidationNode>> BuildDescendants()
        {
            // Build privately, then publish the completed, read-only dictionary through Lazy<T>.
            var map = new Dictionary<string, List<ValidationNode>>(StringComparer.OrdinalIgnoreCase);
            var stack = new Stack<ValidationNode>();
            for (int i = _children.Count - 1; i >= 0; i--) stack.Push(_children[i]);
            while (stack.Count > 0)
            {
                var node = stack.Pop();
                List<ValidationNode> list;
                if (!map.TryGetValue(node.NodeType, out list)) map[node.NodeType] = list = new List<ValidationNode>();
                list.Add(node);
                for (int i = node._children.Count - 1; i >= 0; i--) stack.Push(node._children[i]);
            }
            var result = new Dictionary<string, IReadOnlyList<ValidationNode>>(StringComparer.OrdinalIgnoreCase);
            foreach (var pair in map) result.Add(pair.Key, pair.Value.AsReadOnly());
            return new ReadOnlyDictionary<string, IReadOnlyList<ValidationNode>>(result);
        }
    }

    internal sealed class ValidationNodeIndex
    {
        public ValidationNode Root { get; }
        public IReadOnlyList<ValidationNode> AllNodes { get; }
        public IReadOnlyDictionary<string, IReadOnlyList<ValidationNode>> NodesByType { get; }
        public ValidationNodeIndex(ValidationNode root, int maxDepth = 256)
        {
            if (root == null) throw new ArgumentNullException(nameof(root));
            if (root.Parent != null) throw new ArgumentException("Index must start at the model root.");
            Root = root;
            root.Freeze(root, 0, maxDepth);
            var all = new List<ValidationNode>();
            var map = new Dictionary<string, List<ValidationNode>>(StringComparer.OrdinalIgnoreCase);
            var stack = new Stack<ValidationNode>(); stack.Push(root);
            while (stack.Count > 0)
            {
                var n = stack.Pop(); all.Add(n);
                List<ValidationNode> list;
                if (!map.TryGetValue(n.NodeType, out list)) map[n.NodeType] = list = new List<ValidationNode>();
                list.Add(n);
                for (int i = n.Children.Count - 1; i >= 0; i--) stack.Push(n.Children[i]);
            }
            AllNodes = all.AsReadOnly();
            var result = new Dictionary<string, IReadOnlyList<ValidationNode>>(StringComparer.OrdinalIgnoreCase);
            foreach (var pair in map) result.Add(pair.Key, pair.Value.AsReadOnly());
            NodesByType = new ReadOnlyDictionary<string, IReadOnlyList<ValidationNode>>(result);
        }
    }

    internal static class ValidationNodeBuilder
    {
        public static ValidationNode Build(ModelInfo modelInfo, int maxDepth = 256)
        {
            if (modelInfo == null) throw new ArgumentNullException(nameof(modelInfo));
            string name = modelInfo.getoName() ?? "";
            var root = new ValidationNode("Model", modelInfo.getoObjectId(), name, "Model:" + name);
            var properties = modelInfo.getoObjectProperty();
            if (properties != null) foreach (var property in properties) LoadProperty(root, property);
            var children = modelInfo.getoModelObject();
            if (children != null) foreach (var child in children) BuildObject(child, root, 1, maxDepth);
            root.Freeze(root, 0, maxDepth);
            return root;
        }
        private static void BuildObject(ModelObject obj, ValidationNode parent, int depth, int maxDepth)
        {
            if (obj == null) return;
            if (depth > maxDepth) throw new ArgumentException("Model depth limit exceeded (or model contains a cycle).");
            string type = obj.getoClassName() ?? "";
            string name = obj.getoName() ?? "";
            var node = new ValidationNode(type, obj.getoObjectId(), name, parent.Path + "/" + type + ":" + name);
            var properties = obj.getoObjectProperty();
            if (properties != null) foreach (var property in properties) LoadProperty(node, property);
            parent.AddChild(node);
            var children = obj.getoModelObject();
            if (children != null) foreach (var child in children) BuildObject(child, node, depth + 1, maxDepth);
        }
        private static void LoadProperty(ValidationNode node, ObjectProperty property)
        {
            if (property == null) return;
            string name = property.getoPropertyClassName();
            if (string.IsNullOrWhiteSpace(name) || node.Properties.ContainsKey(name)) return;
            string formatted = property.getoPropertyFormatAsString();
            node.SetProperty(name, !string.IsNullOrWhiteSpace(formatted) ? formatted : property.getoPropertyValue());
        }
    }

    // A character lexer, not a regular-expression-based splitter. Quotes are opaque.
    internal enum RuleTokenKind { Word, String, Operator, LParen, RParen, Comma, End }
    internal sealed class RuleToken
    {
        public RuleTokenKind Kind;
        public string Value;
        public int Position;
        public string Source;
    }
    internal static class RuleLexer
    {
        public static List<RuleToken> Tokenize(string text)
        {
            var result = new List<RuleToken>();
            int i = 0;
            while (i < text.Length)
            {
                char c = text[i];
                if (char.IsWhiteSpace(c)) { i++; continue; }
                int start = i;
                if (c == '\'' || c == '"')
                {
                    char quote = c; i++;
                    var value = new StringBuilder(); bool closed = false;
                    while (i < text.Length)
                    {
                        c = text[i];
                        // Preserve regex backslashes, including \\, \d, \A and \z.
                        // Escaped quote sequences are preserved as in the original engine.
                        // A doubled delimiter inserts a literal quote without a backslash.
                        if (c == '\\' && i + 1 < text.Length)
                        { value.Append(c).Append(text[i + 1]); i += 2; continue; }
                        if (c == quote)
                        {
                            if (i + 1 < text.Length && text[i + 1] == quote)
                            { value.Append(quote); i += 2; continue; }
                            i++; closed = true; break;
                        }
                        value.Append(c); i++;
                    }
                    if (!closed) throw Syntax("Unclosed quoted value", start);
                    result.Add(new RuleToken { Kind = RuleTokenKind.String, Value = value.ToString(),
                        Position = start, Source = text.Substring(start, i - start) });
                    continue;
                }
                RuleTokenKind kind;
                if (c == '(') { kind = RuleTokenKind.LParen; i++; }
                else if (c == ')') { kind = RuleTokenKind.RParen; i++; }
                else if (c == ',') { kind = RuleTokenKind.Comma; i++; }
                else if (c == '=' || c == '!' || c == '<' || c == '>')
                {
                    kind = RuleTokenKind.Operator; i++;
                    if (i < text.Length && text[i] == '=' && c != '=') i++;
                    if (c == '!' && i == start + 1) throw Syntax("Use != or NOT, not !", start);
                }
                else
                {
                    kind = RuleTokenKind.Word;
                    while (i < text.Length && !char.IsWhiteSpace(text[i]) &&
                        "(),=!<>\"'".IndexOf(text[i]) < 0) i++;
                    if (i == start) throw Syntax("Unexpected character", start);
                }
                string raw = text.Substring(start, i - start);
                result.Add(new RuleToken { Kind = kind, Value = raw, Position = start, Source = raw });
            }
            result.Add(new RuleToken { Kind = RuleTokenKind.End, Value = "", Source = "", Position = text.Length });
            return result;
        }
        internal static CrossRuleException Syntax(string message, int position) =>
            new CrossRuleException("INVALID_SYNTAX", message + " (character " + (position + 1) + ").");
    }

    internal sealed class RuleScope
    {
        public CrossCheckScope Kind;
        public string NodeType;
        public string Text => Kind == CrossCheckScope.Self ? "SELF" : Kind.ToString().ToUpperInvariant() + "(" + NodeType + ")";
        public IReadOnlyList<ValidationNode> Resolve(ValidationNode node)
        {
            if (node == null) throw new InvalidOperationException("A model node is required.");
            if (Kind == CrossCheckScope.Self) return new[] { node };
            if (Kind == CrossCheckScope.Descendant) return node.GetDescendantsByType(NodeType);
            var result = Kind == CrossCheckScope.Parent ? node.GetParentByType(NodeType) : node.GetRootByType(NodeType);
            return result == null ? Array.Empty<ValidationNode>() : new[] { result };
        }
    }
    internal abstract class RuleExpr
    {
        public string Text;
        public virtual IEnumerable<RuleExpr> Children => Enumerable.Empty<RuleExpr>();
    }
    internal abstract class BoolExprNode : RuleExpr { }
    internal abstract class ValueExprNode : RuleExpr { }
    internal sealed class LiteralValueExprNode : ValueExprNode
    { public string Value; public bool Quoted; }
    internal sealed class OperandValueExprNode : ValueExprNode
    { public RuleScope Scope; public string PropertyName; }
    internal sealed class FunctionValueExprNode : ValueExprNode
    {
        public string Function;
        public List<ValueExprNode> Arguments = new List<ValueExprNode>();
        public override IEnumerable<RuleExpr> Children => Arguments;
    }
    internal sealed class CountValueExprNode : ValueExprNode
    {
        public RuleScope Scope; public BoolExprNode Filter;
        public override IEnumerable<RuleExpr> Children => new RuleExpr[] { Filter };
    }
    internal sealed class ComparisonExprNode : BoolExprNode
    {
        public ValueExprNode Left, Right; public string Operator;
        public override IEnumerable<RuleExpr> Children => new RuleExpr[] { Left, Right };
    }
    internal sealed class BinaryExprNode : BoolExprNode
    {
        public BoolExprNode Left, Right; public string Operator;
        public override IEnumerable<RuleExpr> Children => new RuleExpr[] { Left, Right };
    }
    internal sealed class UnaryExprNode : BoolExprNode
    {
        public BoolExprNode Operand;
        public override IEnumerable<RuleExpr> Children => new RuleExpr[] { Operand };
    }
    internal sealed class ExistsExprNode : BoolExprNode
    {
        public RuleScope Scope; public BoolExprNode Filter;
        public override IEnumerable<RuleExpr> Children => new RuleExpr[] { Filter };
    }
    internal sealed class HasPropertyExprNode : BoolExprNode
    {
        public OperandValueExprNode Operand;
        public override IEnumerable<RuleExpr> Children => new RuleExpr[] { Operand };
    }
    internal sealed class RuleCheck
    { public RuleScope Target; public BoolExprNode Expression; }
    internal sealed class CrossRuleDefinition
    {
        public string RuleText;
        public string TriggerNodeType;
        public BoolExprNode WhenExpression;
        public RuleCheck Check;
    }

    internal static class CrossRuleParser
    {
        public static CrossRuleDefinition Parse(string ruleText) => Parse(ruleText, new CrossValidationOptions());
        public static CrossRuleDefinition Parse(string ruleText, CrossValidationOptions options)
        {
            var opts = (options ?? new CrossValidationOptions()).Snapshot();
            if (string.IsNullOrWhiteSpace(ruleText)) throw new CrossRuleException("EMPTY_RULE", "Rule text cannot be empty.");
            if (ruleText.Length > opts.MaxRuleLength) throw new CrossRuleException("RULE_TOO_LONG", "Rule length limit exceeded.");
            return new RuleParser(ruleText, opts.MaxExpressionDepth).Parse();
        }
    }
    internal sealed class RuleParser
    {
        private static readonly HashSet<string> WordOperators = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        { "MATCHES", "IMATCHES", "CONTAINS", "ICONTAINS", "STARTSWITH", "ISTARTSWITH",
          "ENDSWITH", "IENDSWITH", "IEQUALS", "INOTEQUALS" };
        private readonly string _source;
        private readonly List<RuleToken> _tokens;
        private readonly int _maxDepth;
        private int _position, _depth;
        public RuleParser(string text, int maxDepth)
        { _source = text; _tokens = RuleLexer.Tokenize(text); _maxDepth = maxDepth; }
        private RuleToken Peek => _tokens[_position];
        private RuleToken Next => _tokens[Math.Min(_position + 1, _tokens.Count - 1)];
        private bool Is(string word) => Peek.Kind == RuleTokenKind.Word && string.Equals(Peek.Value, word, StringComparison.OrdinalIgnoreCase);
        private bool Match(string word) { if (!Is(word)) return false; _position++; return true; }
        private bool Match(RuleTokenKind kind) { if (Peek.Kind != kind) return false; _position++; return true; }
        private RuleToken Take(RuleTokenKind kind, string message)
        { if (Peek.Kind != kind) throw RuleLexer.Syntax(message, Peek.Position); return _tokens[_position++]; }
        private void Expect(string word)
        { if (!Match(word)) throw RuleLexer.Syntax("Expected " + word, Peek.Position); }
        private void Enter()
        { if (++_depth > _maxDepth) throw new CrossRuleException("EXPRESSION_TOO_DEEP", "Expression nesting limit exceeded."); }
        private static bool IsScopeWord(string value) =>
            string.Equals(value, "SELF", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(value, "PARENT", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(value, "ROOT", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(value, "DESCENDANT", StringComparison.OrdinalIgnoreCase);
        public CrossRuleDefinition Parse()
        {
            Expect("WHEN");
            string trigger = Take(RuleTokenKind.Word, "Expected trigger node type").Value;
            var when = ParseOr();
            Expect("CHECK");
            var check = ParseCheck();
            Take(RuleTokenKind.End, "Unexpected trailing token; quote regex patterns and string literals");
            CheckDepth(when); CheckDepth(check.Expression);
            return new CrossRuleDefinition { RuleText = _source, TriggerNodeType = trigger, WhenExpression = when, Check = check };
        }
        private void CheckDepth(RuleExpr root)
        {
            var stack = new Stack<Tuple<RuleExpr, int>>(); stack.Push(Tuple.Create(root, 1));
            while (stack.Count > 0)
            {
                var pair = stack.Pop();
                if (pair.Item2 > _maxDepth) throw new CrossRuleException("EXPRESSION_TOO_DEEP", "Expression tree depth limit exceeded.");
                foreach (var child in pair.Item1.Children) stack.Push(Tuple.Create(child, pair.Item2 + 1));
            }
        }
        private RuleCheck ParseCheck()
        {
            var scope = new RuleScope { Kind = CrossCheckScope.Self };
            if (Peek.Kind == RuleTokenKind.Word && IsScopeWord(Peek.Value) &&
                (Next.Kind == RuleTokenKind.LParen || Is("SELF")))
            {
                // SELF is a scope here only as CHECK SELF (...); SELF.Name is an operand.
                if (!Is("SELF") || Next.Kind == RuleTokenKind.LParen)
                {
                    scope = ParseScope();
                    if (Match(RuleTokenKind.LParen))
                    {
                        var grouped = ParseOr();
                        Take(RuleTokenKind.RParen, "Expected ')' after CHECK group");
                        return new RuleCheck { Target = scope, Expression = grouped };
                    }
                    // Legacy shorthand: CHECK DESCENDANT(Attribute).Name != "".
                    if (Peek.Kind == RuleTokenKind.Word && Peek.Value.StartsWith(".", StringComparison.Ordinal))
                        Peek.Value = "SELF" + Peek.Value;
                    // Also allow CHECK DESCENDANT(Attribute) SELF.Name != "".
                }
            }
            return new RuleCheck { Target = scope, Expression = ParseOr() };
        }
        private RuleScope ParseScope()
        {
            var token = Take(RuleTokenKind.Word, "Expected SELF/PARENT/ROOT/DESCENDANT scope");
            string word = token.Value.ToUpperInvariant();
            CrossCheckScope kind;
            switch (word)
            {
                case "SELF": return new RuleScope { Kind = CrossCheckScope.Self };
                case "PARENT": kind = CrossCheckScope.Parent; break;
                case "ROOT": kind = CrossCheckScope.Root; break;
                case "DESCENDANT": kind = CrossCheckScope.Descendant; break;
                default: throw RuleLexer.Syntax("Unknown scope " + token.Value, token.Position);
            }
            Take(RuleTokenKind.LParen, "Expected '(' after scope");
            string nodeType = Take(RuleTokenKind.Word, "Expected scope node type").Value;
            Take(RuleTokenKind.RParen, "Expected ')' after node type");
            return new RuleScope { Kind = kind, NodeType = nodeType };
        }
        private BoolExprNode ParseOr()
        {
            var left = ParseAnd();
            while (Match("OR"))
            {
                var right = ParseAnd();
                left = new BinaryExprNode { Operator = "OR", Left = left, Right = right,
                    Text = "(" + left.Text + " OR " + right.Text + ")" };
            }
            return left;
        }
        private BoolExprNode ParseAnd()
        {
            var left = ParseUnary();
            while (Match("AND"))
            {
                var right = ParseUnary();
                left = new BinaryExprNode { Operator = "AND", Left = left, Right = right,
                    Text = "(" + left.Text + " AND " + right.Text + ")" };
            }
            return left;
        }
        private BoolExprNode ParseUnary()
        {
            Enter();
            try
            {
                if (Match("NOT"))
                { var inner = ParseUnary(); return new UnaryExprNode { Operand = inner, Text = "NOT (" + inner.Text + ")" }; }
                if (Match(RuleTokenKind.LParen))
                { var inner = ParseOr(); Take(RuleTokenKind.RParen, "Expected ')'"); return inner; }
                if (Match("EXISTS"))
                {
                    var scope = ParseScope();
                    Take(RuleTokenKind.LParen, "Expected '(' before EXISTS filter");
                    var inner = ParseOr(); Take(RuleTokenKind.RParen, "Expected ')' after EXISTS filter");
                    return new ExistsExprNode { Scope = scope, Filter = inner, Text = "EXISTS " + scope.Text + " (" + inner.Text + ")" };
                }
                if (Match("HASPROPERTY"))
                {
                    Take(RuleTokenKind.LParen, "Expected '(' after HASPROPERTY");
                    var operand = ParseValue(true) as OperandValueExprNode;
                    if (operand == null) throw RuleLexer.Syntax("HASPROPERTY requires a property operand", Peek.Position);
                    Take(RuleTokenKind.RParen, "Expected ')' after HASPROPERTY operand");
                    return new HasPropertyExprNode { Operand = operand, Text = "HASPROPERTY(" + operand.Text + ")" };
                }
                var left = ParseValue(true);
                var op = Peek;
                if (op.Kind == RuleTokenKind.Operator || (op.Kind == RuleTokenKind.Word && WordOperators.Contains(op.Value))) _position++;
                else throw RuleLexer.Syntax("Expected comparison operator", op.Position);
                var right = ParseValue(false); // No implicit empty operand.
                string operation = op.Value.ToUpperInvariant();
                var literal = right as LiteralValueExprNode;
                if ((operation == "MATCHES" || operation == "IMATCHES") && literal != null && !literal.Quoted)
                    throw RuleLexer.Syntax("Regex patterns must be quoted", op.Position);
                return new ComparisonExprNode { Left = left, Operator = operation, Right = right,
                    Text = left.Text + " " + operation + " " + right.Text };
            }
            finally { _depth--; }
        }
        private ValueExprNode ParseValue(bool leftSide)
        {
            Enter();
            try
            {
                if (Peek.Kind == RuleTokenKind.String)
                { var t = _tokens[_position++]; return new LiteralValueExprNode { Value = t.Value, Quoted = true, Text = t.Source }; }
                if (Peek.Kind != RuleTokenKind.Word || Is("AND") || Is("OR") || Is("CHECK") || Is("WHEN"))
                    throw RuleLexer.Syntax("Missing value; use \"\" for an explicit empty string", Peek.Position);
                if (Match("COUNT"))
                {
                    var scope = ParseScope();
                    Take(RuleTokenKind.LParen, "Expected '(' before COUNT filter");
                    var inner = ParseOr(); Take(RuleTokenKind.RParen, "Expected ')' after COUNT filter");
                    return new CountValueExprNode { Scope = scope, Filter = inner, Text = "COUNT " + scope.Text + " (" + inner.Text + ")" };
                }
                if ((Is("CONCAT") || Is("NUM") || Is("BOOL")) && Next.Kind == RuleTokenKind.LParen)
                {
                    string name = _tokens[_position++].Value.ToUpperInvariant();
                    Take(RuleTokenKind.LParen, "Expected '('");
                    var function = new FunctionValueExprNode { Function = name };
                    function.Arguments.Add(ParseValue(false));
                    while (Match(RuleTokenKind.Comma)) function.Arguments.Add(ParseValue(false));
                    Take(RuleTokenKind.RParen, "Expected ')' after function arguments");
                    if (name != "CONCAT" && function.Arguments.Count != 1)
                        throw RuleLexer.Syntax(name + " requires one argument", Peek.Position);
                    function.Text = name + "(" + string.Join(", ", function.Arguments.Select(x => x.Text)) + ")";
                    return function;
                }
                if (Peek.Value.StartsWith("SELF.", StringComparison.OrdinalIgnoreCase))
                {
                    var token = _tokens[_position++];
                    string property = PropertySuffix(token.Value.Substring(5));
                    return new OperandValueExprNode { Scope = new RuleScope { Kind = CrossCheckScope.Self },
                        PropertyName = property, Text = "SELF." + property };
                }
                if (IsScopeWord(Peek.Value) && !Is("SELF") && Next.Kind == RuleTokenKind.LParen)
                {
                    var scope = ParseScope();
                    var suffix = Take(RuleTokenKind.Word, "Expected .Property after scope");
                    if (!suffix.Value.StartsWith(".", StringComparison.Ordinal)) throw RuleLexer.Syntax("Expected .Property after scope", suffix.Position);
                    string property = PropertySuffix(suffix.Value.Substring(1));
                    return new OperandValueExprNode { Scope = scope, PropertyName = property, Text = scope.Text + "." + property };
                }
                var value = _tokens[_position++];
                if (leftSide)
                    return new OperandValueExprNode { Scope = new RuleScope { Kind = CrossCheckScope.Self },
                        PropertyName = value.Value, Text = value.Value };
                return new LiteralValueExprNode { Value = value.Value, Quoted = false, Text = value.Source };
            }
            finally { _depth--; }
        }
        private string PropertySuffix(string suffix)
        {
            if (suffix.Length > 0) return suffix;
            if (Peek.Kind != RuleTokenKind.Word && Peek.Kind != RuleTokenKind.String)
                throw RuleLexer.Syntax("Expected property name", Peek.Position);
            string name = _tokens[_position++].Value;
            if (name.Length == 0) throw RuleLexer.Syntax("Property name cannot be empty", Peek.Position);
            return name;
        }
    }

    internal enum RuleValueKind { Text, Number, Boolean }
    internal sealed class EvaluatedValue
    {
        public RuleValueKind Kind;
        public string Text;
        public decimal Number;
        public bool Boolean;
        public ValidationNode Node;
        public string PropertyName;
        public static EvaluatedValue String(string text, ValidationNode node = null, string property = null) =>
            new EvaluatedValue { Kind = RuleValueKind.Text, Text = text ?? "", Node = node, PropertyName = property };
        public static EvaluatedValue Numeric(decimal value, ValidationNode node = null, string property = null) =>
            new EvaluatedValue { Kind = RuleValueKind.Number, Text = value.ToString(CultureInfo.InvariantCulture),
                Number = value, Node = node, PropertyName = property };
        public static EvaluatedValue Bool(bool value, ValidationNode node = null, string property = null) =>
            new EvaluatedValue { Kind = RuleValueKind.Boolean, Text = value ? "true" : "false",
                Boolean = value, Node = node, PropertyName = property };
    }
    internal sealed class BooleanEvaluation
    {
        public bool Value;
        public CrossConditionDetail Summary;
        public IReadOnlyList<CrossConditionDetail> Details;
        public static BooleanEvaluation Create(bool value, CrossConditionDetail summary,
            IEnumerable<CrossConditionDetail> details = null) => new BooleanEvaluation
            { Value = value, Summary = summary,
                Details = (details ?? new[] { summary }).ToList().AsReadOnly() };
    }

    internal sealed class RegexCache
    {
        private readonly object _gate = new object();
        private readonly Dictionary<string, Regex> _cache = new Dictionary<string, Regex>(StringComparer.Ordinal);
        private readonly Queue<string> _order = new Queue<string>();
        private readonly CrossValidationOptions _options;
        public RegexCache(CrossValidationOptions options) { _options = options; }
        public Regex Get(string pattern, bool ignoreCase)
        {
            if (string.IsNullOrEmpty(pattern))
                throw new CrossRuleException("EMPTY_REGEX", "Empty regex patterns are not permitted. Use an explicit pattern, e.g. .*.");
            if (pattern.Length > _options.MaxRegexPatternLength)
                throw new CrossRuleException("REGEX_TOO_LONG", "Regex pattern length limit exceeded.");
            string key = (ignoreCase ? "I:" : "C:") + pattern;
            lock (_gate)
            {
                Regex cached;
                if (_cache.TryGetValue(key, out cached)) return cached;
            }
            Regex regex;
            try
            {
                var flags = RegexOptions.Compiled | RegexOptions.CultureInvariant;
                if (ignoreCase) flags |= RegexOptions.IgnoreCase;
                regex = new Regex(pattern, flags, _options.RegexTimeout);
            }
            catch (ArgumentException ex)
            { throw new CrossRuleException("INVALID_REGEX", "Invalid regex: " + ex.Message, null, ex); }
            lock (_gate)
            {
                Regex cached;
                if (_cache.TryGetValue(key, out cached)) return cached;
                while (_cache.Count >= _options.MaxCachedRegexes) _cache.Remove(_order.Dequeue());
                _cache.Add(key, regex); _order.Enqueue(key);
                return regex;
            }
        }
    }

    internal sealed class CompiledCrossRule
    {
        public string RuleText { get; internal set; }
        public string TriggerNodeType { get; internal set; }
        public Func<ValidationNode, bool> TriggerPredicate { get; internal set; }
        public Func<ValidationNode, IReadOnlyList<ValidationNode>> TargetSelector { get; internal set; }
        public Func<ValidationNode, bool> CheckPredicate { get; internal set; }
        internal Func<ValidationNode, BooleanEvaluation> EvaluateTrigger;
        internal Func<ValidationNode, BooleanEvaluation> EvaluateCheck;
        internal CrossValidationOptions Options;
        internal string CheckExpressionText;
    }

    internal static class CrossRuleCompiler
    {
        public static CompiledCrossRule Compile(CrossRuleDefinition rule) => Compile(rule, new CrossValidationOptions());
        public static CompiledCrossRule Compile(CrossRuleDefinition rule, CrossValidationOptions options)
        {
            if (rule == null) throw new ArgumentNullException(nameof(rule));
            return new RuleCompilationContext((options ?? new CrossValidationOptions()).Snapshot()).Compile(rule);
        }
    }
    internal sealed class RuleCompilationContext
    {
        private readonly CrossValidationOptions _options;
        private readonly RegexCache _regexes;
        public RuleCompilationContext(CrossValidationOptions options)
        { _options = options; _regexes = new RegexCache(options); }
        public CompiledCrossRule Compile(CrossRuleDefinition rule)
        {
            var trigger = CompileBoolean(rule.WhenExpression);
            var check = CompileBoolean(rule.Check.Expression);
            return new CompiledCrossRule { RuleText = rule.RuleText, TriggerNodeType = rule.TriggerNodeType,
                EvaluateTrigger = trigger, EvaluateCheck = check, TargetSelector = rule.Check.Target.Resolve,
                TriggerPredicate = n => trigger(n).Value, CheckPredicate = n => check(n).Value,
                Options = _options, CheckExpressionText = rule.Check.Expression.Text };
        }
        private static CrossConditionDetail Detail(string expression, ValidationNode node, string property,
            string op, string actual, string expected, string message)
        {
            return new CrossConditionDetail { Expression = expression, ContextNode = node,
                ObjectPath = node?.Path ?? "", PropertyName = property ?? "", Operator = op ?? "",
                ActualValue = actual ?? "", ExpectedValue = expected ?? "", Message = message ?? "" };
        }
        private static ValidationNode ScalarNode(OperandValueExprNode operand, ValidationNode node, bool allowAbsent)
        {
            var targets = operand.Scope.Resolve(node);
            if (targets.Count > 1)
                throw new CrossRuleException("AMBIGUOUS_SCOPE", "Scalar operand selects multiple objects. Use CHECK scope, EXISTS or COUNT.",
                    Detail(operand.Text, node, operand.PropertyName, "", targets.Count.ToString(CultureInfo.InvariantCulture), "1", "Ambiguous object selection."));
            if (targets.Count == 0)
            {
                if (allowAbsent) return null;
                throw new CrossRuleException("MISSING_SCOPE", "No object found for " + operand.Scope.Text,
                    Detail(operand.Text, node, operand.PropertyName, "", "0", "1", "Referenced object is missing."));
            }
            return targets[0];
        }
        private Func<ValidationNode, EvaluatedValue> CompileValue(ValueExprNode expr)
        {
            var literal = expr as LiteralValueExprNode;
            if (literal != null) return n => EvaluatedValue.String(literal.Value);
            var operand = expr as OperandValueExprNode;
            if (operand != null)
                return n =>
                {
                    var source = ScalarNode(operand, n, false);
                    string value;
                    if (!source.TryGetValue(operand.PropertyName, out value))
                        throw new CrossRuleException("MISSING_PROPERTY", "Property '" + operand.PropertyName + "' is missing on " + source.Path,
                            Detail(expr.Text, source, operand.PropertyName, "READ", "<missing>", "Property must exist", "Property is absent, not empty."));
                    return EvaluatedValue.String(value, source, operand.PropertyName);
                };
            var function = expr as FunctionValueExprNode;
            if (function != null)
            {
                var args = function.Arguments.Select(CompileValue).ToArray();
                return n =>
                {
                    if (function.Function == "CONCAT")
                    {
                        var b = new StringBuilder();
                        foreach (var arg in args) b.Append(arg(n).Text);
                        return EvaluatedValue.String(b.ToString(), n, function.Text);
                    }
                    var value = args[0](n);
                    try
                    {
                        if (function.Function == "NUM") return EvaluatedValue.Numeric(Number(value), value.Node ?? n, value.PropertyName);
                        if (function.Function == "BOOL") return EvaluatedValue.Bool(Boolean(value), value.Node ?? n, value.PropertyName);
                        throw new InvalidOperationException("Unknown value function.");
                    }
                    catch (CrossRuleException ex)
                    {
                        throw new CrossRuleException(ex.Code, ex.Message,
                            Detail(expr.Text, value.Node ?? n, value.PropertyName, function.Function, value.Text,
                                function.Function == "NUM" ? "Invariant number (decimal separator .)" : "true/false/1/0", ex.Message), ex);
                    }
                };
            }
            var count = expr as CountValueExprNode;
            if (count != null)
            {
                var predicate = CompileBoolean(count.Filter);
                return n =>
                {
                    decimal matches = 0;
                    foreach (var child in count.Scope.Resolve(n))
                        if (predicate(child).Value) matches++;
                    return EvaluatedValue.Numeric(matches, n, count.Text);
                };
            }
            throw new InvalidOperationException("Unknown value expression.");
        }
        private Func<ValidationNode, BooleanEvaluation> CompileBoolean(BoolExprNode expr)
        {
            var comparison = expr as ComparisonExprNode;
            if (comparison != null) return CompileComparison(comparison);
            var binary = expr as BinaryExprNode;
            if (binary != null)
            {
                var left = CompileBoolean(binary.Left); var right = CompileBoolean(binary.Right);
                return n =>
                {
                    var l = left(n);
                    if (binary.Operator == "AND")
                    {
                        if (!l.Value) return l;
                        var r = right(n);
                        if (!r.Value) return r;
                        return BooleanEvaluation.Create(true, Detail(expr.Text, n, "", "AND", "true", "true", "Both conditions passed."));
                    }
                    if (l.Value) return BooleanEvaluation.Create(true, Detail(expr.Text, n, "", "OR", "true", "true", "Left condition passed."));
                    var rr = right(n);
                    if (rr.Value) return BooleanEvaluation.Create(true, Detail(expr.Text, n, "", "OR", "true", "true", "Right condition passed."));
                    return BooleanEvaluation.Create(false, Detail(expr.Text, n, "", "OR", "false", "true", "Both OR branches failed."),
                        l.Details.Concat(rr.Details));
                };
            }
            var not = expr as UnaryExprNode;
            if (not != null)
            {
                var inner = CompileBoolean(not.Operand);
                return n =>
                {
                    var result = inner(n);
                    return BooleanEvaluation.Create(!result.Value,
                        Detail(expr.Text, n, "", "NOT", (!result.Value).ToString().ToLowerInvariant(), "true",
                            result.Value ? "The negated condition was true; NOT therefore failed." : "The negated condition was false."));
                };
            }
            var exists = expr as ExistsExprNode;
            if (exists != null)
            {
                var filter = CompileBoolean(exists.Filter);
                return n =>
                {
                    var targets = exists.Scope.Resolve(n);
                    foreach (var target in targets)
                        if (filter(target).Value)
                            return BooleanEvaluation.Create(true, Detail(expr.Text, n, exists.Scope.Text, "EXISTS", ">= 1 match", ">= 1 match", "A matching object exists."));
                    return BooleanEvaluation.Create(false, Detail(expr.Text, n, exists.Scope.Text, "EXISTS", "0 matches", ">= 1 match",
                        "No matching object among " + targets.Count + " candidates."));
                };
            }
            var has = expr as HasPropertyExprNode;
            if (has != null)
                return n =>
                {
                    var source = ScalarNode(has.Operand, n, true); string value;
                    bool found = source != null && source.TryGetValue(has.Operand.PropertyName, out value);
                    return BooleanEvaluation.Create(found, Detail(expr.Text, source ?? n, has.Operand.PropertyName,
                        "HASPROPERTY", found ? "true" : "false", "true", found ? "Property exists." : "Property does not exist."));
                };
            throw new InvalidOperationException("Unknown boolean expression.");
        }
        private static bool IsConstant(ValueExprNode expr)
        {
            if (expr is LiteralValueExprNode) return true;
            var function = expr as FunctionValueExprNode;
            return function != null && function.Arguments.All(IsConstant);
        }
        private Func<ValidationNode, BooleanEvaluation> CompileComparison(ComparisonExprNode expr)
        {
            var left = CompileValue(expr.Left); var right = CompileValue(expr.Right);
            string op = expr.Operator;
            bool isRegex = op == "MATCHES" || op == "IMATCHES";
            bool ignoreRegexCase = op == "IMATCHES" || !_options.RegexCaseSensitive;
            Regex constantRegex = null;
            if (isRegex && IsConstant(expr.Right))
            {
                string pattern = right(null).Text;
                try { constantRegex = _regexes.Get(pattern, ignoreRegexCase); }
                catch (CrossRuleException ex)
                {
                    throw new CrossRuleException(ex.Code, ex.Message,
                        Detail(expr.Text, null, expr.Left.Text, op, "<not evaluated>", pattern, ex.Message), ex);
                }
            }
            return n =>
            {
                var a = left(n); var b = right(n);
                var detail = Detail(expr.Text, a.Node ?? n, a.PropertyName ?? expr.Left.Text, op, a.Text, b.Text, "");
                try
                {
                    bool passed;
                    if (isRegex)
                    {
                        if (a.Text.Length > _options.MaxRegexInputLength)
                            throw new CrossRuleException("REGEX_INPUT_TOO_LONG", "Regex input length limit exceeded.");
                        var regex = constantRegex ?? _regexes.Get(b.Text, ignoreRegexCase);
                        // MATCHES retains .NET IsMatch semantics: add \A...\z for a whole value.
                        passed = regex.IsMatch(a.Text);
                    }
                    else passed = Compare(a, op, b);
                    detail.Message = passed ? "Condition passed." : "Condition failed.";
                    return BooleanEvaluation.Create(passed, detail);
                }
                catch (RegexMatchTimeoutException ex)
                {
                    detail.Message = "Regex timed out after approximately " + _options.RegexTimeout.TotalMilliseconds + " ms.";
                    throw new CrossRuleException("REGEX_TIMEOUT", detail.Message, detail, ex);
                }
                catch (CrossRuleException ex)
                { detail.Message = ex.Message; throw new CrossRuleException(ex.Code, ex.Message, detail, ex); }
            };
        }
        private bool Compare(EvaluatedValue a, string op, EvaluatedValue b)
        {
            var comparison = _options.TextCaseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
            switch (op)
            {
                case "=": return EqualsValue(a, b, comparison);
                case "!=": return !EqualsValue(a, b, comparison);
                case "IEQUALS": return EqualsValue(a, b, StringComparison.OrdinalIgnoreCase);
                case "INOTEQUALS": return !EqualsValue(a, b, StringComparison.OrdinalIgnoreCase);
                case "<": return Number(a) < Number(b);
                case "<=": return Number(a) <= Number(b);
                case ">": return Number(a) > Number(b);
                case ">=": return Number(a) >= Number(b);
                case "CONTAINS": return a.Text.IndexOf(b.Text, comparison) >= 0;
                case "ICONTAINS": return a.Text.IndexOf(b.Text, StringComparison.OrdinalIgnoreCase) >= 0;
                case "STARTSWITH": return a.Text.StartsWith(b.Text, comparison);
                case "ISTARTSWITH": return a.Text.StartsWith(b.Text, StringComparison.OrdinalIgnoreCase);
                case "ENDSWITH": return a.Text.EndsWith(b.Text, comparison);
                case "IENDSWITH": return a.Text.EndsWith(b.Text, StringComparison.OrdinalIgnoreCase);
                default: throw new CrossRuleException("UNKNOWN_OPERATOR", "Unsupported operator: " + op);
            }
        }
        private static bool EqualsValue(EvaluatedValue a, EvaluatedValue b, StringComparison comparison)
        {
            if ((a.Kind == RuleValueKind.Number && b.Kind == RuleValueKind.Boolean) ||
                (a.Kind == RuleValueKind.Boolean && b.Kind == RuleValueKind.Number))
                throw new CrossRuleException("TYPE_MISMATCH", "A typed number cannot be compared with a typed boolean.");
            if (a.Kind == RuleValueKind.Number || b.Kind == RuleValueKind.Number) return Number(a) == Number(b);
            if (a.Kind == RuleValueKind.Boolean || b.Kind == RuleValueKind.Boolean) return Boolean(a) == Boolean(b);
            // No implicit trimming, number conversion, or boolean conversion for ordinary text.
            return string.Equals(a.Text, b.Text, comparison);
        }
        private static decimal Number(EvaluatedValue value)
        {
            if (value.Kind == RuleValueKind.Number) return value.Number;
            decimal number;
            if (value.Kind == RuleValueKind.Boolean ||
                !decimal.TryParse(value.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out number))
                throw new CrossRuleException("INVALID_NUMBER", "Expected an invariant number (decimal separator .), got '" + value.Text + "'.");
            return number;
        }
        private static bool Boolean(EvaluatedValue value)
        {
            if (value.Kind == RuleValueKind.Boolean) return value.Boolean;
            bool boolean; string text = value.Text.Trim();
            if (value.Kind == RuleValueKind.Number)
                throw new CrossRuleException("INVALID_BOOLEAN", "Use BOOL on a text value, not on NUM(...).");
            if (text == "1") return true;
            if (text == "0") return false;
            if (bool.TryParse(text, out boolean)) return boolean;
            throw new CrossRuleException("INVALID_BOOLEAN", "Expected true/false/1/0, got '" + text + "'.");
        }
    }

    internal static class CrossRuleValidationEngine_V2
    {
        // Compatibility API. BOTH violations and rule errors are returned.
        // For approval/workflow gates use ValidateDetailed and inspect the report.
        public static List<CrossValidationIssue> Validate(ModelInfo modelInfo, IEnumerable<string> ruleTexts,
            bool runParallel = true, int? maxParallelism = null)
        {
            return ValidateDetailed(modelInfo, ruleTexts, new CrossValidationOptions
            { RunParallel = runParallel, MaxParallelism = maxParallelism }).Issues.ToList();
        }
        public static List<CrossValidationIssue> Validate(ValidationNodeIndex index,
            IEnumerable<CompiledCrossRule> compiledRules, bool runParallel = true, int? maxParallelism = null)
        {
            return ValidateDetailed(index, compiledRules, new CrossValidationOptions
            { RunParallel = runParallel, MaxParallelism = maxParallelism }).Issues.ToList();
        }
        public static CrossValidationReport ValidateDetailed(ModelInfo modelInfo,
            IEnumerable<string> ruleTexts, CrossValidationOptions options = null)
        {
            if (modelInfo == null) throw new ArgumentNullException(nameof(modelInfo));
            if (ruleTexts == null) throw new ArgumentNullException(nameof(ruleTexts));
            var opts = (options ?? new CrossValidationOptions()).Snapshot();
            // Read the existing DTO graph once, before parallel evaluation; no COM calls in workers.
            var root = ValidationNodeBuilder.Build(modelInfo, opts.MaxModelDepth);
            return ValidateDetailed(new ValidationNodeIndex(root, opts.MaxModelDepth), ruleTexts, opts);
        }
        public static CrossValidationReport ValidateDetailed(ValidationNodeIndex index,
            IEnumerable<string> ruleTexts, CrossValidationOptions options = null)
        {
            if (index == null) throw new ArgumentNullException(nameof(index));
            if (ruleTexts == null) throw new ArgumentNullException(nameof(ruleTexts));
            var opts = (options ?? new CrossValidationOptions()).Snapshot();
            var texts = ruleTexts.ToList();
            var context = new RuleCompilationContext(opts);
            return Run(texts.Count, opts, i =>
            {
                var result = NewResult(i, texts[i]);
                string phase = "Parse";
                CompiledCrossRule rule;
                try
                {
                    var definition = CrossRuleParser.Parse(texts[i], opts);
                    phase = "Compile";
                    rule = context.Compile(definition);
                }
                catch (Exception ex) when (Recoverable(ex))
                {
                    AddError(result, ex, phase, null, null); result.Finish(); return result;
                }
                ExecuteRule(index, rule, result, opts);
                return result;
            });
        }
        public static CrossValidationReport ValidateDetailed(ValidationNodeIndex index,
            IEnumerable<CompiledCrossRule> compiledRules, CrossValidationOptions options = null)
        {
            if (index == null) throw new ArgumentNullException(nameof(index));
            if (compiledRules == null) throw new ArgumentNullException(nameof(compiledRules));
            var opts = (options ?? new CrossValidationOptions()).Snapshot();
            var rules = compiledRules.ToList();
            return Run(rules.Count, opts, i =>
            {
                var rule = rules[i]; var result = NewResult(i, rule?.RuleText);
                if (rule == null || rule.EvaluateTrigger == null || rule.EvaluateCheck == null || rule.TargetSelector == null)
                    AddError(result, new CrossRuleException("INVALID_COMPILED_RULE", "Rule must be created by CrossRuleCompiler.Compile."), "Compile", null, null);
                else ExecuteRule(index, rule, result, opts);
                result.Finish(); return result;
            });
        }
        private static CrossRuleExecutionResult NewResult(int index, string text) =>
            new CrossRuleExecutionResult { RuleNumber = index + 1, RuleText = text ?? "", SkipReason = "" };
        private static CrossValidationReport Run(int count, CrossValidationOptions options,
            Func<int, CrossRuleExecutionResult> action)
        {
            var results = new CrossRuleExecutionResult[count];
            if (options.RunParallel && count > 1)
            {
                var parallel = new ParallelOptions
                { MaxDegreeOfParallelism = options.MaxParallelism ?? Math.Max(1, Environment.ProcessorCount - 1) };
                Parallel.For(0, count, parallel, i => results[i] = action(i));
            }
            else for (int i = 0; i < count; i++) results[i] = action(i);
            return new CrossValidationReport(results);
        }
        private static void ExecuteRule(ValidationNodeIndex index, CompiledCrossRule rule,
            CrossRuleExecutionResult result, CrossValidationOptions runOptions)
        {
            ValidationNode trigger = null, target = null;
            string phase = "WHEN";
            try
            {
                IReadOnlyList<ValidationNode> triggers;
                if (!index.NodesByType.TryGetValue(rule.TriggerNodeType, out triggers) || triggers.Count == 0)
                { result.SkipReason = "NO_TRIGGER_OBJECTS: no objects of type " + rule.TriggerNodeType; return; }
                result.TriggerCandidates = triggers.Count;
                foreach (var candidate in triggers)
                {
                    trigger = candidate; target = null; phase = "WHEN"; result.EvaluatedTriggers++;
                    if (!rule.EvaluateTrigger(trigger).Value) { result.UnmatchedTriggers++; continue; }
                    result.MatchedTriggers++;
                    phase = "TargetSelection";
                    var targets = rule.TargetSelector(trigger);
                    if (targets == null || targets.Count == 0)
                    {
                        result.MissingTargetTriggers++;
                        if (runOptions.MissingCheckTargetIsError)
                            throw new CrossRuleException("TARGET_NOT_FOUND", "WHEN matched, but CHECK selected no target objects.");
                        continue;
                    }
                    foreach (var checkTarget in targets)
                    {
                        target = checkTarget; phase = "CHECK";
                        if (target == null) throw new CrossRuleException("NULL_TARGET", "CHECK selector returned a null target.");
                        var evaluation = rule.EvaluateCheck(target);
                        if (evaluation.Value) result.PassedChecks++;
                        else
                        {
                            result.FailedChecks++;
                            result.Add(CreateIssue(result, CrossIssueKind.RuleViolation, "CONDITION_FAILED", phase,
                                trigger, target, evaluation.Summary, evaluation.Details, evaluation.Summary.Message));
                        }
                    }
                }
                if (result.CompletedChecks == 0)
                    result.SkipReason = result.MatchedTriggers == 0 ? "WHEN_NOT_MATCHED: no applicable trigger objects." :
                        "NO_CHECK_TARGETS: matching triggers had no CHECK targets (configured as skipped).";
            }
            catch (Exception ex) when (Recoverable(ex))
            {
                // Abort this rule after an execution error (especially a timeout), not the whole batch.
                AddError(result, ex, phase, trigger, target);
            }
            finally { result.Finish(); }
        }
        private static bool Recoverable(Exception ex) => !(ex is OutOfMemoryException) &&
            !(ex is StackOverflowException) && !(ex is AccessViolationException) && !(ex is OperationCanceledException);
        private static void AddError(CrossRuleExecutionResult result, Exception exception, string phase,
            ValidationNode trigger, ValidationNode target)
        {
            result.ErrorCount++; result.Aborted = true;
            var error = exception as CrossRuleException;
            var detail = error?.Detail;
            result.Add(CreateIssue(result, CrossIssueKind.RuleError, error?.Code ?? "UNEXPECTED_ERROR", phase,
                trigger, target, detail, detail == null ? null : new[] { detail }, exception.Message));
        }
        // Report rows must not retain the entire model snapshot through a diagnostic node reference.
        private static CrossConditionDetail DetachDetail(CrossConditionDetail detail) =>
            new CrossConditionDetail
            {
                Expression = detail.Expression, ObjectPath = detail.ObjectPath,
                PropertyName = detail.PropertyName, Operator = detail.Operator,
                ExpectedValue = detail.ExpectedValue, ActualValue = detail.ActualValue,
                Message = detail.Message
            };
        private static CrossValidationIssue CreateIssue(CrossRuleExecutionResult result, CrossIssueKind kind,
            string code, string phase, ValidationNode trigger, ValidationNode target,
            CrossConditionDetail detail, IEnumerable<CrossConditionDetail> details, string message)
        {
            var actualNode = detail?.ContextNode ?? target ?? trigger;
            var parent = actualNode?.Parent;
            var owner = actualNode == null ? null :
                string.Equals(actualNode.NodeType, "Entity", StringComparison.OrdinalIgnoreCase) ? actualNode :
                actualNode.GetNearestAncestorByType("Entity");
            return new CrossValidationIssue
            {
                RuleNumber = result.RuleNumber, RuleText = result.RuleText, Kind = kind,
                ErrorCode = code, Phase = phase, FailedExpression = detail?.Expression ?? "",
                EvaluationTargetPath = target?.Path ?? "",
                TriggerNodeType = trigger?.NodeType ?? "", TriggerObjectId = trigger?.ObjectId ?? "",
                TriggerObjectName = trigger?.Name ?? "", TriggerObjectPath = trigger?.Path ?? "",
                CheckNodeType = actualNode?.NodeType ?? "", CheckObjectId = actualNode?.ObjectId ?? "",
                CheckObjectName = actualNode?.Name ?? "", CheckObjectPath = actualNode?.Path ?? "",
                CheckParentNodeType = parent?.NodeType ?? "", CheckParentObjectId = parent?.ObjectId ?? "",
                CheckParentObjectName = parent?.Name ?? "", CheckParentObjectPath = parent?.Path ?? "",
                CheckOwnerEntityId = owner?.ObjectId ?? "", CheckOwnerEntityName = owner?.Name ?? "",
                CheckOwnerEntityPath = owner?.Path ?? "", PropertyName = detail?.PropertyName ?? "",
                Operator = detail?.Operator ?? "", ExpectedValue = detail?.ExpectedValue ?? "",
                ActualValue = detail?.ActualValue ?? "", Message = message,
                ConditionDetails = (details ?? Enumerable.Empty<CrossConditionDetail>())
                    .Select(DetachDetail).ToList().AsReadOnly()
            };
        }
    }
}
