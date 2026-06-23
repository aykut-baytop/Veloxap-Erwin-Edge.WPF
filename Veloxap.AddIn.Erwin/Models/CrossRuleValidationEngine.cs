using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Veloxap.AddIn.Erwin.Models
{
    internal sealed class CrossValidationIssue
    {
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
    }

    internal sealed class ValidationNode
    {
        public string NodeType { get; set; }
        public string ObjectId { get; set; }
        public string Name { get; set; }
        public string Path { get; set; }

        public ValidationNode Parent { get; set; }
        public ValidationNode Root { get; set; }

        public List<ValidationNode> Children { get; } = new List<ValidationNode>();

        public Dictionary<string, string> Properties { get; }
            = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        private Dictionary<string, List<ValidationNode>> _descendantsByTypeCache;

        public string GetValue(string propertyName)
        {
            if (string.Equals(propertyName, "Name", StringComparison.OrdinalIgnoreCase))
                return Name ?? string.Empty;

            if (Properties.TryGetValue(propertyName, out var value))
                return value ?? string.Empty;

            return string.Empty;
        }

        public IReadOnlyList<ValidationNode> GetDescendantsByType(string nodeType)
        {
            EnsureDescendantCache();

            if (_descendantsByTypeCache.TryGetValue(nodeType, out var list))
                return list;

            return Array.Empty<ValidationNode>();
        }

        public ValidationNode GetParentByType(string nodeType)
        {
            var current = Parent;
            while (current != null)
            {
                if (string.Equals(current.NodeType, nodeType, StringComparison.OrdinalIgnoreCase))
                    return current;
                current = current.Parent;
            }
            return null;
        }

        public ValidationNode GetRootByType(string nodeType)
        {
            if (Root == null)
                return null;

            if (string.IsNullOrWhiteSpace(nodeType))
                return Root;

            return string.Equals(Root.NodeType, nodeType, StringComparison.OrdinalIgnoreCase)
                ? Root
                : null;
        }

        public ValidationNode GetNearestAncestorByType(string nodeType)
        {
            var current = Parent;
            while (current != null)
            {
                if (string.Equals(current.NodeType, nodeType, StringComparison.OrdinalIgnoreCase))
                    return current;
                current = current.Parent;
            }
            return null;
        }

        private void EnsureDescendantCache()
        {
            if (_descendantsByTypeCache != null)
                return;

            _descendantsByTypeCache = new Dictionary<string, List<ValidationNode>>(StringComparer.OrdinalIgnoreCase);
            BuildDescendantCache(this, _descendantsByTypeCache);
        }

        private static void BuildDescendantCache(
            ValidationNode current,
            Dictionary<string, List<ValidationNode>> cache)
        {
            foreach (var child in current.Children)
            {
                if (!cache.TryGetValue(child.NodeType, out var typedList))
                {
                    typedList = new List<ValidationNode>();
                    cache[child.NodeType] = typedList;
                }

                typedList.Add(child);

                var childTempCache = new Dictionary<string, List<ValidationNode>>(StringComparer.OrdinalIgnoreCase);
                BuildDescendantCache(child, childTempCache);

                foreach (var kvp in childTempCache)
                {
                    if (!cache.TryGetValue(kvp.Key, out var targetList))
                    {
                        targetList = new List<ValidationNode>();
                        cache[kvp.Key] = targetList;
                    }

                    targetList.AddRange(kvp.Value);
                }
            }
        }
    }

    internal enum CrossCheckScope
    {
        Self,
        Descendant,
        Parent,
        Root
    }

    internal sealed class RuleCheck
    {
        public CrossCheckScope Scope { get; set; }
        public string TargetNodeType { get; set; }
        public BoolExprNode Expression { get; set; }
    }

    internal sealed class CrossRuleDefinition
    {
        public string RuleText { get; set; }
        public string TriggerNodeType { get; set; }
        public BoolExprNode WhenExpression { get; set; }
        public RuleCheck Check { get; set; }
    }

    internal sealed class CompiledCrossRule
    {
        public string RuleText { get; set; }
        public string TriggerNodeType { get; set; }
        public Func<ValidationNode, bool> TriggerPredicate { get; set; }
        public Func<ValidationNode, IReadOnlyList<ValidationNode>> TargetSelector { get; set; }
        public Func<ValidationNode, bool> CheckPredicate { get; set; }

        public string FirstCheckPropertyName { get; set; }
        public string FirstCheckOperator { get; set; }
        public string FirstCheckExpectedValue { get; set; }
    }

    internal sealed class ValidationNodeIndex
    {
        public ValidationNode Root { get; }
        public IReadOnlyList<ValidationNode> AllNodes { get; }
        public IReadOnlyDictionary<string, List<ValidationNode>> NodesByType { get; }

        public ValidationNodeIndex(ValidationNode root)
        {
            Root = root ?? throw new ArgumentNullException(nameof(root));

            var allNodes = new List<ValidationNode>();
            var nodesByType = new Dictionary<string, List<ValidationNode>>(StringComparer.OrdinalIgnoreCase);

            IndexRecursive(root, allNodes, nodesByType);

            AllNodes = allNodes;
            NodesByType = nodesByType;
        }

        private static void IndexRecursive(
            ValidationNode node,
            List<ValidationNode> allNodes,
            Dictionary<string, List<ValidationNode>> nodesByType)
        {
            allNodes.Add(node);

            if (!nodesByType.TryGetValue(node.NodeType, out var typedList))
            {
                typedList = new List<ValidationNode>();
                nodesByType[node.NodeType] = typedList;
            }

            typedList.Add(node);

            foreach (var child in node.Children)
                IndexRecursive(child, allNodes, nodesByType);
        }
    }

    internal static class ValidationNodeBuilder
    {
        public static ValidationNode Build(ModelInfo modelInfo)
        {
            if (modelInfo == null)
                throw new ArgumentNullException(nameof(modelInfo));

            var root = new ValidationNode
            {
                NodeType = "Model",
                ObjectId = Safe(modelInfo.getoObjectId()),
                Name = Safe(modelInfo.getoName()),
                Path = "Model:" + Safe(modelInfo.getoName())
            };

            root.Root = root;
            LoadModelProperties(modelInfo, root);

            var children = modelInfo.getoModelObject();
            if (children != null)
            {
                foreach (var child in children)
                    BuildObjectNode(child, root, root);
            }

            return root;
        }

        private static void BuildObjectNode(ModelObject obj, ValidationNode parent, ValidationNode root)
        {
            if (obj == null)
                return;

            var node = new ValidationNode
            {
                NodeType = Safe(obj.getoClassName()),
                ObjectId = Safe(obj.getoObjectId()),
                Name = Safe(obj.getoName()),
                Parent = parent,
                Root = root,
                Path = parent.Path + "/" + Safe(obj.getoClassName()) + ":" + Safe(obj.getoName())
            };

            LoadObjectProperties(obj, node);
            parent.Children.Add(node);

            var children = obj.getoModelObject();
            if (children == null)
                return;

            foreach (var child in children)
                BuildObjectNode(child, node, root);
        }

        private static void LoadModelProperties(ModelInfo modelInfo, ValidationNode node)
        {
            var props = modelInfo.getoObjectProperty();
            if (props == null)
                return;

            foreach (var prop in props)
            {
                if (prop == null)
                    continue;

                string propName = Safe(prop.getoPropertyClassName());
                if (string.IsNullOrWhiteSpace(propName))
                    continue;

                if (!node.Properties.ContainsKey(propName))
                    node.Properties[propName] = GetPropertyEffectiveValue(prop);
            }
        }

        private static void LoadObjectProperties(ModelObject obj, ValidationNode node)
        {
            var props = obj.getoObjectProperty();
            if (props == null)
                return;

            foreach (var prop in props)
            {
                if (prop == null)
                    continue;

                string propName = Safe(prop.getoPropertyClassName());
                if (string.IsNullOrWhiteSpace(propName))
                    continue;

                if (!node.Properties.ContainsKey(propName))
                    node.Properties[propName] = GetPropertyEffectiveValue(prop);
            }
        }

        private static string GetPropertyEffectiveValue(ObjectProperty prop)
        {
            string asString = Safe(prop.getoPropertyFormatAsString());
            if (!string.IsNullOrWhiteSpace(asString))
                return asString;

            return Safe(prop.getoPropertyValue());
        }

        private static string Safe(string value) => value ?? string.Empty;
    }

    // =========================================================
    // AST - Boolean expressions
    // =========================================================

    internal abstract class BoolExprNode { }

    internal sealed class ComparisonExprNode : BoolExprNode
    {
        public string Left { get; set; }
        public string Operator { get; set; }
        public ValueExprNode Right { get; set; }
    }

    internal sealed class BinaryExprNode : BoolExprNode
    {
        public string Operator { get; set; }
        public BoolExprNode Left { get; set; }
        public BoolExprNode Right { get; set; }
    }

    internal sealed class UnaryExprNode : BoolExprNode
    {
        public string Operator { get; set; }
        public BoolExprNode Operand { get; set; }
    }

    internal sealed class ExistsExprNode : BoolExprNode
    {
        public string ScopeExpression { get; set; }
        public BoolExprNode InnerExpression { get; set; }
    }

    internal sealed class CountExprNode : BoolExprNode
    {
        public string ScopeExpression { get; set; }
        public BoolExprNode InnerExpression { get; set; }
        public string CountOperator { get; set; }
        public ValueExprNode CountValue { get; set; }
    }

    // =========================================================
    // AST - Value expressions
    // =========================================================

    internal abstract class ValueExprNode { }

    internal sealed class LiteralValueExprNode : ValueExprNode
    {
        public string Value { get; set; }
    }

    internal sealed class OperandValueExprNode : ValueExprNode
    {
        public string Operand { get; set; }
    }

    internal sealed class ConcatValueExprNode : ValueExprNode
    {
        public List<ValueExprNode> Parts { get; } = new List<ValueExprNode>();
    }

    // =========================================================
    // Tokenizer
    // =========================================================

    internal enum ExprTokenType
    {
        Identifier,
        Operator,
        And,
        Or,
        Not,
        Exists,
        Count,
        Concat,
        Comma,
        LParen,
        RParen
    }

    internal sealed class ExprToken
    {
        public ExprTokenType Type { get; set; }
        public string Value { get; set; }
    }

    internal static class BooleanExpressionTokenizer
    {
        private static readonly Regex TokenRegex = new Regex(
            @"PARENT\([A-Za-z_][A-Za-z0-9_]*\)\.[^\s(),]+|ROOT\([A-Za-z_][A-Za-z0-9_]*\)\.[^\s(),]+|DESCENDANT\([A-Za-z_][A-Za-z0-9_]*\)\.[^\s(),]+|SELF\.[^\s(),]+|PARENT\([A-Za-z_][A-Za-z0-9_]*\)|ROOT\([A-Za-z_][A-Za-z0-9_]*\)|DESCENDANT\([A-Za-z_][A-Za-z0-9_]*\)|""(?:\\.|[^""])*""|'(?:\\.|[^'])*'|,|\(|\)|<=|>=|!=|=|<|>|\bAND\b|\bOR\b|\bNOT\b|\bEXISTS\b|\bCOUNT\b|\bCONCAT\b|\bMATCHES\b|\bCONTAINS\b|\bSTARTSWITH\b|\bENDSWITH\b|[^\s(),]+",
            RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.CultureInvariant);

        public static List<ExprToken> Tokenize(string text)
        {
            var result = new List<ExprToken>();

            if (string.IsNullOrWhiteSpace(text))
                return result;

            foreach (Match match in TokenRegex.Matches(text))
            {
                string token = match.Value;

                if (string.Equals(token, "AND", StringComparison.OrdinalIgnoreCase))
                    result.Add(new ExprToken { Type = ExprTokenType.And, Value = "AND" });
                else if (string.Equals(token, "OR", StringComparison.OrdinalIgnoreCase))
                    result.Add(new ExprToken { Type = ExprTokenType.Or, Value = "OR" });
                else if (string.Equals(token, "NOT", StringComparison.OrdinalIgnoreCase))
                    result.Add(new ExprToken { Type = ExprTokenType.Not, Value = "NOT" });
                else if (string.Equals(token, "EXISTS", StringComparison.OrdinalIgnoreCase))
                    result.Add(new ExprToken { Type = ExprTokenType.Exists, Value = "EXISTS" });
                else if (string.Equals(token, "COUNT", StringComparison.OrdinalIgnoreCase))
                    result.Add(new ExprToken { Type = ExprTokenType.Count, Value = "COUNT" });
                else if (string.Equals(token, "CONCAT", StringComparison.OrdinalIgnoreCase))
                    result.Add(new ExprToken { Type = ExprTokenType.Concat, Value = "CONCAT" });
                else if (token == ",")
                    result.Add(new ExprToken { Type = ExprTokenType.Comma, Value = "," });
                else if (string.Equals(token, "MATCHES", StringComparison.OrdinalIgnoreCase) ||
                         string.Equals(token, "CONTAINS", StringComparison.OrdinalIgnoreCase) ||
                         string.Equals(token, "STARTSWITH", StringComparison.OrdinalIgnoreCase) ||
                         string.Equals(token, "ENDSWITH", StringComparison.OrdinalIgnoreCase) ||
                         token == "=" || token == "!=" ||
                         token == "<" || token == "<=" ||
                         token == ">" || token == ">=")
                    result.Add(new ExprToken { Type = ExprTokenType.Operator, Value = token });
                else if (token == "(")
                    result.Add(new ExprToken { Type = ExprTokenType.LParen, Value = token });
                else if (token == ")")
                    result.Add(new ExprToken { Type = ExprTokenType.RParen, Value = token });
                else
                    result.Add(new ExprToken { Type = ExprTokenType.Identifier, Value = token });
            }

            return result;
        }
    }

    // =========================================================
    // Parser
    // =========================================================

    internal sealed class BooleanExpressionParser
    {
        private readonly List<ExprToken> _tokens;
        private int _position;

        public BooleanExpressionParser(List<ExprToken> tokens)
        {
            _tokens = tokens ?? throw new ArgumentNullException(nameof(tokens));
        }

        public BoolExprNode Parse()
        {
            if (_tokens.Count == 0)
                throw new Exception("Expression bos.");

            var expr = ParseOr();

            if (_position != _tokens.Count)
                throw new Exception("Expression parse edilemedi. Beklenmeyen token: " + _tokens[_position].Value);

            return expr;
        }

        private BoolExprNode ParseOr()
        {
            var left = ParseAnd();

            while (Match(ExprTokenType.Or))
            {
                var right = ParseAnd();
                left = new BinaryExprNode { Operator = "OR", Left = left, Right = right };
            }

            return left;
        }

        private BoolExprNode ParseAnd()
        {
            var left = ParseUnary();

            while (Match(ExprTokenType.And))
            {
                var right = ParseUnary();
                left = new BinaryExprNode { Operator = "AND", Left = left, Right = right };
            }

            return left;
        }

        private BoolExprNode ParseUnary()
        {
            if (Match(ExprTokenType.Not))
            {
                return new UnaryExprNode
                {
                    Operator = "NOT",
                    Operand = ParseUnary()
                };
            }

            return ParsePrimary();
        }

        private BoolExprNode ParsePrimary()
        {
            if (Match(ExprTokenType.LParen))
            {
                var expr = ParseOr();
                Expect(ExprTokenType.RParen, "Kapanis parantezi ')' bekleniyordu.");
                return expr;
            }

            if (Match(ExprTokenType.Exists))
                return ParseExists();

            if (Match(ExprTokenType.Count))
                return ParseCount();

            return ParseComparison();
        }

        private BoolExprNode ParseExists()
        {
            var scopeExpr = Expect(ExprTokenType.Identifier, "EXISTS sonrasi scope bekleniyordu.").Value;
            Expect(ExprTokenType.LParen, "EXISTS sonrasi '(' bekleniyordu.");
            var inner = ParseOr();
            Expect(ExprTokenType.RParen, "EXISTS ic ifadesi icin ')' bekleniyordu.");

            return new ExistsExprNode
            {
                ScopeExpression = scopeExpr,
                InnerExpression = inner
            };
        }

        private BoolExprNode ParseCount()
        {
            var scopeExpr = Expect(ExprTokenType.Identifier, "COUNT sonrasi scope bekleniyordu.").Value;
            Expect(ExprTokenType.LParen, "COUNT sonrasi '(' bekleniyordu.");
            var inner = ParseOr();
            Expect(ExprTokenType.RParen, "COUNT ic ifadesi icin ')' bekleniyordu.");

            var countOperator = Expect(ExprTokenType.Operator, "COUNT sonrasi sayi kiyas operatoru bekleniyordu.").Value;
            var countValue = ParseValueExpr();

            return new CountExprNode
            {
                ScopeExpression = scopeExpr,
                InnerExpression = inner,
                CountOperator = countOperator,
                CountValue = countValue
            };
        }

        private BoolExprNode ParseComparison()
        {
            var left = Expect(ExprTokenType.Identifier, "Sol operand bekleniyordu.").Value;
            var op = Expect(ExprTokenType.Operator, "Operator bekleniyordu.").Value;

            ValueExprNode right;

            if (!IsEnd() &&
                Peek().Type != ExprTokenType.And &&
                Peek().Type != ExprTokenType.Or &&
                Peek().Type != ExprTokenType.RParen)
            {
                right = ParseValueExpr();
            }
            else
            {
                right = new LiteralValueExprNode { Value = string.Empty };
            }

            return new ComparisonExprNode
            {
                Left = left,
                Operator = op,
                Right = right
            };
        }

        private ValueExprNode ParseValueExpr()
        {
            if (Match(ExprTokenType.Concat))
            {
                Expect(ExprTokenType.LParen, "CONCAT sonrasi '(' bekleniyordu.");

                var concat = new ConcatValueExprNode();
                concat.Parts.Add(ParseValueExpr());

                while (Match(ExprTokenType.Comma))
                    concat.Parts.Add(ParseValueExpr());

                Expect(ExprTokenType.RParen, "CONCAT icin ')' bekleniyordu.");
                return concat;
            }

            var token = Expect(ExprTokenType.Identifier, "Value expression bekleniyordu.").Value;

            if (IsOperandExpression(token))
                return new OperandValueExprNode { Operand = token };

            return new LiteralValueExprNode { Value = Unquote(token) };
        }

        private static bool IsOperandExpression(string token)
        {
            return token.StartsWith("SELF.", StringComparison.OrdinalIgnoreCase) ||
                   token.StartsWith("PARENT(", StringComparison.OrdinalIgnoreCase) ||
                   token.StartsWith("ROOT(", StringComparison.OrdinalIgnoreCase) ||
                   token.StartsWith("DESCENDANT(", StringComparison.OrdinalIgnoreCase);
        }

        private bool Match(ExprTokenType type)
        {
            if (IsEnd() || Peek().Type != type)
                return false;

            _position++;
            return true;
        }

        private ExprToken Expect(ExprTokenType type, string message)
        {
            if (IsEnd() || Peek().Type != type)
                throw new Exception(message);

            return _tokens[_position++];
        }

        private ExprToken Peek() => _tokens[_position];
        private bool IsEnd() => _position >= _tokens.Count;

        private static string Unquote(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return string.Empty;

            text = text.Trim();

            if (text.Length >= 2 && text.StartsWith("\"") && text.EndsWith("\""))
                return text.Substring(1, text.Length - 2);

            if (text.Length >= 2 && text.StartsWith("'") && text.EndsWith("'"))
                return text.Substring(1, text.Length - 2);

            return text;
        }
    }

    // =========================================================
    // Rule parser
    // =========================================================

    internal static class CrossRuleParser
    {
        public static CrossRuleDefinition Parse(string ruleText)
        {
            if (string.IsNullOrWhiteSpace(ruleText))
                throw new ArgumentException("Rule text bos olamaz.");

            string trimmed = ruleText.Trim();

            int whenIndex = CultureInfo.InvariantCulture.CompareInfo.IndexOf(
                trimmed, "WHEN ", CompareOptions.IgnoreCase);

            int checkIndex = CultureInfo.InvariantCulture.CompareInfo.IndexOf(
                trimmed, " CHECK ", CompareOptions.IgnoreCase);

            if (whenIndex != 0 || checkIndex < 0)
                throw new Exception("Rule formati hatali. Beklenen format: WHEN <TriggerNodeType> <WhenExpression> CHECK <CheckExpression>");

            string whenPartFull = trimmed.Substring(5, checkIndex - 5).Trim();
            string checkPart = trimmed.Substring(checkIndex + 7).Trim();

            SplitWhenPart(whenPartFull, out string triggerNodeType, out string whenExpressionText);

            return new CrossRuleDefinition
            {
                RuleText = ruleText,
                TriggerNodeType = triggerNodeType,
                WhenExpression = ParseBooleanExpression(whenExpressionText),
                Check = ParseCheck(checkPart)
            };
        }

        private static void SplitWhenPart(string whenPartFull, out string triggerNodeType, out string whenExpressionText)
        {
            int firstSpace = whenPartFull.IndexOf(' ');
            if (firstSpace <= 0 || firstSpace >= whenPartFull.Length - 1)
                throw new Exception("WHEN kismi hatali. Beklenen format: WHEN <TriggerNodeType> <WhenExpression>");

            triggerNodeType = whenPartFull.Substring(0, firstSpace).Trim();
            whenExpressionText = whenPartFull.Substring(firstSpace + 1).Trim();

            if (string.IsNullOrWhiteSpace(triggerNodeType))
                throw new Exception("WHEN trigger node type bos olamaz.");

            if (string.IsNullOrWhiteSpace(whenExpressionText))
                throw new Exception("WHEN expression bos olamaz.");
        }

        private static RuleCheck ParseCheck(string text)
        {
            if (text.StartsWith("DESCENDANT(", StringComparison.OrdinalIgnoreCase))
            {
                int closeParen = text.IndexOf(')');
                if (closeParen < 0)
                    throw new Exception("CHECK kismi hatali. DESCENDANT(...) kapanisi yok.");

                string nodeType = text.Substring("DESCENDANT(".Length, closeParen - "DESCENDANT(".Length).Trim();
                string rest = text.Substring(closeParen + 1).Trim();

                if (rest.StartsWith("(", StringComparison.Ordinal))
                {
                    var inner = ParseGroupedRemainder(rest);
                    return new RuleCheck
                    {
                        Scope = CrossCheckScope.Descendant,
                        TargetNodeType = nodeType,
                        Expression = inner
                    };
                }

                if (!rest.StartsWith(".", StringComparison.Ordinal))
                    throw new Exception("CHECK kismi hatali. Beklenen: DESCENDANT(Attribute).Property ... veya DESCENDANT(Attribute) ( ... )");

                rest = rest.Substring(1).Trim();

                return new RuleCheck
                {
                    Scope = CrossCheckScope.Descendant,
                    TargetNodeType = nodeType,
                    Expression = ParseBooleanExpression(rest)
                };
            }

            if (text.StartsWith("PARENT(", StringComparison.OrdinalIgnoreCase))
            {
                int closeParen = text.IndexOf(')');
                if (closeParen < 0)
                    throw new Exception("CHECK kismi hatali. PARENT(...) kapanisi yok.");

                string nodeType = text.Substring("PARENT(".Length, closeParen - "PARENT(".Length).Trim();
                string rest = text.Substring(closeParen + 1).Trim();

                if (rest.StartsWith("(", StringComparison.Ordinal))
                {
                    var inner = ParseGroupedRemainder(rest);
                    return new RuleCheck
                    {
                        Scope = CrossCheckScope.Parent,
                        TargetNodeType = nodeType,
                        Expression = inner
                    };
                }

                if (!rest.StartsWith(".", StringComparison.Ordinal))
                    throw new Exception("CHECK kismi hatali. Beklenen: PARENT(Entity).Property ... veya PARENT(Entity) ( ... )");

                rest = rest.Substring(1).Trim();

                return new RuleCheck
                {
                    Scope = CrossCheckScope.Parent,
                    TargetNodeType = nodeType,
                    Expression = ParseBooleanExpression(rest)
                };
            }

            if (text.StartsWith("ROOT(", StringComparison.OrdinalIgnoreCase))
            {
                int closeParen = text.IndexOf(')');
                if (closeParen < 0)
                    throw new Exception("CHECK kismi hatali. ROOT(...) kapanisi yok.");

                string nodeType = text.Substring("ROOT(".Length, closeParen - "ROOT(".Length).Trim();
                string rest = text.Substring(closeParen + 1).Trim();

                if (rest.StartsWith("(", StringComparison.Ordinal))
                {
                    var inner = ParseGroupedRemainder(rest);
                    return new RuleCheck
                    {
                        Scope = CrossCheckScope.Root,
                        TargetNodeType = nodeType,
                        Expression = inner
                    };
                }

                if (!rest.StartsWith(".", StringComparison.Ordinal))
                    throw new Exception("CHECK kismi hatali. Beklenen: ROOT(Model).Property ... veya ROOT(Model) ( ... )");

                rest = rest.Substring(1).Trim();

                return new RuleCheck
                {
                    Scope = CrossCheckScope.Root,
                    TargetNodeType = nodeType,
                    Expression = ParseBooleanExpression(rest)
                };
            }

            if (text.StartsWith("SELF.", StringComparison.OrdinalIgnoreCase) ||
                text.StartsWith("NOT ", StringComparison.OrdinalIgnoreCase) ||
                text.StartsWith("EXISTS ", StringComparison.OrdinalIgnoreCase) ||
                text.StartsWith("COUNT ", StringComparison.OrdinalIgnoreCase))
            {
                return new RuleCheck
                {
                    Scope = CrossCheckScope.Self,
                    TargetNodeType = string.Empty,
                    Expression = ParseBooleanExpression(text)
                };
            }

            throw new Exception("CHECK scope desteklenmiyor.");
        }

        private static BoolExprNode ParseGroupedRemainder(string text)
        {
            text = text.Trim();
            if (!text.StartsWith("(") || !text.EndsWith(")"))
                throw new Exception("Scope sonrasi grup formati hatali.");

            string inner = text.Substring(1, text.Length - 2).Trim();
            return ParseBooleanExpression(inner);
        }

        private static BoolExprNode ParseBooleanExpression(string text)
        {
            var tokens = BooleanExpressionTokenizer.Tokenize(text);
            var parser = new BooleanExpressionParser(tokens);
            return parser.Parse();
        }
    }

    // =========================================================
    // Regex cache
    // =========================================================

    internal static class RegexCache
    {
        private static readonly ConcurrentDictionary<string, Regex> _cache =
            new ConcurrentDictionary<string, Regex>(StringComparer.Ordinal);

        public static Regex Get(string pattern)
        {
            pattern = pattern ?? string.Empty;

            return _cache.GetOrAdd(
                pattern,
                p => new Regex(
                    p,
                    RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled));
        }
    }

    // =========================================================
    // Compiler
    // =========================================================

    internal static class CrossRuleCompiler
    {
        public static CompiledCrossRule Compile(CrossRuleDefinition rule)
        {
            if (rule == null)
                throw new ArgumentNullException(nameof(rule));

            var firstCheckComparison = FindFirstRelevant(rule.Check.Expression);

            return new CompiledCrossRule
            {
                RuleText = rule.RuleText,
                TriggerNodeType = rule.TriggerNodeType,
                TriggerPredicate = BuildPredicate(rule.WhenExpression, ResolveOperandAccessor),
                TargetSelector = BuildTargetSelector(rule.Check),
                CheckPredicate = BuildPredicate(rule.Check.Expression, ResolveOperandAccessor),
                FirstCheckPropertyName = firstCheckComparison?.PropertyName ?? string.Empty,
                FirstCheckOperator = firstCheckComparison?.Operator ?? string.Empty,
                FirstCheckExpectedValue = firstCheckComparison?.ExpectedValue ?? string.Empty
            };
        }

        private sealed class FirstRelevantInfo
        {
            public string PropertyName { get; set; }
            public string Operator { get; set; }
            public string ExpectedValue { get; set; }
        }

        private sealed class NodeValueAccessor
        {
            public Func<ValidationNode, string> GetValue { get; set; }
        }

        private static FirstRelevantInfo FindFirstRelevant(BoolExprNode expr)
        {
            if (expr is ComparisonExprNode cmp)
            {
                return new FirstRelevantInfo
                {
                    PropertyName = NormalizeOperandPropertyName(cmp.Left),
                    Operator = cmp.Operator,
                    ExpectedValue = ValueExprToText(cmp.Right)
                };
            }

            if (expr is UnaryExprNode un)
                return FindFirstRelevant(un.Operand);

            if (expr is ExistsExprNode ex)
                return FindFirstRelevant(ex.InnerExpression);

            if (expr is CountExprNode cnt)
                return FindFirstRelevant(cnt.InnerExpression);

            if (expr is BinaryExprNode bin)
                return FindFirstRelevant(bin.Left) ?? FindFirstRelevant(bin.Right);

            return null;
        }

        private static string NormalizeOperandPropertyName(string operand)
        {
            if (string.IsNullOrWhiteSpace(operand))
                return string.Empty;

            if (operand.StartsWith("SELF.", StringComparison.OrdinalIgnoreCase))
                return operand.Substring(5).Trim();

            if (operand.StartsWith("PARENT(", StringComparison.OrdinalIgnoreCase) ||
                operand.StartsWith("ROOT(", StringComparison.OrdinalIgnoreCase) ||
                operand.StartsWith("DESCENDANT(", StringComparison.OrdinalIgnoreCase))
            {
                int closeParen = operand.IndexOf(')');
                if (closeParen < 0 || closeParen + 2 > operand.Length)
                    return operand;

                return operand.Substring(closeParen + 2).Trim();
            }

            return operand;
        }

        private static string ValueExprToText(ValueExprNode valueExpr)
        {
            if (valueExpr == null)
                return string.Empty;

            if (valueExpr is LiteralValueExprNode lit)
                return lit.Value ?? string.Empty;

            if (valueExpr is OperandValueExprNode op)
                return op.Operand ?? string.Empty;

            if (valueExpr is ConcatValueExprNode concat)
                return "CONCAT(" + string.Join(", ", concat.Parts.Select(ValueExprToText)) + ")";

            return string.Empty;
        }

        private static Func<ValidationNode, bool> BuildPredicate(
            BoolExprNode expr,
            Func<string, NodeValueAccessor> accessorResolver)
        {
            if (expr is ComparisonExprNode cmp)
            {
                NodeValueAccessor leftAccessor = accessorResolver(cmp.Left);
                Func<ValidationNode, string> rightEvaluator = BuildValueEvaluator(cmp.Right, accessorResolver);

                if (string.Equals(cmp.Operator, "matches", StringComparison.OrdinalIgnoreCase))
                {
                    // rightEvaluator runtime value olabilir, yine de regex cache kullaniyoruz
                    return node =>
                    {
                        string actualValue = leftAccessor.GetValue(node);
                        string expectedValue = rightEvaluator(node);
                        var regex = RegexCache.Get(expectedValue);
                        return regex.IsMatch(Safe(actualValue));
                    };
                }

                if (string.Equals(cmp.Operator, "CONTAINS", StringComparison.OrdinalIgnoreCase))
                {
                    return node =>
                    {
                        string actualValue = leftAccessor.GetValue(node);
                        string expectedValue = rightEvaluator(node);
                        return Safe(actualValue).IndexOf(Safe(expectedValue), StringComparison.OrdinalIgnoreCase) >= 0;
                    };
                }

                if (string.Equals(cmp.Operator, "STARTSWITH", StringComparison.OrdinalIgnoreCase))
                {
                    return node =>
                    {
                        string actualValue = leftAccessor.GetValue(node);
                        string expectedValue = rightEvaluator(node);
                        return Safe(actualValue).StartsWith(Safe(expectedValue), StringComparison.OrdinalIgnoreCase);
                    };
                }

                if (string.Equals(cmp.Operator, "ENDSWITH", StringComparison.OrdinalIgnoreCase))
                {
                    return node =>
                    {
                        string actualValue = leftAccessor.GetValue(node);
                        string expectedValue = rightEvaluator(node);
                        return Safe(actualValue).EndsWith(Safe(expectedValue), StringComparison.OrdinalIgnoreCase);
                    };
                }

                return node =>
                {
                    string actualValue = leftAccessor.GetValue(node);
                    string expectedValue = rightEvaluator(node);
                    return EvaluateComparison(actualValue, cmp.Operator, expectedValue);
                };
            }

            if (expr is ExistsExprNode ex)
                return BuildExistsPredicate(ex, accessorResolver);

            if (expr is CountExprNode cnt)
                return BuildCountPredicate(cnt, accessorResolver);

            if (expr is UnaryExprNode un)
            {
                var inner = BuildPredicate(un.Operand, accessorResolver);

                if (string.Equals(un.Operator, "NOT", StringComparison.OrdinalIgnoreCase))
                    return node => !inner(node);

                throw new Exception("Desteklenmeyen unary operator: " + un.Operator);
            }

            if (expr is BinaryExprNode bin)
            {
                var leftFn = BuildPredicate(bin.Left, accessorResolver);
                var rightFn = BuildPredicate(bin.Right, accessorResolver);

                if (string.Equals(bin.Operator, "AND", StringComparison.OrdinalIgnoreCase))
                    return node => leftFn(node) && rightFn(node);

                if (string.Equals(bin.Operator, "OR", StringComparison.OrdinalIgnoreCase))
                    return node => leftFn(node) || rightFn(node);

                throw new Exception("Desteklenmeyen binary operator: " + bin.Operator);
            }

            throw new Exception("Desteklenmeyen expression tipi.");
        }

        private static Func<ValidationNode, string> BuildValueEvaluator(
            ValueExprNode valueExpr,
            Func<string, NodeValueAccessor> accessorResolver)
        {
            if (valueExpr == null)
                return _ => string.Empty;

            if (valueExpr is LiteralValueExprNode lit)
            {
                string value = lit.Value ?? string.Empty;
                return _ => value;
            }

            if (valueExpr is OperandValueExprNode op)
            {
                var accessor = accessorResolver(op.Operand);
                return node => accessor.GetValue(node);
            }

            if (valueExpr is ConcatValueExprNode concat)
            {
                var evaluators = concat.Parts.Select(x => BuildValueEvaluator(x, accessorResolver)).ToArray();

                return node =>
                {
                    var sb = new StringBuilder();
                    for (int i = 0; i < evaluators.Length; i++)
                        sb.Append(evaluators[i](node));
                    return sb.ToString();
                };
            }

            throw new Exception("Desteklenmeyen value expression tipi.");
        }

        private static Func<ValidationNode, bool> BuildExistsPredicate(
            ExistsExprNode expr,
            Func<string, NodeValueAccessor> accessorResolver)
        {
            // OPT-2 ve OPT-3: inner predicate ve scope resolver compile-time hazirlanir
            var targetResolver = BuildScopeResolver(expr.ScopeExpression);
            var innerPredicate = BuildPredicate(expr.InnerExpression, accessorResolver);

            return node =>
            {
                var targets = targetResolver(node);
                if (targets.Count == 0)
                    return false;

                for (int i = 0; i < targets.Count; i++)
                {
                    if (innerPredicate(targets[i]))
                        return true;
                }

                return false;
            };
        }

        private static Func<ValidationNode, bool> BuildCountPredicate(
            CountExprNode expr,
            Func<string, NodeValueAccessor> accessorResolver)
        {
            // OPT-2 ve OPT-3
            var targetResolver = BuildScopeResolver(expr.ScopeExpression);
            var innerPredicate = BuildPredicate(expr.InnerExpression, accessorResolver);
            var countValueEvaluator = BuildValueEvaluator(expr.CountValue, accessorResolver);

            return node =>
            {
                var targets = targetResolver(node);
                int count = 0;

                for (int i = 0; i < targets.Count; i++)
                {
                    if (innerPredicate(targets[i]))
                        count++;
                }

                string countValue = countValueEvaluator(node);

                return EvaluateComparison(
                    count.ToString(CultureInfo.InvariantCulture),
                    expr.CountOperator,
                    countValue);
            };
        }

        private static Func<ValidationNode, IReadOnlyList<ValidationNode>> BuildScopeResolver(string scopeExpression)
        {
            if (scopeExpression.StartsWith("DESCENDANT(", StringComparison.OrdinalIgnoreCase))
            {
                int closeParen = scopeExpression.IndexOf(')');
                if (closeParen < 0)
                    throw new Exception("Scope expression hatali: " + scopeExpression);

                string nodeType = scopeExpression.Substring("DESCENDANT(".Length, closeParen - "DESCENDANT(".Length).Trim();
                return node => node.GetDescendantsByType(nodeType);
            }

            if (scopeExpression.StartsWith("PARENT(", StringComparison.OrdinalIgnoreCase))
            {
                int closeParen = scopeExpression.IndexOf(')');
                if (closeParen < 0)
                    throw new Exception("Scope expression hatali: " + scopeExpression);

                string nodeType = scopeExpression.Substring("PARENT(".Length, closeParen - "PARENT(".Length).Trim();
                return node =>
                {
                    var parent = node.GetParentByType(nodeType);
                    return parent == null ? Array.Empty<ValidationNode>() : new[] { parent };
                };
            }

            if (scopeExpression.StartsWith("ROOT(", StringComparison.OrdinalIgnoreCase))
            {
                int closeParen = scopeExpression.IndexOf(')');
                if (closeParen < 0)
                    throw new Exception("Scope expression hatali: " + scopeExpression);

                string nodeType = scopeExpression.Substring("ROOT(".Length, closeParen - "ROOT(".Length).Trim();
                return node =>
                {
                    var root = node.GetRootByType(nodeType);
                    return root == null ? Array.Empty<ValidationNode>() : new[] { root };
                };
            }

            if (string.Equals(scopeExpression, "SELF", StringComparison.OrdinalIgnoreCase))
                return node => new[] { node };

            throw new Exception("Desteklenmeyen scope expression: " + scopeExpression);
        }

        private static NodeValueAccessor ResolveOperandAccessor(string leftOperand)
        {
            // compile-time accessor resolver
            if (leftOperand.StartsWith("SELF.", StringComparison.OrdinalIgnoreCase))
            {
                string propertyName = leftOperand.Substring(5).Trim();
                return new NodeValueAccessor
                {
                    GetValue = node => node.GetValue(propertyName)
                };
            }

            if (leftOperand.StartsWith("PARENT(", StringComparison.OrdinalIgnoreCase))
            {
                int closeParen = leftOperand.IndexOf(')');
                if (closeParen < 0)
                    throw new Exception("Operand PARENT(...) hatali: " + leftOperand);

                string parentNodeType = leftOperand.Substring("PARENT(".Length, closeParen - "PARENT(".Length).Trim();
                string rest = leftOperand.Substring(closeParen + 1).Trim();

                if (!rest.StartsWith(".", StringComparison.Ordinal))
                    throw new Exception("Operand PARENT(...) formati hatali: " + leftOperand);

                string propertyName = rest.Substring(1).Trim();

                return new NodeValueAccessor
                {
                    GetValue = node =>
                    {
                        var parent = node.GetParentByType(parentNodeType);
                        return parent == null ? string.Empty : parent.GetValue(propertyName);
                    }
                };
            }

            if (leftOperand.StartsWith("ROOT(", StringComparison.OrdinalIgnoreCase))
            {
                int closeParen = leftOperand.IndexOf(')');
                if (closeParen < 0)
                    throw new Exception("Operand ROOT(...) hatali: " + leftOperand);

                string rootNodeType = leftOperand.Substring("ROOT(".Length, closeParen - "ROOT(".Length).Trim();
                string rest = leftOperand.Substring(closeParen + 1).Trim();

                if (!rest.StartsWith(".", StringComparison.Ordinal))
                    throw new Exception("Operand ROOT(...) formati hatali: " + leftOperand);

                string propertyName = rest.Substring(1).Trim();

                return new NodeValueAccessor
                {
                    GetValue = node =>
                    {
                        var root = node.GetRootByType(rootNodeType);
                        return root == null ? string.Empty : root.GetValue(propertyName);
                    }
                };
            }

            if (leftOperand.StartsWith("DESCENDANT(", StringComparison.OrdinalIgnoreCase))
            {
                int closeParen = leftOperand.IndexOf(')');
                if (closeParen < 0)
                    throw new Exception("Operand DESCENDANT(...) hatali: " + leftOperand);

                string descendantNodeType = leftOperand.Substring("DESCENDANT(".Length, closeParen - "DESCENDANT(".Length).Trim();
                string rest = leftOperand.Substring(closeParen + 1).Trim();

                if (!rest.StartsWith(".", StringComparison.Ordinal))
                    throw new Exception("Operand DESCENDANT(...) formati hatali: " + leftOperand);

                string propertyName = rest.Substring(1).Trim();

                return new NodeValueAccessor
                {
                    GetValue = node =>
                    {
                        var first = node.GetDescendantsByType(descendantNodeType).FirstOrDefault();
                        return first == null ? string.Empty : first.GetValue(propertyName);
                    }
                };
            }

            return new NodeValueAccessor
            {
                GetValue = node => node.GetValue(leftOperand)
            };
        }

        private static Func<ValidationNode, IReadOnlyList<ValidationNode>> BuildTargetSelector(RuleCheck check)
        {
            if (check.Scope == CrossCheckScope.Self)
                return node => new[] { node };

            if (check.Scope == CrossCheckScope.Descendant)
            {
                string nodeType = check.TargetNodeType;
                return node => node.GetDescendantsByType(nodeType);
            }

            if (check.Scope == CrossCheckScope.Parent)
            {
                string nodeType = check.TargetNodeType;
                return node =>
                {
                    var parent = node.GetParentByType(nodeType);
                    return parent == null ? Array.Empty<ValidationNode>() : new[] { parent };
                };
            }

            if (check.Scope == CrossCheckScope.Root)
            {
                string nodeType = check.TargetNodeType;
                return node =>
                {
                    var root = node.GetRootByType(nodeType);
                    return root == null ? Array.Empty<ValidationNode>() : new[] { root };
                };
            }

            throw new Exception("Desteklenmeyen scope.");
        }

        private static bool EvaluateComparison(string actualValue, string op, string expectedValue)
        {
            switch (op)
            {
                case "=":
                    return CompareEquals(actualValue, expectedValue);
                case "!=":
                    return !CompareEquals(actualValue, expectedValue);
                case "<":
                    return CompareNumeric(actualValue, expectedValue, (a, b) => a < b);
                case "<=":
                    return CompareNumeric(actualValue, expectedValue, (a, b) => a <= b);
                case ">":
                    return CompareNumeric(actualValue, expectedValue, (a, b) => a > b);
                case ">=":
                    return CompareNumeric(actualValue, expectedValue, (a, b) => a >= b);
                default:
                    throw new Exception("Desteklenmeyen operator: " + op);
            }
        }

        private static bool CompareEquals(string actualValue, string expectedValue)
        {
            actualValue = Safe(actualValue).Trim();
            expectedValue = Safe(expectedValue).Trim();

            if (TryParseBool(actualValue, out bool actualBool) &&
                TryParseBool(expectedValue, out bool expectedBool))
            {
                return actualBool == expectedBool;
            }

            if (TryParseDecimal(actualValue, out decimal actualDecimal) &&
                TryParseDecimal(expectedValue, out decimal expectedDecimal))
            {
                return actualDecimal == expectedDecimal;
            }

            return string.Equals(actualValue, expectedValue, StringComparison.OrdinalIgnoreCase);
        }

        private static bool CompareNumeric(string actualValue, string expectedValue, Func<decimal, decimal, bool> comparer)
        {
            if (!TryParseDecimal(actualValue, out decimal actualDecimal))
                return false;

            if (!TryParseDecimal(expectedValue, out decimal expectedDecimal))
                return false;

            return comparer(actualDecimal, expectedDecimal);
        }

        private static bool TryParseBool(string value, out bool result)
        {
            value = Safe(value).Trim();

            if (string.Equals(value, "1", StringComparison.OrdinalIgnoreCase))
            {
                result = true;
                return true;
            }

            if (string.Equals(value, "0", StringComparison.OrdinalIgnoreCase))
            {
                result = false;
                return true;
            }

            return bool.TryParse(value, out result);
        }

        private static bool TryParseDecimal(string value, out decimal result)
        {
            return decimal.TryParse(
                Safe(value).Trim(),
                NumberStyles.Number,
                CultureInfo.InvariantCulture,
                out result);
        }

        private static string Safe(string value) => value ?? string.Empty;
    }

    // =========================================================
    // Engine
    // =========================================================

    internal static class CrossRuleValidationEngine
    {
        public static List<CrossValidationIssue> Validate(
            ModelInfo modelInfo,
            IEnumerable<string> ruleTexts,
            bool runParallel = true,
            int? maxParallelism = null)
        {
            if (modelInfo == null)
                throw new ArgumentNullException(nameof(modelInfo));

            if (ruleTexts == null)
                throw new ArgumentNullException(nameof(ruleTexts));

            var ruleList = ruleTexts
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .ToList();

            if (ruleList.Count == 0)
                return new List<CrossValidationIssue>();

            var root = ValidationNodeBuilder.Build(modelInfo);
            var index = new ValidationNodeIndex(root);

            var compiledRules = ruleList
                .Select(CrossRuleParser.Parse)
                .Select(CrossRuleCompiler.Compile)
                .ToList();

            return Validate(index, compiledRules, runParallel, maxParallelism);
        }

        public static List<CrossValidationIssue> Validate(
            ValidationNodeIndex index,
            IEnumerable<CompiledCrossRule> compiledRules,
            bool runParallel = true,
            int? maxParallelism = null)
        {
            if (index == null)
                throw new ArgumentNullException(nameof(index));

            if (compiledRules == null)
                throw new ArgumentNullException(nameof(compiledRules));

            var rules = compiledRules.ToList();
            if (rules.Count == 0)
                return new List<CrossValidationIssue>();

            int effectiveMaxParallelism = maxParallelism ?? Math.Max(1, Environment.ProcessorCount - 1);
            var issues = new ConcurrentBag<CrossValidationIssue>();

            if (!runParallel)
            {
                foreach (var rule in rules)
                    ExecuteRule(index, rule, issues);
            }
            else
            {
                var options = new ParallelOptions
                {
                    MaxDegreeOfParallelism = effectiveMaxParallelism
                };

                Parallel.ForEach(rules, options, rule =>
                {
                    ExecuteRule(index, rule, issues);
                });
            }

            return issues
                .OrderBy(x => x.RuleText, StringComparer.OrdinalIgnoreCase)
                .ThenBy(x => x.TriggerObjectPath, StringComparer.OrdinalIgnoreCase)
                .ThenBy(x => x.CheckObjectPath, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private static void ExecuteRule(
            ValidationNodeIndex index,
            CompiledCrossRule rule,
            ConcurrentBag<CrossValidationIssue> issues)
        {
            if (!index.NodesByType.TryGetValue(rule.TriggerNodeType, out var triggerNodes))
                return;

            foreach (var triggerNode in triggerNodes)
            {
                if (!rule.TriggerPredicate(triggerNode))
                    continue;

                var targets = rule.TargetSelector(triggerNode);
                if (targets == null || targets.Count == 0)
                    continue;

                foreach (var target in targets)
                {
                    if (target == null)
                        continue;

                    if (!rule.CheckPredicate(target))
                    {
                        var parent = target.Parent;
                        var ownerEntity = string.Equals(target.NodeType, "Entity", StringComparison.OrdinalIgnoreCase)
                            ? target
                            : target.GetNearestAncestorByType("Entity");

                        issues.Add(new CrossValidationIssue
                        {
                            RuleText = rule.RuleText,

                            TriggerNodeType = triggerNode.NodeType,
                            TriggerObjectId = triggerNode.ObjectId,
                            TriggerObjectName = triggerNode.Name,
                            TriggerObjectPath = triggerNode.Path,

                            CheckNodeType = target.NodeType,
                            CheckObjectId = target.ObjectId,
                            CheckObjectName = target.Name,
                            CheckObjectPath = target.Path,

                            CheckParentNodeType = parent?.NodeType ?? string.Empty,
                            CheckParentObjectId = parent?.ObjectId ?? string.Empty,
                            CheckParentObjectName = parent?.Name ?? string.Empty,
                            CheckParentObjectPath = parent?.Path ?? string.Empty,

                            CheckOwnerEntityId = ownerEntity?.ObjectId ?? string.Empty,
                            CheckOwnerEntityName = ownerEntity?.Name ?? string.Empty,
                            CheckOwnerEntityPath = ownerEntity?.Path ?? string.Empty,

                            PropertyName = rule.FirstCheckPropertyName,
                            Operator = rule.FirstCheckOperator,
                            ExpectedValue = rule.FirstCheckExpectedValue,
                            ActualValue = target.GetValue(rule.FirstCheckPropertyName),

                            Message = "Cross kural saglanmadi."
                        });
                    }
                }
            }
        }
    }
}