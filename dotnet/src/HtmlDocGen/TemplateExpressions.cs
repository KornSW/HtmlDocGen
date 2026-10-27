using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Text;

namespace HtmlDocGen {

  // v 2.0.0

  /// <summary>
  /// Kinds of tokens produced by the expression lexer.
  /// </summary>
  internal enum ExpressionTokenKind {
    End,
    Identifier,
    String,
    Number,
    Dot,
    Comma,
    Colon,
    OpenParenthesis,
    CloseParenthesis,
    OpenBracket,
    CloseBracket,
    Operator
  }

  /// <summary>
  /// A single expression token with its absolute offset in the template text.
  /// </summary>
  internal sealed class ExpressionToken {

    private readonly ExpressionTokenKind _Kind;
    private readonly string _Text;
    private readonly object _Value;
    private readonly int _Offset;

    /// <summary>
    /// Creates a token.
    /// </summary>
    public ExpressionToken(ExpressionTokenKind kind, string text, object value, int offset) {
      _Kind = kind;
      _Text = text;
      _Value = value;
      _Offset = offset;
    }

    /// <summary>
    /// The token kind.
    /// </summary>
    public ExpressionTokenKind Kind {
      get {
        return _Kind;
      }
    }

    /// <summary>
    /// The token text as written (for strings: the unescaped value).
    /// </summary>
    public string Text {
      get {
        return _Text;
      }
    }

    /// <summary>
    /// The literal value for string and number tokens.
    /// </summary>
    public object Value {
      get {
        return _Value;
      }
    }

    /// <summary>
    /// The absolute offset of the token in the template text.
    /// </summary>
    public int Offset {
      get {
        return _Offset;
      }
    }

    /// <summary>
    /// Returns true when this token is the specified operator.
    /// </summary>
    public bool IsOperator(string operatorText) {
      return _Kind == ExpressionTokenKind.Operator && _Text == operatorText;
    }

    /// <summary>
    /// Returns true when this token is the specified identifier or keyword.
    /// </summary>
    public bool IsIdentifier(string identifier) {
      return _Kind == ExpressionTokenKind.Identifier && _Text == identifier;
    }

  }

  /// <summary>
  /// Converts expression text into tokens. String literals use single or double quotes with backslash escapes.
  /// </summary>
  internal static class ExpressionLexer {

    /// <summary>
    /// Tokenizes a section of the template text.
    /// </summary>
    /// <param name="document">The template document (for diagnostics).</param>
    /// <param name="start">The absolute start offset of the expression.</param>
    /// <param name="end">The absolute end offset (exclusive).</param>
    public static ExpressionToken[] Tokenize(TemplateDocument document, int start, int end) {
      string text = document.Text;
      List<ExpressionToken> tokens = new List<ExpressionToken>();
      int position = start;
      while (position < end) {
        char current = text[position];
        if (char.IsWhiteSpace(current)) {
          position++;
        } else if (char.IsLetter(current) || current == '_') {
          int tokenStart = position;
          while (position < end && (char.IsLetterOrDigit(text[position]) || text[position] == '_')) {
            position++;
          }
          string identifier = text.Substring(tokenStart, position - tokenStart);
          tokens.Add(new ExpressionToken(ExpressionTokenKind.Identifier, identifier, null, tokenStart));
        } else if (char.IsDigit(current)) {
          int tokenStart = position;
          bool hasDecimalSeparator = false;
          while (position < end && (char.IsDigit(text[position]) || (text[position] == '.' && !hasDecimalSeparator && position + 1 < end && char.IsDigit(text[position + 1])))) {
            if (text[position] == '.') {
              hasDecimalSeparator = true;
            }
            position++;
          }
          string numberText = text.Substring(tokenStart, position - tokenStart);
          tokens.Add(new ExpressionToken(ExpressionTokenKind.Number, numberText, ExpressionLexer.ParseNumber(document, numberText, hasDecimalSeparator, tokenStart), tokenStart));
        } else if (current == '"' || current == '\'') {
          int tokenStart = position;
          StringBuilder value = new StringBuilder();
          position++;
          bool closed = false;
          while (position < end) {
            char character = text[position];
            if (character == '\\' && position + 1 < end) {
              char escaped = text[position + 1];
              if (escaped == 'n') {
                value.Append('\n');
              } else if (escaped == 't') {
                value.Append('\t');
              } else if (escaped == 'r') {
                value.Append('\r');
              } else {
                value.Append(escaped);
              }
              position += 2;
            } else if (character == current) {
              position++;
              closed = true;
              break;
            } else {
              value.Append(character);
              position++;
            }
          }
          if (!closed) {
            throw document.CreateError(tokenStart, "Unterminated string literal.", null);
          }
          tokens.Add(new ExpressionToken(ExpressionTokenKind.String, value.ToString(), value.ToString(), tokenStart));
        } else {
          ExpressionTokenKind kind = ExpressionTokenKind.Operator;
          string symbol = null;
          string twoCharacters = string.Empty;
          if (position + 1 < end) {
            twoCharacters = text.Substring(position, 2);
          }
          if (twoCharacters == "==" || twoCharacters == "!=" || twoCharacters == "<=" || twoCharacters == ">=" || twoCharacters == "&&" || twoCharacters == "||") {
            symbol = twoCharacters;
          } else if (current == '<' || current == '>' || current == '!' || current == '-') {
            symbol = current.ToString();
          } else if (current == '.') {
            kind = ExpressionTokenKind.Dot;
            symbol = ".";
          } else if (current == ',') {
            kind = ExpressionTokenKind.Comma;
            symbol = ",";
          } else if (current == ':') {
            kind = ExpressionTokenKind.Colon;
            symbol = ":";
          } else if (current == '(') {
            kind = ExpressionTokenKind.OpenParenthesis;
            symbol = "(";
          } else if (current == ')') {
            kind = ExpressionTokenKind.CloseParenthesis;
            symbol = ")";
          } else if (current == '[') {
            kind = ExpressionTokenKind.OpenBracket;
            symbol = "[";
          } else if (current == ']') {
            kind = ExpressionTokenKind.CloseBracket;
            symbol = "]";
          } else {
            throw document.CreateError(position, "Unexpected character '" + current + "' in expression.", null);
          }
          tokens.Add(new ExpressionToken(kind, symbol, null, position));
          position += symbol.Length;
        }
      }
      tokens.Add(new ExpressionToken(ExpressionTokenKind.End, string.Empty, null, end));
      return tokens.ToArray();
    }

    /// <summary>
    /// Parses a numeric literal (invariant culture): Int32, Int64 or Decimal.
    /// </summary>
    private static object ParseNumber(TemplateDocument document, string numberText, bool hasDecimalSeparator, int offset) {
      if (hasDecimalSeparator) {
        decimal decimalValue;
        if (decimal.TryParse(numberText, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out decimalValue)) {
          return decimalValue;
        }
      } else {
        int intValue;
        if (int.TryParse(numberText, NumberStyles.None, CultureInfo.InvariantCulture, out intValue)) {
          return intValue;
        }
        long longValue;
        if (long.TryParse(numberText, NumberStyles.None, CultureInfo.InvariantCulture, out longValue)) {
          return longValue;
        }
      }
      throw document.CreateError(offset, "Invalid numeric literal '" + numberText + "'.", null);
    }

  }

  /// <summary>
  /// Recursive descent parser for template expressions.
  /// Grammar (lowest to highest precedence): or (|| / or), and (&amp;&amp; / and), comparison (== != &lt; &lt;= &gt; &gt;=),
  /// unary (! / not / -), postfix (.Member, .Method(args), [index]), primary (literal, identifier, parentheses).
  /// </summary>
  internal sealed class ExpressionParser {

    private readonly TemplateDocument _Document;
    private readonly ExpressionToken[] _Tokens;
    private int _Index;

    /// <summary>
    /// Creates a parser for a section of the template text.
    /// </summary>
    private ExpressionParser(TemplateDocument document, int start, int end) {
      _Document = document;
      _Tokens = ExpressionLexer.Tokenize(document, start, end);
      _Index = 0;
    }

    /// <summary>
    /// Parses a complete expression.
    /// </summary>
    public static ExpressionNode ParseExpression(TemplateDocument document, int start, int end) {
      ExpressionParser parser = new ExpressionParser(document, start, end);
      if (parser.Current.Kind == ExpressionTokenKind.End) {
        throw document.CreateError(start, "Empty expression.", null);
      }
      ExpressionNode expression = parser.ParseOr();
      parser.Expect(ExpressionTokenKind.End, "end of expression");
      return expression;
    }

    /// <summary>
    /// Parses an interpolation: an expression optionally followed by ':' and a .NET format string.
    /// </summary>
    public static ExpressionNode ParseInterpolation(TemplateDocument document, int start, int end, out string format) {
      ExpressionParser parser = new ExpressionParser(document, start, end);
      if (parser.Current.Kind == ExpressionTokenKind.End) {
        throw document.CreateError(start, "Empty expression.", null);
      }
      ExpressionNode expression = parser.ParseOr();
      format = null;
      if (parser.Current.Kind == ExpressionTokenKind.Colon) {
        int formatStart = parser.Current.Offset + 1;
        format = document.Text.Substring(formatStart, end - formatStart).Trim();
        if (format.Length == 0) {
          throw document.CreateError(parser.Current.Offset, "Empty format string after ':'.", null);
        }
        return expression;
      }
      parser.Expect(ExpressionTokenKind.End, "end of expression");
      return expression;
    }

    /// <summary>
    /// Parses an iteration declaration 'item in expression' or 'item, index in expression'.
    /// </summary>
    public static ExpressionNode ParseIteration(TemplateDocument document, int start, int end, out string itemName, out string indexName) {
      ExpressionParser parser = new ExpressionParser(document, start, end);
      itemName = parser.ExpectLocalName();
      indexName = null;
      if (parser.Current.Kind == ExpressionTokenKind.Comma) {
        parser.Advance();
        indexName = parser.ExpectLocalName();
      }
      if (!parser.Current.IsIdentifier("in")) {
        throw document.CreateError(parser.Current.Offset, "Expected 'in' in iteration declaration ('item in expression').", null);
      }
      parser.Advance();
      ExpressionNode expression = parser.ParseOr();
      parser.Expect(ExpressionTokenKind.End, "end of expression");
      return expression;
    }

    /// <summary>
    /// The current token.
    /// </summary>
    private ExpressionToken Current {
      get {
        return _Tokens[_Index];
      }
    }

    /// <summary>
    /// Moves to the next token and returns the previous one.
    /// </summary>
    private ExpressionToken Advance() {
      ExpressionToken token = _Tokens[_Index];
      if (_Index < _Tokens.Length - 1) {
        _Index++;
      }
      return token;
    }

    /// <summary>
    /// Consumes a token of the expected kind or throws a diagnostic error.
    /// </summary>
    private ExpressionToken Expect(ExpressionTokenKind kind, string description) {
      if (this.Current.Kind != kind) {
        string found = this.Current.Text;
        if (this.Current.Kind == ExpressionTokenKind.End) {
          found = "end of expression";
        }
        throw _Document.CreateError(this.Current.Offset, "Expected " + description + " but found '" + found + "'.", null);
      }
      return this.Advance();
    }

    /// <summary>
    /// Consumes an identifier usable as local variable name.
    /// </summary>
    private string ExpectLocalName() {
      ExpressionToken token = this.Expect(ExpressionTokenKind.Identifier, "a variable name");
      if (token.Text.StartsWith("_", StringComparison.Ordinal) || ExpressionParser.IsKeyword(token.Text)) {
        throw _Document.CreateError(token.Offset, "'" + token.Text + "' cannot be used as a local variable name.", null);
      }
      return token.Text;
    }

    /// <summary>
    /// Returns true for reserved keywords.
    /// </summary>
    private static bool IsKeyword(string identifier) {
      return identifier == "true" || identifier == "false" || identifier == "null" || identifier == "and" || identifier == "or" || identifier == "not" || identifier == "in" || identifier == "raw";
    }

    /// <summary>
    /// or := and (('||' | 'or') and)*
    /// </summary>
    private ExpressionNode ParseOr() {
      ExpressionNode left = this.ParseAnd();
      while (this.Current.IsOperator("||") || this.Current.IsIdentifier("or")) {
        ExpressionToken token = this.Advance();
        ExpressionNode right = this.ParseAnd();
        left = new LogicalNode(token.Offset, false, left, right);
      }
      return left;
    }

    /// <summary>
    /// and := comparison (('&amp;&amp;' | 'and') comparison)*
    /// </summary>
    private ExpressionNode ParseAnd() {
      ExpressionNode left = this.ParseComparison();
      while (this.Current.IsOperator("&&") || this.Current.IsIdentifier("and")) {
        ExpressionToken token = this.Advance();
        ExpressionNode right = this.ParseComparison();
        left = new LogicalNode(token.Offset, true, left, right);
      }
      return left;
    }

    /// <summary>
    /// comparison := unary (comparisonOperator unary)?
    /// </summary>
    private ExpressionNode ParseComparison() {
      ExpressionNode left = this.ParseUnary();
      ExpressionToken token = this.Current;
      if (token.Kind == ExpressionTokenKind.Operator && (token.Text == "==" || token.Text == "!=" || token.Text == "<" || token.Text == "<=" || token.Text == ">" || token.Text == ">=")) {
        this.Advance();
        ExpressionNode right = this.ParseUnary();
        return new ComparisonNode(token.Offset, token.Text, left, right);
      }
      return left;
    }

    /// <summary>
    /// unary := ('!' | 'not') unary | '-' number | postfix
    /// </summary>
    private ExpressionNode ParseUnary() {
      if (this.Current.IsOperator("!") || this.Current.IsIdentifier("not")) {
        ExpressionToken token = this.Advance();
        return new NotNode(token.Offset, this.ParseUnary());
      }
      if (this.Current.IsOperator("-")) {
        ExpressionToken minus = this.Advance();
        ExpressionToken number = this.Expect(ExpressionTokenKind.Number, "a number after '-'");
        return new LiteralNode(minus.Offset, ExpressionParser.Negate(number.Value));
      }
      return this.ParsePostfix();
    }

    /// <summary>
    /// Negates a numeric literal value.
    /// </summary>
    private static object Negate(object value) {
      if (value is int) {
        return -(int)value;
      }
      if (value is long) {
        return -(long)value;
      }
      return -(decimal)value;
    }

    /// <summary>
    /// postfix := primary ('.' identifier ('(' arguments ')')? | '[' expression ']')*
    /// </summary>
    private ExpressionNode ParsePostfix() {
      ExpressionNode target = this.ParsePrimary();
      while (true) {
        if (this.Current.Kind == ExpressionTokenKind.Dot) {
          this.Advance();
          ExpressionToken name = this.Expect(ExpressionTokenKind.Identifier, "a member name after '.'");
          if (this.Current.Kind == ExpressionTokenKind.OpenParenthesis) {
            this.Advance();
            ExpressionNode[] arguments = this.ParseArguments();
            target = new MethodCallNode(name.Offset, target, name.Text, arguments);
          } else {
            target = new MemberNode(name.Offset, target, name.Text);
          }
        } else if (this.Current.Kind == ExpressionTokenKind.OpenBracket) {
          ExpressionToken bracket = this.Advance();
          ExpressionNode index = this.ParseOr();
          this.Expect(ExpressionTokenKind.CloseBracket, "']'");
          target = new IndexNode(bracket.Offset, target, index);
        } else {
          return target;
        }
      }
    }

    /// <summary>
    /// Parses a comma separated argument list after '(' including the closing ')'.
    /// </summary>
    private ExpressionNode[] ParseArguments() {
      List<ExpressionNode> arguments = new List<ExpressionNode>();
      if (this.Current.Kind == ExpressionTokenKind.CloseParenthesis) {
        this.Advance();
        return arguments.ToArray();
      }
      while (true) {
        arguments.Add(this.ParseOr());
        if (this.Current.Kind == ExpressionTokenKind.Comma) {
          this.Advance();
        } else {
          this.Expect(ExpressionTokenKind.CloseParenthesis, "',' or ')'");
          return arguments.ToArray();
        }
      }
    }

    /// <summary>
    /// primary := string | number | true | false | null | identifier | '(' expression ')'
    /// </summary>
    private ExpressionNode ParsePrimary() {
      ExpressionToken token = this.Current;
      if (token.Kind == ExpressionTokenKind.String || token.Kind == ExpressionTokenKind.Number) {
        this.Advance();
        return new LiteralNode(token.Offset, token.Value);
      }
      if (token.Kind == ExpressionTokenKind.OpenParenthesis) {
        this.Advance();
        ExpressionNode inner = this.ParseOr();
        this.Expect(ExpressionTokenKind.CloseParenthesis, "')'");
        return inner;
      }
      if (token.Kind == ExpressionTokenKind.Identifier) {
        this.Advance();
        if (token.Text == "true") {
          return new LiteralNode(token.Offset, true);
        }
        if (token.Text == "false") {
          return new LiteralNode(token.Offset, false);
        }
        if (token.Text == "null") {
          return new LiteralNode(token.Offset, null);
        }
        if (ExpressionParser.IsKeyword(token.Text)) {
          throw _Document.CreateError(token.Offset, "Unexpected keyword '" + token.Text + "'.", null);
        }
        if (this.Current.Kind == ExpressionTokenKind.OpenParenthesis) {
          throw _Document.CreateError(token.Offset, "Free functions are not supported; call methods on a data source, for example '_Source." + token.Text + "()'.", null);
        }
        return new VariableNode(token.Offset, token.Text);
      }
      string found = token.Text;
      if (token.Kind == ExpressionTokenKind.End) {
        found = "end of expression";
      }
      throw _Document.CreateError(token.Offset, "Expected a value but found '" + found + "'.", null);
    }

  }

  /// <summary>
  /// Runtime state for evaluating expressions.
  /// </summary>
  internal sealed class EvaluationContext {

    private readonly TemplateRuntime _Runtime;
    private readonly TemplateDocument _Document;
    private readonly RenderScope _Scope;
    private readonly CultureInfo _Culture;

    /// <summary>
    /// Creates an evaluation context.
    /// </summary>
    public EvaluationContext(TemplateRuntime runtime, TemplateDocument document, RenderScope scope, CultureInfo culture) {
      _Runtime = runtime;
      _Document = document;
      _Scope = scope;
      _Culture = culture;
    }

    /// <summary>
    /// The engine-wide runtime configuration and caches.
    /// </summary>
    public TemplateRuntime Runtime {
      get {
        return _Runtime;
      }
    }

    /// <summary>
    /// The document containing the evaluated expression.
    /// </summary>
    public TemplateDocument Document {
      get {
        return _Document;
      }
    }

    /// <summary>
    /// The current variable scope.
    /// </summary>
    public RenderScope Scope {
      get {
        return _Scope;
      }
    }

    /// <summary>
    /// The culture used for formatting.
    /// </summary>
    public CultureInfo Culture {
      get {
        return _Culture;
      }
    }

    /// <summary>
    /// Creates a context for the same document with another scope.
    /// </summary>
    public EvaluationContext WithScope(RenderScope scope) {
      return new EvaluationContext(_Runtime, _Document, scope, _Culture);
    }

    /// <summary>
    /// Creates a context for another document and scope.
    /// </summary>
    public EvaluationContext WithDocument(TemplateDocument document, RenderScope scope) {
      return new EvaluationContext(_Runtime, document, scope, _Culture);
    }

  }

  /// <summary>
  /// Base class of all parsed expression nodes.
  /// </summary>
  internal abstract class ExpressionNode {

    private readonly int _Offset;

    /// <summary>
    /// Creates a node located at the specified absolute offset.
    /// </summary>
    protected ExpressionNode(int offset) {
      _Offset = offset;
    }

    /// <summary>
    /// The absolute offset of the node in the template text.
    /// </summary>
    public int Offset {
      get {
        return _Offset;
      }
    }

    /// <summary>
    /// Evaluates the node.
    /// </summary>
    public abstract object Evaluate(EvaluationContext context);

  }

  /// <summary>
  /// A literal value.
  /// </summary>
  internal sealed class LiteralNode : ExpressionNode {

    private readonly object _Value;

    /// <summary>
    /// Creates a literal node.
    /// </summary>
    public LiteralNode(int offset, object value) : base(offset) {
      _Value = value;
    }

    /// <summary>
    /// Returns the literal value.
    /// </summary>
    public override object Evaluate(EvaluationContext context) {
      return _Value;
    }

  }

  /// <summary>
  /// A root identifier: an underscore-prefixed data source or a local variable.
  /// </summary>
  internal sealed class VariableNode : ExpressionNode {

    private readonly string _Name;

    /// <summary>
    /// Creates a variable node.
    /// </summary>
    public VariableNode(int offset, string name) : base(offset) {
      _Name = name;
    }

    /// <summary>
    /// Resolves the variable or throws a diagnostic error for unknown names.
    /// </summary>
    public override object Evaluate(EvaluationContext context) {
      object value;
      if (context.Scope.TryResolve(_Name, out value)) {
        return value;
      }
      if (_Name.StartsWith("_", StringComparison.Ordinal)) {
        throw context.Document.CreateError(this.Offset, "Unknown data source '" + _Name + "'. Register it with TemplateContext.AddSource.", null);
      }
      throw context.Document.CreateError(this.Offset, "Unknown variable '" + _Name + "'. Locals are not inherited by includes; pass them as include parameters.", null);
    }

  }

  /// <summary>
  /// Property access (or dictionary key lookup) on a target value. Null targets yield null.
  /// </summary>
  internal sealed class MemberNode : ExpressionNode {

    private readonly ExpressionNode _Target;
    private readonly string _Name;

    /// <summary>
    /// Creates a member node.
    /// </summary>
    public MemberNode(int offset, ExpressionNode target, string name) : base(offset) {
      _Target = target;
      _Name = name;
    }

    /// <summary>
    /// Reads the property value.
    /// </summary>
    public override object Evaluate(EvaluationContext context) {
      object target = _Target.Evaluate(context);
      if (target == null) {
        return null;
      }
      PropertyInfo property = context.Runtime.Members.GetProperty(target.GetType(), _Name);
      if (property != null) {
        if (!context.Runtime.Policy.IsPropertyAccessible(property)) {
          throw context.Document.CreateError(this.Offset, "Property '" + _Name + "' of type '" + target.GetType().FullName + "' is not accessible by the member access policy.", null);
        }
        try {
          return property.GetValue(target, null);
        } catch (TargetInvocationException ex) {
          throw context.Document.CreateError(this.Offset, "Property '" + _Name + "' threw an exception: " + ex.InnerException.Message, ex.InnerException);
        }
      }
      IDictionary dictionary = target as IDictionary;
      if (dictionary != null) {
        if (dictionary.Contains(_Name)) {
          return dictionary[_Name];
        }
        return null;
      }
      throw context.Document.CreateError(this.Offset, "Type '" + target.GetType().FullName + "' has no public property '" + _Name + "'.", null);
    }

  }

  /// <summary>
  /// Public instance method invocation with deterministic overload resolution. Null targets yield null.
  /// </summary>
  internal sealed class MethodCallNode : ExpressionNode {

    private readonly ExpressionNode _Target;
    private readonly string _Name;
    private readonly ExpressionNode[] _Arguments;

    /// <summary>
    /// Creates a method call node.
    /// </summary>
    public MethodCallNode(int offset, ExpressionNode target, string name, ExpressionNode[] arguments) : base(offset) {
      _Target = target;
      _Name = name;
      _Arguments = arguments;
    }

    /// <summary>
    /// Invokes the best matching overload.
    /// </summary>
    public override object Evaluate(EvaluationContext context) {
      object target = _Target.Evaluate(context);
      if (target == null) {
        return null;
      }
      object[] argumentValues = new object[_Arguments.Length];
      for (int index = 0; index < _Arguments.Length; index++) {
        argumentValues[index] = _Arguments[index].Evaluate(context);
      }
      MethodInfo[] candidates = context.Runtime.Members.GetMethods(target.GetType(), _Name);
      if (candidates.Length == 0) {
        throw context.Document.CreateError(this.Offset, "Type '" + target.GetType().FullName + "' has no public instance method '" + _Name + "'.", null);
      }
      List<MethodInfo> accessible = new List<MethodInfo>();
      foreach (MethodInfo candidate in candidates) {
        if (context.Runtime.Policy.IsMethodAccessible(candidate)) {
          accessible.Add(candidate);
        }
      }
      if (accessible.Count == 0) {
        throw context.Document.CreateError(this.Offset, "Method '" + _Name + "' of type '" + target.GetType().FullName + "' is not accessible by the member access policy.", null);
      }
      string resolutionError;
      object[] convertedArguments;
      MethodInfo method = OverloadResolver.Resolve(accessible.ToArray(), argumentValues, out convertedArguments, out resolutionError);
      if (method == null) {
        throw context.Document.CreateError(this.Offset, "Cannot call '" + _Name + "' on type '" + target.GetType().FullName + "': " + resolutionError, null);
      }
      try {
        return method.Invoke(target, convertedArguments);
      } catch (TargetInvocationException ex) {
        throw context.Document.CreateError(this.Offset, "Method '" + _Name + "' threw an exception: " + ex.InnerException.Message, ex.InnerException);
      }
    }

  }

  /// <summary>
  /// Index access on arrays, lists, dictionaries and public indexers. Null targets yield null.
  /// </summary>
  internal sealed class IndexNode : ExpressionNode {

    private readonly ExpressionNode _Target;
    private readonly ExpressionNode _Index;

    /// <summary>
    /// Creates an index node.
    /// </summary>
    public IndexNode(int offset, ExpressionNode target, ExpressionNode index) : base(offset) {
      _Target = target;
      _Index = index;
    }

    /// <summary>
    /// Reads the indexed value.
    /// </summary>
    public override object Evaluate(EvaluationContext context) {
      object target = _Target.Evaluate(context);
      if (target == null) {
        return null;
      }
      object index = _Index.Evaluate(context);
      Array array = target as Array;
      if (array != null && array.Rank == 1 && ValueOperations.IsNumeric(index)) {
        int position = Convert.ToInt32(index, CultureInfo.InvariantCulture);
        if (position < 0 || position >= array.Length) {
          throw context.Document.CreateError(this.Offset, "Index " + position + " is out of range (length " + array.Length + ").", null);
        }
        return array.GetValue(position);
      }
      IList list = target as IList;
      if (list != null && ValueOperations.IsNumeric(index)) {
        int position = Convert.ToInt32(index, CultureInfo.InvariantCulture);
        if (position < 0 || position >= list.Count) {
          throw context.Document.CreateError(this.Offset, "Index " + position + " is out of range (count " + list.Count + ").", null);
        }
        return list[position];
      }
      IDictionary dictionary = target as IDictionary;
      if (dictionary != null && index != null) {
        if (dictionary.Contains(index)) {
          return dictionary[index];
        }
        return null;
      }
      PropertyInfo[] indexers = context.Runtime.Members.GetIndexers(target.GetType());
      List<MethodInfo> getters = new List<MethodInfo>();
      foreach (PropertyInfo indexer in indexers) {
        if (context.Runtime.Policy.IsPropertyAccessible(indexer)) {
          getters.Add(indexer.GetGetMethod());
        }
      }
      if (getters.Count == 0) {
        throw context.Document.CreateError(this.Offset, "Type '" + target.GetType().FullName + "' has no accessible indexer.", null);
      }
      string resolutionError;
      object[] convertedArguments;
      MethodInfo getter = OverloadResolver.Resolve(getters.ToArray(), new object[] { index }, out convertedArguments, out resolutionError);
      if (getter == null) {
        throw context.Document.CreateError(this.Offset, "Cannot index type '" + target.GetType().FullName + "': " + resolutionError, null);
      }
      try {
        return getter.Invoke(target, convertedArguments);
      } catch (TargetInvocationException ex) {
        throw context.Document.CreateError(this.Offset, "Indexer threw an exception: " + ex.InnerException.Message, ex.InnerException);
      }
    }

  }

  /// <summary>
  /// Logical negation using template truthiness.
  /// </summary>
  internal sealed class NotNode : ExpressionNode {

    private readonly ExpressionNode _Operand;

    /// <summary>
    /// Creates a negation node.
    /// </summary>
    public NotNode(int offset, ExpressionNode operand) : base(offset) {
      _Operand = operand;
    }

    /// <summary>
    /// Returns the negated truth value.
    /// </summary>
    public override object Evaluate(EvaluationContext context) {
      return !context.Runtime.IsTrue(_Operand.Evaluate(context));
    }

  }

  /// <summary>
  /// Short-circuit 'and' / 'or' returning Boolean results.
  /// </summary>
  internal sealed class LogicalNode : ExpressionNode {

    private readonly bool _IsAnd;
    private readonly ExpressionNode _Left;
    private readonly ExpressionNode _Right;

    /// <summary>
    /// Creates a logical node.
    /// </summary>
    public LogicalNode(int offset, bool isAnd, ExpressionNode left, ExpressionNode right) : base(offset) {
      _IsAnd = isAnd;
      _Left = left;
      _Right = right;
    }

    /// <summary>
    /// Evaluates with short-circuit semantics.
    /// </summary>
    public override object Evaluate(EvaluationContext context) {
      bool left = context.Runtime.IsTrue(_Left.Evaluate(context));
      if (_IsAnd) {
        if (!left) {
          return false;
        }
        return context.Runtime.IsTrue(_Right.Evaluate(context));
      }
      if (left) {
        return true;
      }
      return context.Runtime.IsTrue(_Right.Evaluate(context));
    }

  }

  /// <summary>
  /// Equality and ordering comparisons.
  /// </summary>
  internal sealed class ComparisonNode : ExpressionNode {

    private readonly string _Operator;
    private readonly ExpressionNode _Left;
    private readonly ExpressionNode _Right;

    /// <summary>
    /// Creates a comparison node.
    /// </summary>
    public ComparisonNode(int offset, string comparisonOperator, ExpressionNode left, ExpressionNode right) : base(offset) {
      _Operator = comparisonOperator;
      _Left = left;
      _Right = right;
    }

    /// <summary>
    /// Evaluates the comparison.
    /// </summary>
    public override object Evaluate(EvaluationContext context) {
      object left = _Left.Evaluate(context);
      object right = _Right.Evaluate(context);
      if (_Operator == "==") {
        return ValueOperations.AreEqual(left, right);
      }
      if (_Operator == "!=") {
        return !ValueOperations.AreEqual(left, right);
      }
      int comparison;
      string error;
      if (!ValueOperations.TryCompare(left, right, out comparison, out error)) {
        throw context.Document.CreateError(this.Offset, error, null);
      }
      if (_Operator == "<") {
        return comparison < 0;
      }
      if (_Operator == "<=") {
        return comparison <= 0;
      }
      if (_Operator == ">") {
        return comparison > 0;
      }
      return comparison >= 0;
    }

  }

  /// <summary>
  /// Value semantics shared by expressions: numeric detection, equality and ordering.
  /// </summary>
  internal static class ValueOperations {

    /// <summary>
    /// Returns true for primitive numeric values and decimals.
    /// </summary>
    public static bool IsNumeric(object value) {
      if (value == null) {
        return false;
      }
      return ValueOperations.IsNumericType(value.GetType());
    }

    /// <summary>
    /// Returns true for primitive numeric types and decimal.
    /// </summary>
    public static bool IsNumericType(Type type) {
      return type == typeof(int) || type == typeof(long) || type == typeof(decimal) || type == typeof(double) || type == typeof(float)
        || type == typeof(short) || type == typeof(byte) || type == typeof(uint) || type == typeof(ulong) || type == typeof(ushort) || type == typeof(sbyte);
    }

    /// <summary>
    /// Compares two values for equality: numbers by value, enums against their names, otherwise Object.Equals.
    /// </summary>
    public static bool AreEqual(object left, object right) {
      if (left == null || right == null) {
        return left == null && right == null;
      }
      if (ValueOperations.IsNumeric(left) && ValueOperations.IsNumeric(right)) {
        int comparison;
        string error;
        ValueOperations.TryCompare(left, right, out comparison, out error);
        return comparison == 0;
      }
      if (left is Enum && right is string) {
        return string.Equals(left.ToString(), (string)right, StringComparison.Ordinal);
      }
      if (right is Enum && left is string) {
        return string.Equals(right.ToString(), (string)left, StringComparison.Ordinal);
      }
      return left.Equals(right);
    }

    /// <summary>
    /// Orders two values: numbers by value, strings ordinal, otherwise via IComparable of equal types.
    /// </summary>
    public static bool TryCompare(object left, object right, out int comparison, out string error) {
      error = null;
      comparison = 0;
      if (left == null || right == null) {
        error = "Cannot order null values.";
        return false;
      }
      if (ValueOperations.IsNumeric(left) && ValueOperations.IsNumeric(right)) {
        if (left is double || left is float || right is double || right is float) {
          comparison = Convert.ToDouble(left, CultureInfo.InvariantCulture).CompareTo(Convert.ToDouble(right, CultureInfo.InvariantCulture));
        } else {
          comparison = Convert.ToDecimal(left, CultureInfo.InvariantCulture).CompareTo(Convert.ToDecimal(right, CultureInfo.InvariantCulture));
        }
        return true;
      }
      if (left is string && right is string) {
        comparison = string.CompareOrdinal((string)left, (string)right);
        return true;
      }
      IComparable comparable = left as IComparable;
      if (comparable != null && left.GetType() == right.GetType()) {
        comparison = comparable.CompareTo(right);
        return true;
      }
      error = "Cannot order values of type '" + left.GetType().FullName + "' and '" + right.GetType().FullName + "'.";
      return false;
    }

  }

  /// <summary>
  /// Cached lookup of public instance members per runtime type.
  /// </summary>
  internal sealed class MemberCache {

    private readonly ConcurrentDictionary<Type, TypeMembers> _Types = new ConcurrentDictionary<Type, TypeMembers>();

    /// <summary>
    /// Returns the public instance property with the specified name, or null.
    /// </summary>
    public PropertyInfo GetProperty(Type type, string name) {
      PropertyInfo property;
      if (this.GetMembers(type).Properties.TryGetValue(name, out property)) {
        return property;
      }
      return null;
    }

    /// <summary>
    /// Returns all public instance methods with the specified name.
    /// </summary>
    public MethodInfo[] GetMethods(Type type, string name) {
      MethodInfo[] methods;
      if (this.GetMembers(type).Methods.TryGetValue(name, out methods)) {
        return methods;
      }
      return new MethodInfo[0];
    }

    /// <summary>
    /// Returns all public instance indexers.
    /// </summary>
    public PropertyInfo[] GetIndexers(Type type) {
      return this.GetMembers(type).Indexers;
    }

    /// <summary>
    /// Returns (and caches) the member table of a type.
    /// </summary>
    private TypeMembers GetMembers(Type type) {
      TypeMembers members;
      if (_Types.TryGetValue(type, out members)) {
        return members;
      }
      members = new TypeMembers(type);
      return _Types.GetOrAdd(type, members);
    }

  }

  /// <summary>
  /// The public instance members of one type, prepared for lookup.
  /// </summary>
  internal sealed class TypeMembers {

    private readonly Dictionary<string, PropertyInfo> _Properties = new Dictionary<string, PropertyInfo>(StringComparer.Ordinal);
    private readonly Dictionary<string, MethodInfo[]> _Methods = new Dictionary<string, MethodInfo[]>(StringComparer.Ordinal);
    private readonly PropertyInfo[] _Indexers;

    /// <summary>
    /// Collects the readable public instance properties, indexers and non-generic methods of a type.
    /// Hidden members ('new') resolve to the most derived declaration.
    /// </summary>
    public TypeMembers(Type type) {
      List<PropertyInfo> indexers = new List<PropertyInfo>();
      foreach (PropertyInfo property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance)) {
        MethodInfo getter = property.GetGetMethod();
        if (getter == null) {
          continue;
        }
        if (property.GetIndexParameters().Length > 0) {
          indexers.Add(property);
          continue;
        }
        PropertyInfo existing;
        if (_Properties.TryGetValue(property.Name, out existing)) {
          if (property.DeclaringType.IsSubclassOf(existing.DeclaringType)) {
            _Properties[property.Name] = property;
          }
        } else {
          _Properties.Add(property.Name, property);
        }
      }
      _Indexers = indexers.ToArray();
      Dictionary<string, List<MethodInfo>> methods = new Dictionary<string, List<MethodInfo>>(StringComparer.Ordinal);
      foreach (MethodInfo method in type.GetMethods(BindingFlags.Public | BindingFlags.Instance)) {
        if (method.IsSpecialName || method.ContainsGenericParameters) {
          continue;
        }
        List<MethodInfo> overloads;
        if (!methods.TryGetValue(method.Name, out overloads)) {
          overloads = new List<MethodInfo>();
          methods.Add(method.Name, overloads);
        }
        overloads.Add(method);
      }
      foreach (KeyValuePair<string, List<MethodInfo>> entry in methods) {
        _Methods.Add(entry.Key, entry.Value.ToArray());
      }
    }

    /// <summary>
    /// Readable non-indexed properties by name.
    /// </summary>
    public Dictionary<string, PropertyInfo> Properties {
      get {
        return _Properties;
      }
    }

    /// <summary>
    /// Method overloads by name.
    /// </summary>
    public Dictionary<string, MethodInfo[]> Methods {
      get {
        return _Methods;
      }
    }

    /// <summary>
    /// Readable indexers.
    /// </summary>
    public PropertyInfo[] Indexers {
      get {
        return _Indexers;
      }
    }

  }

  /// <summary>
  /// Deterministic overload resolution: each argument gets a conversion cost
  /// (0 exact, 1 assignable, 2 object parameter, 3 numeric conversion, 4 string to enum);
  /// the overload with the lowest total cost wins, equal best costs are reported as ambiguous.
  /// Methods with ref/out parameters and params arrays are not supported.
  /// </summary>
  internal static class OverloadResolver {

    private const int _NotApplicable = -1;

    /// <summary>
    /// Selects the best overload and converts the arguments.
    /// </summary>
    /// <returns>The selected method, or null with an error description.</returns>
    public static MethodInfo Resolve(MethodInfo[] candidates, object[] arguments, out object[] convertedArguments, out string error) {
      MethodInfo best = null;
      object[] bestArguments = null;
      int bestCost = int.MaxValue;
      bool ambiguous = false;
      foreach (MethodInfo candidate in candidates) {
        object[] converted;
        int cost = OverloadResolver.GetCost(candidate, arguments, out converted);
        if (cost == _NotApplicable) {
          continue;
        }
        if (cost < bestCost) {
          best = candidate;
          bestArguments = converted;
          bestCost = cost;
          ambiguous = false;
        } else if (cost == bestCost) {
          ambiguous = true;
        }
      }
      if (best == null) {
        convertedArguments = null;
        error = "no overload accepts " + arguments.Length + " argument(s) of the given types.";
        return null;
      }
      if (ambiguous) {
        convertedArguments = null;
        error = "the call is ambiguous between multiple overloads.";
        return null;
      }
      convertedArguments = bestArguments;
      error = null;
      return best;
    }

    /// <summary>
    /// Calculates the conversion cost of a candidate, or -1 when it is not applicable.
    /// </summary>
    private static int GetCost(MethodInfo candidate, object[] arguments, out object[] converted) {
      ParameterInfo[] parameters = candidate.GetParameters();
      converted = null;
      if (arguments.Length > parameters.Length) {
        return _NotApplicable;
      }
      object[] values = new object[parameters.Length];
      int total = 0;
      for (int index = 0; index < parameters.Length; index++) {
        ParameterInfo parameter = parameters[index];
        if (parameter.ParameterType.IsByRef) {
          return _NotApplicable;
        }
        if (index >= arguments.Length) {
          if (!parameter.HasDefaultValue) {
            return _NotApplicable;
          }
          values[index] = parameter.DefaultValue;
          continue;
        }
        object value;
        int cost = OverloadResolver.TryConvert(arguments[index], parameter.ParameterType, out value);
        if (cost == _NotApplicable) {
          return _NotApplicable;
        }
        values[index] = value;
        total += cost;
      }
      converted = values;
      return total;
    }

    /// <summary>
    /// Converts a single argument and returns its cost, or -1 when no conversion exists.
    /// </summary>
    private static int TryConvert(object argument, Type parameterType, out object converted) {
      converted = null;
      Type underlyingType = Nullable.GetUnderlyingType(parameterType);
      if (argument == null) {
        if (!parameterType.IsValueType || underlyingType != null) {
          return 1;
        }
        return _NotApplicable;
      }
      if (underlyingType == null) {
        underlyingType = parameterType;
      }
      Type argumentType = argument.GetType();
      if (argumentType == parameterType || argumentType == underlyingType) {
        converted = argument;
        return 0;
      }
      if (parameterType == typeof(object)) {
        converted = argument;
        return 2;
      }
      if (parameterType.IsAssignableFrom(argumentType)) {
        converted = argument;
        return 1;
      }
      if (ValueOperations.IsNumeric(argument) && ValueOperations.IsNumericType(underlyingType)) {
        try {
          converted = Convert.ChangeType(argument, underlyingType, CultureInfo.InvariantCulture);
          return 3;
        } catch (OverflowException) {
          return _NotApplicable;
        }
      }
      if (underlyingType.IsEnum && argument is string) {
        object enumValue;
        if (Enum.TryParse(underlyingType, (string)argument, false, out enumValue)) {
          converted = enumValue;
          return 4;
        }
      }
      return _NotApplicable;
    }

  }

}
