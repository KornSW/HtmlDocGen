using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace HtmlDocGen {

  // v 2.0.0

  /// <summary>
  /// Raised for template syntax and evaluation errors. Carries the template identity and the
  /// 1-based line and column of the offending construct.
  /// </summary>
  public sealed class TemplateException : Exception {

    private readonly string _TemplateName;
    private readonly int _Line;
    private readonly int _Column;
    private readonly string _Reason;

    /// <summary>
    /// Creates a template exception.
    /// </summary>
    /// <param name="reason">The error description without location.</param>
    /// <param name="templateName">The name of the template containing the error.</param>
    /// <param name="line">The 1-based line number.</param>
    /// <param name="column">The 1-based column number.</param>
    /// <param name="innerException">An optional causing exception.</param>
    public TemplateException(string reason, string templateName, int line, int column, Exception innerException)
      : base(reason + " (template '" + templateName + "', line " + line + ", column " + column + ")", innerException) {
      _Reason = reason;
      _TemplateName = templateName;
      _Line = line;
      _Column = column;
    }

    /// <summary>
    /// The name of the template containing the error.
    /// </summary>
    public string TemplateName {
      get {
        return _TemplateName;
      }
    }

    /// <summary>
    /// The 1-based line number of the error.
    /// </summary>
    public int Line {
      get {
        return _Line;
      }
    }

    /// <summary>
    /// The 1-based column number of the error.
    /// </summary>
    public int Column {
      get {
        return _Column;
      }
    }

    /// <summary>
    /// The error description without location information.
    /// </summary>
    public string Reason {
      get {
        return _Reason;
      }
    }

  }

  /// <summary>
  /// A parsed template: name, text, line table and the root node list.
  /// </summary>
  internal sealed class TemplateDocument {

    private readonly string _Name;
    private readonly string _Text;
    private readonly int[] _LineStarts;
    private TemplateNode[] _Nodes;

    /// <summary>
    /// Creates a document and builds its line table.
    /// </summary>
    public TemplateDocument(string name, string text) {
      _Name = name;
      _Text = text;
      List<int> lineStarts = new List<int>();
      lineStarts.Add(0);
      for (int index = 0; index < text.Length; index++) {
        if (text[index] == '\n') {
          lineStarts.Add(index + 1);
        }
      }
      _LineStarts = lineStarts.ToArray();
      _Nodes = new TemplateNode[0];
    }

    /// <summary>
    /// The template name.
    /// </summary>
    public string Name {
      get {
        return _Name;
      }
    }

    /// <summary>
    /// The template text.
    /// </summary>
    public string Text {
      get {
        return _Text;
      }
    }

    /// <summary>
    /// The root nodes.
    /// </summary>
    public TemplateNode[] Nodes {
      get {
        return _Nodes;
      }
      set {
        _Nodes = value;
      }
    }

    /// <summary>
    /// Creates a located template exception for an absolute offset.
    /// </summary>
    public TemplateException CreateError(int offset, string reason, Exception innerException) {
      int lineIndex = Array.BinarySearch(_LineStarts, offset);
      if (lineIndex < 0) {
        lineIndex = ~lineIndex - 1;
      }
      int column = offset - _LineStarts[lineIndex] + 1;
      return new TemplateException(reason, _Name, lineIndex + 1, column, innerException);
    }

  }

  /// <summary>
  /// Base class of all template nodes.
  /// </summary>
  internal abstract class TemplateNode {

    private readonly int _Offset;

    /// <summary>
    /// Creates a node located at the specified absolute offset.
    /// </summary>
    protected TemplateNode(int offset) {
      _Offset = offset;
    }

    /// <summary>
    /// The absolute offset of the node.
    /// </summary>
    public int Offset {
      get {
        return _Offset;
      }
    }

    /// <summary>
    /// Writes the node output.
    /// </summary>
    public abstract void Render(TextWriter writer, EvaluationContext context);

    /// <summary>
    /// Renders a node list.
    /// </summary>
    public static void RenderAll(TemplateNode[] nodes, TextWriter writer, EvaluationContext context) {
      foreach (TemplateNode node in nodes) {
        node.Render(writer, context);
      }
    }

  }

  /// <summary>
  /// Literal template text (markup, comments, whitespace).
  /// </summary>
  internal sealed class TextNode : TemplateNode {

    private readonly string _Text;

    /// <summary>
    /// Creates a text node.
    /// </summary>
    public TextNode(int offset, string text) : base(offset) {
      _Text = text;
    }

    /// <summary>
    /// The literal text.
    /// </summary>
    public string Text {
      get {
        return _Text;
      }
    }

    /// <summary>
    /// Writes the literal text.
    /// </summary>
    public override void Render(TextWriter writer, EvaluationContext context) {
      writer.Write(_Text);
    }

  }

  /// <summary>
  /// A '{{ expression : format }}' interpolation; HTML encoded unless 'raw' or the value is <see cref="TrustedHtml"/>.
  /// </summary>
  internal sealed class InterpolationNode : TemplateNode {

    private readonly ExpressionNode _Expression;
    private readonly string _Format;
    private readonly bool _Raw;

    /// <summary>
    /// Creates an interpolation node.
    /// </summary>
    public InterpolationNode(int offset, ExpressionNode expression, string format, bool raw) : base(offset) {
      _Expression = expression;
      _Format = format;
      _Raw = raw;
    }

    /// <summary>
    /// Evaluates, formats and writes the value.
    /// </summary>
    public override void Render(TextWriter writer, EvaluationContext context) {
      object value = _Expression.Evaluate(context);
      TrustedHtml trusted = value as TrustedHtml;
      if (trusted != null) {
        writer.Write(trusted.Html);
        return;
      }
      string text;
      try {
        text = context.Runtime.Format(value, _Format, context.Culture);
      } catch (FormatException ex) {
        throw context.Document.CreateError(this.Offset, "Invalid format '" + _Format + "': " + ex.Message, ex);
      }
      if (_Raw) {
        writer.Write(text);
      } else {
        writer.Write(System.Net.WebUtility.HtmlEncode(text));
      }
    }

  }

  /// <summary>
  /// An attribute of an HTML element. Value parts are text and interpolation nodes; a null part list is a boolean attribute.
  /// </summary>
  internal sealed class AttributeNode {

    private readonly string _Name;
    private readonly char _Quote;
    private readonly TemplateNode[] _ValueParts;

    /// <summary>
    /// Creates an attribute node.
    /// </summary>
    public AttributeNode(string name, char quote, TemplateNode[] valueParts) {
      _Name = name;
      _Quote = quote;
      _ValueParts = valueParts;
    }

    /// <summary>
    /// Writes the attribute including the leading space.
    /// </summary>
    public void Render(TextWriter writer, EvaluationContext context) {
      writer.Write(' ');
      writer.Write(_Name);
      if (_ValueParts == null) {
        return;
      }
      writer.Write('=');
      char quote = _Quote;
      if (quote == '\0' && _ValueParts.Length > 0 && !(_ValueParts.Length == 1 && _ValueParts[0] is TextNode)) {
        quote = '"';
      }
      if (quote != '\0') {
        writer.Write(quote);
      }
      TemplateNode.RenderAll(_ValueParts, writer, context);
      if (quote != '\0') {
        writer.Write(quote);
      }
    }

  }

  /// <summary>
  /// Base class of elements supporting the 'u-for' directive. Conditions are handled by <see cref="ConditionalNode"/>.
  /// </summary>
  internal abstract class ElementNode : TemplateNode {

    private ExpressionNode _ForExpression;
    private string _ForItemName;
    private string _ForIndexName;
    private ExpressionNode _IfExpression;
    private ExpressionNode _ElseIfExpression;
    private bool _IsElse;

    /// <summary>
    /// Creates an element node.
    /// </summary>
    protected ElementNode(int offset) : base(offset) {
    }

    /// <summary>
    /// The 'u-if' condition, or null.
    /// </summary>
    public ExpressionNode IfExpression {
      get {
        return _IfExpression;
      }
      set {
        _IfExpression = value;
      }
    }

    /// <summary>
    /// The 'u-else-if' condition, or null.
    /// </summary>
    public ExpressionNode ElseIfExpression {
      get {
        return _ElseIfExpression;
      }
      set {
        _ElseIfExpression = value;
      }
    }

    /// <summary>
    /// True when the element carries 'u-else'.
    /// </summary>
    public bool IsElse {
      get {
        return _IsElse;
      }
      set {
        _IsElse = value;
      }
    }

    /// <summary>
    /// Configures the 'u-for' directive.
    /// </summary>
    public void SetIteration(ExpressionNode expression, string itemName, string indexName) {
      _ForExpression = expression;
      _ForItemName = itemName;
      _ForIndexName = indexName;
    }

    /// <summary>
    /// Renders the element once or once per item of the 'u-for' collection.
    /// Each iteration gets a nested scope that inherits all lexical locals.
    /// </summary>
    public override void Render(TextWriter writer, EvaluationContext context) {
      if (_ForExpression == null) {
        this.RenderElement(writer, context);
        return;
      }
      object collection = _ForExpression.Evaluate(context);
      if (collection == null) {
        return;
      }
      IEnumerable enumerable = collection as IEnumerable;
      if (enumerable == null || collection is string) {
        throw context.Document.CreateError(this.Offset, "'u-for' requires an enumerable (non-string) value but got '" + collection.GetType().FullName + "'.", null);
      }
      int index = 0;
      foreach (object item in enumerable) {
        RenderScope iterationScope = context.Scope.CreateNested();
        iterationScope.SetLocal(_ForItemName, item);
        if (_ForIndexName != null) {
          iterationScope.SetLocal(_ForIndexName, index);
        }
        this.RenderElement(writer, context.WithScope(iterationScope));
        index++;
      }
    }

    /// <summary>
    /// Renders the element itself (without directives).
    /// </summary>
    protected abstract void RenderElement(TextWriter writer, EvaluationContext context);

  }

  /// <summary>
  /// An ordinary HTML element whose tag is written to the output.
  /// </summary>
  internal sealed class HtmlElementNode : ElementNode {

    private readonly string _TagName;
    private readonly AttributeNode[] _Attributes;
    private readonly TemplateNode[] _Children;
    private readonly bool _SelfClosing;
    private readonly bool _HasEndTag;

    /// <summary>
    /// Creates an HTML element node.
    /// </summary>
    public HtmlElementNode(int offset, string tagName, AttributeNode[] attributes, TemplateNode[] children, bool selfClosing, bool hasEndTag) : base(offset) {
      _TagName = tagName;
      _Attributes = attributes;
      _Children = children;
      _SelfClosing = selfClosing;
      _HasEndTag = hasEndTag;
    }

    /// <summary>
    /// Writes start tag, children and end tag.
    /// </summary>
    protected override void RenderElement(TextWriter writer, EvaluationContext context) {
      writer.Write('<');
      writer.Write(_TagName);
      foreach (AttributeNode attribute in _Attributes) {
        attribute.Render(writer, context);
      }
      if (_SelfClosing) {
        writer.Write(" />");
        return;
      }
      writer.Write('>');
      TemplateNode.RenderAll(_Children, writer, context);
      if (_HasEndTag) {
        writer.Write("</");
        writer.Write(_TagName);
        writer.Write('>');
      }
    }

  }

  /// <summary>
  /// '&lt;u-block&gt;': renders only its children; used to apply directives without a wrapper element.
  /// </summary>
  internal sealed class BlockNode : ElementNode {

    private readonly TemplateNode[] _Children;

    /// <summary>
    /// Creates a block node.
    /// </summary>
    public BlockNode(int offset, TemplateNode[] children) : base(offset) {
      _Children = children;
    }

    /// <summary>
    /// Renders the children.
    /// </summary>
    protected override void RenderElement(TextWriter writer, EvaluationContext context) {
      TemplateNode.RenderAll(_Children, writer, context);
    }

  }

  /// <summary>
  /// Content passed to a slot, rendered in the lexical context of the caller.
  /// </summary>
  internal sealed class SlotContent {

    private readonly TemplateNode[] _Nodes;
    private readonly EvaluationContext _CallerContext;

    /// <summary>
    /// Creates slot content bound to the caller context.
    /// </summary>
    public SlotContent(TemplateNode[] nodes, EvaluationContext callerContext) {
      _Nodes = nodes;
      _CallerContext = callerContext;
    }

    /// <summary>
    /// Renders the content in the caller context.
    /// </summary>
    public void Render(TextWriter writer) {
      TemplateNode.RenderAll(_Nodes, writer, _CallerContext);
    }

  }

  /// <summary>
  /// '&lt;u-include template="name" param="expression"&gt;' with optional '&lt;u-fill slot="name"&gt;' children.
  /// The included template inherits all data sources but only the explicitly passed parameters as locals.
  /// Layouts are includes whose content is passed via slots.
  /// </summary>
  internal sealed class IncludeNode : ElementNode {

    private readonly string _TemplateName;
    private readonly string[] _ParameterNames;
    private readonly ExpressionNode[] _ParameterExpressions;
    private readonly Dictionary<string, TemplateNode[]> _Fills;

    /// <summary>
    /// Creates an include node.
    /// </summary>
    public IncludeNode(int offset, string templateName, string[] parameterNames, ExpressionNode[] parameterExpressions, Dictionary<string, TemplateNode[]> fills) : base(offset) {
      _TemplateName = templateName;
      _ParameterNames = parameterNames;
      _ParameterExpressions = parameterExpressions;
      _Fills = fills;
    }

    /// <summary>
    /// Renders the included template in an isolated scope.
    /// </summary>
    protected override void RenderElement(TextWriter writer, EvaluationContext context) {
      if (context.Scope.IncludeDepth >= context.Runtime.MaxIncludeDepth) {
        throw context.Document.CreateError(this.Offset, "Maximum include depth of " + context.Runtime.MaxIncludeDepth + " exceeded (cyclic include?): " + context.Scope.DescribeIncludeChain() + " -> " + _TemplateName, null);
      }
      TemplateDocument document = context.Runtime.LoadDocument(_TemplateName);
      if (document == null) {
        throw context.Document.CreateError(this.Offset, "Included template '" + _TemplateName + "' was not found.", null);
      }
      Dictionary<string, SlotContent> slots = new Dictionary<string, SlotContent>(StringComparer.Ordinal);
      foreach (KeyValuePair<string, TemplateNode[]> fill in _Fills) {
        slots.Add(fill.Key, new SlotContent(fill.Value, context));
      }
      RenderScope includeScope = context.Scope.CreateInclude(_TemplateName, slots);
      for (int index = 0; index < _ParameterNames.Length; index++) {
        includeScope.SetLocal(_ParameterNames[index], _ParameterExpressions[index].Evaluate(context));
      }
      TemplateNode.RenderAll(document.Nodes, writer, context.WithDocument(document, includeScope));
    }

  }

  /// <summary>
  /// '&lt;u-slot name="x"&gt;default&lt;/u-slot&gt;': outputs the content passed by the includer, or its own default content.
  /// A slot without name is the default slot receiving all non-fill content of the include element.
  /// </summary>
  internal sealed class SlotNode : ElementNode {

    private readonly string _SlotName;
    private readonly TemplateNode[] _DefaultContent;

    /// <summary>
    /// Creates a slot node.
    /// </summary>
    public SlotNode(int offset, string slotName, TemplateNode[] defaultContent) : base(offset) {
      _SlotName = slotName;
      _DefaultContent = defaultContent;
    }

    /// <summary>
    /// Renders the passed content or the default content.
    /// </summary>
    protected override void RenderElement(TextWriter writer, EvaluationContext context) {
      SlotContent content;
      if (context.Scope.TryGetSlot(_SlotName, out content)) {
        content.Render(writer);
      } else {
        TemplateNode.RenderAll(_DefaultContent, writer, context);
      }
    }

  }

  /// <summary>
  /// '&lt;u-fill slot="name"&gt;': content for a named slot; only valid as direct child of '&lt;u-include&gt;'.
  /// Collected by the include node at parse time and never rendered directly.
  /// </summary>
  internal sealed class FillNode : TemplateNode {

    private readonly string _SlotName;
    private readonly TemplateNode[] _Children;

    /// <summary>
    /// Creates a fill node.
    /// </summary>
    public FillNode(int offset, string slotName, TemplateNode[] children) : base(offset) {
      _SlotName = slotName;
      _Children = children;
    }

    /// <summary>
    /// The name of the filled slot.
    /// </summary>
    public string SlotName {
      get {
        return _SlotName;
      }
    }

    /// <summary>
    /// The fill content.
    /// </summary>
    public TemplateNode[] Children {
      get {
        return _Children;
      }
    }

    /// <summary>
    /// Fill nodes are consumed by their include node and never rendered directly.
    /// </summary>
    public override void Render(TextWriter writer, EvaluationContext context) {
      throw context.Document.CreateError(this.Offset, "<u-fill> is only allowed as direct child of <u-include>.", null);
    }

  }

  /// <summary>
  /// A 'u-if' / 'u-else-if' / 'u-else' chain of sibling elements.
  /// </summary>
  internal sealed class ConditionalNode : TemplateNode {

    private readonly ExpressionNode[] _Conditions;
    private readonly ElementNode[] _Branches;

    /// <summary>
    /// Creates a conditional node. A null condition denotes the 'u-else' branch.
    /// </summary>
    public ConditionalNode(int offset, ExpressionNode[] conditions, ElementNode[] branches) : base(offset) {
      _Conditions = conditions;
      _Branches = branches;
    }

    /// <summary>
    /// Renders the first branch whose condition is true.
    /// </summary>
    public override void Render(TextWriter writer, EvaluationContext context) {
      for (int index = 0; index < _Branches.Length; index++) {
        if (_Conditions[index] == null || context.Runtime.IsTrue(_Conditions[index].Evaluate(context))) {
          _Branches[index].Render(writer, context);
          return;
        }
      }
    }

  }

  /// <summary>
  /// HTML-aware template parser. Recognizes elements, attributes, comments, raw text elements (script, style)
  /// and interpolations, and builds a node tree with directive information. No regular expressions are used.
  /// </summary>
  internal sealed class TemplateParser {

    private static readonly HashSet<string> _VoidElements = new HashSet<string>(StringComparer.OrdinalIgnoreCase) {
      "area", "base", "br", "col", "embed", "hr", "img", "input", "link", "meta", "param", "source", "track", "wbr"
    };

    private static readonly HashSet<string> _RawTextElements = new HashSet<string>(StringComparer.OrdinalIgnoreCase) {
      "script", "style", "textarea", "title"
    };

    private static readonly HashSet<string> _OptionalEndTagElements = new HashSet<string>(StringComparer.OrdinalIgnoreCase) {
      "p", "li", "dt", "dd", "tr", "td", "th", "thead", "tbody", "tfoot", "option", "optgroup", "colgroup", "html", "head", "body"
    };

    private readonly TemplateDocument _Document;
    private readonly string _Text;
    private readonly List<string> _OpenElements = new List<string>();
    private int _Position;

    /// <summary>
    /// Creates a parser for a document.
    /// </summary>
    private TemplateParser(TemplateDocument document) {
      _Document = document;
      _Text = document.Text;
      _Position = 0;
    }

    /// <summary>
    /// Parses template text into a document.
    /// </summary>
    public static TemplateDocument Parse(string templateName, string templateText) {
      TemplateDocument document = new TemplateDocument(templateName, templateText);
      TemplateParser parser = new TemplateParser(document);
      bool closedExplicitly;
      document.Nodes = parser.ParseContent(null, 0, out closedExplicitly);
      return document;
    }

    /// <summary>
    /// Parses child content until the end tag of the parent element (or the end of the text for the root).
    /// </summary>
    private TemplateNode[] ParseContent(string parentTag, int parentOffset, out bool closedExplicitly) {
      List<TemplateNode> nodes = new List<TemplateNode>();
      StringBuilder text = new StringBuilder();
      int textStart = _Position;
      closedExplicitly = false;
      while (_Position < _Text.Length) {
        if (this.StartsWith("{{")) {
          this.FlushText(nodes, text, textStart);
          nodes.Add(this.ParseInterpolation(_Position));
          textStart = _Position;
          continue;
        }
        if (this.StartsWith("<!--")) {
          int commentEnd = _Text.IndexOf("-->", _Position + 4, StringComparison.Ordinal);
          if (commentEnd < 0) {
            throw _Document.CreateError(_Position, "Unclosed HTML comment.", null);
          }
          text.Append(_Text, _Position, commentEnd + 3 - _Position);
          _Position = commentEnd + 3;
          continue;
        }
        if (this.StartsWith("<!") || this.StartsWith("<?")) {
          int declarationEnd = _Text.IndexOf('>', _Position);
          if (declarationEnd < 0) {
            throw _Document.CreateError(_Position, "Unclosed markup declaration.", null);
          }
          text.Append(_Text, _Position, declarationEnd + 1 - _Position);
          _Position = declarationEnd + 1;
          continue;
        }
        if (this.StartsWith("</") && _Position + 2 < _Text.Length && char.IsLetter(_Text[_Position + 2])) {
          int closeStart = _Position;
          string closeName = this.ReadName(_Position + 2);
          if (parentTag != null && string.Equals(closeName, parentTag, StringComparison.OrdinalIgnoreCase)) {
            this.FlushText(nodes, text, textStart);
            int closeEnd = _Text.IndexOf('>', closeStart);
            if (closeEnd < 0) {
              throw _Document.CreateError(closeStart, "Unclosed end tag '</" + closeName + "'.", null);
            }
            _Position = closeEnd + 1;
            closedExplicitly = true;
            return this.GroupConditionals(nodes);
          }
          if (parentTag != null && this.IsOpenAncestor(closeName) && _OptionalEndTagElements.Contains(parentTag)) {
            this.FlushText(nodes, text, textStart);
            return this.GroupConditionals(nodes);
          }
          if (parentTag != null && this.IsOpenAncestor(closeName)) {
            throw _Document.CreateError(parentOffset, "Element <" + parentTag + "> is not closed before '</" + closeName + ">'.", null);
          }
          throw _Document.CreateError(closeStart, "Unexpected end tag '</" + closeName + ">'.", null);
        }
        if (_Text[_Position] == '<' && _Position + 1 < _Text.Length && char.IsLetter(_Text[_Position + 1])) {
          this.FlushText(nodes, text, textStart);
          TemplateNode element = this.ParseElement();
          nodes.Add(element);
          textStart = _Position;
          continue;
        }
        text.Append(_Text[_Position]);
        _Position++;
      }
      this.FlushText(nodes, text, textStart);
      if (parentTag != null && (!_OptionalEndTagElements.Contains(parentTag) || parentTag.StartsWith("u-", StringComparison.OrdinalIgnoreCase))) {
        throw _Document.CreateError(parentOffset, "Element <" + parentTag + "> is not closed.", null);
      }
      return this.GroupConditionals(nodes);
    }

    /// <summary>
    /// Returns true when an element with the specified name is currently open (excluding the innermost element).
    /// </summary>
    private bool IsOpenAncestor(string tagName) {
      for (int index = _OpenElements.Count - 2; index >= 0; index--) {
        if (string.Equals(_OpenElements[index], tagName, StringComparison.OrdinalIgnoreCase)) {
          return true;
        }
      }
      return false;
    }

    /// <summary>
    /// Adds pending literal text as a text node.
    /// </summary>
    private void FlushText(List<TemplateNode> nodes, StringBuilder text, int textStart) {
      if (text.Length > 0) {
        nodes.Add(new TextNode(textStart, text.ToString()));
        text.Clear();
      }
    }

    /// <summary>
    /// Returns true when the text at the current position starts with the specified value.
    /// </summary>
    private bool StartsWith(string value) {
      return string.CompareOrdinal(_Text, _Position, value, 0, value.Length) == 0;
    }

    /// <summary>
    /// Reads a tag or attribute name starting at the specified position.
    /// </summary>
    private string ReadName(int start) {
      int end = start;
      while (end < _Text.Length && !char.IsWhiteSpace(_Text[end]) && _Text[end] != '>' && _Text[end] != '/' && _Text[end] != '=' && _Text[end] != '"' && _Text[end] != '\'') {
        end++;
      }
      return _Text.Substring(start, end - start);
    }

    /// <summary>
    /// Finds the offset of the closing '}}' of an interpolation starting at the specified '{{', skipping string literals.
    /// </summary>
    private int FindInterpolationEnd(int start) {
      int position = start + 2;
      while (position < _Text.Length) {
        char current = _Text[position];
        if (current == '"' || current == '\'') {
          position++;
          while (position < _Text.Length && _Text[position] != current) {
            if (_Text[position] == '\\') {
              position++;
            }
            position++;
          }
          position++;
          continue;
        }
        if (current == '}' && position + 1 < _Text.Length && _Text[position + 1] == '}') {
          return position;
        }
        position++;
      }
      throw _Document.CreateError(start, "Unclosed interpolation '{{'.", null);
    }

    /// <summary>
    /// Parses an interpolation at the specified position and advances behind it.
    /// </summary>
    private InterpolationNode ParseInterpolation(int start) {
      int end = this.FindInterpolationEnd(start);
      int expressionStart = start + 2;
      while (expressionStart < end && char.IsWhiteSpace(_Text[expressionStart])) {
        expressionStart++;
      }
      bool raw = false;
      if (end - expressionStart > 4 && string.CompareOrdinal(_Text, expressionStart, "raw", 0, 3) == 0 && char.IsWhiteSpace(_Text[expressionStart + 3])) {
        raw = true;
        expressionStart += 4;
      }
      string format;
      ExpressionNode expression = ExpressionParser.ParseInterpolation(_Document, expressionStart, end, out format);
      _Position = end + 2;
      return new InterpolationNode(start, expression, format, raw);
    }

    /// <summary>
    /// Parses text with interpolations between two offsets (attribute values and raw text element content).
    /// </summary>
    private TemplateNode[] ParseTextWithInterpolations(int start, int end) {
      List<TemplateNode> parts = new List<TemplateNode>();
      int savedPosition = _Position;
      _Position = start;
      int textStart = start;
      while (_Position < end) {
        if (this.StartsWith("{{")) {
          if (_Position > textStart) {
            parts.Add(new TextNode(textStart, _Text.Substring(textStart, _Position - textStart)));
          }
          InterpolationNode interpolation = this.ParseInterpolation(_Position);
          if (_Position > end) {
            throw _Document.CreateError(interpolation.Offset, "Interpolation exceeds its enclosing attribute or element.", null);
          }
          parts.Add(interpolation);
          textStart = _Position;
        } else {
          _Position++;
        }
      }
      if (end > textStart) {
        parts.Add(new TextNode(textStart, _Text.Substring(textStart, end - textStart)));
      }
      _Position = savedPosition;
      return parts.ToArray();
    }

    /// <summary>
    /// Parses an element starting at '&lt;' including its content and end tag.
    /// </summary>
    private TemplateNode ParseElement() {
      int elementStart = _Position;
      string parentTag = null;
      if (_OpenElements.Count > 0) {
        parentTag = _OpenElements[_OpenElements.Count - 1];
      }
      string tagName = this.ReadName(_Position + 1);
      _Position += 1 + tagName.Length;
      List<ParsedAttribute> attributes = new List<ParsedAttribute>();
      bool selfClosing = false;
      while (true) {
        while (_Position < _Text.Length && char.IsWhiteSpace(_Text[_Position])) {
          _Position++;
        }
        if (_Position >= _Text.Length) {
          throw _Document.CreateError(elementStart, "Unclosed start tag <" + tagName + ">.", null);
        }
        if (_Text[_Position] == '>') {
          _Position++;
          break;
        }
        if (this.StartsWith("/>")) {
          _Position += 2;
          selfClosing = true;
          break;
        }
        attributes.Add(this.ParseAttribute(tagName));
      }
      string lowerTagName = tagName.ToLowerInvariant();
      bool isVoid = _VoidElements.Contains(tagName);
      TemplateNode[] children = new TemplateNode[0];
      bool hasEndTag = false;
      if (!selfClosing && !isVoid) {
        if (_RawTextElements.Contains(tagName)) {
          int closeStart = this.FindRawTextEnd(tagName, elementStart);
          children = this.ParseTextWithInterpolations(_Position, closeStart);
          int closeEnd = _Text.IndexOf('>', closeStart);
          _Position = closeEnd + 1;
          hasEndTag = true;
        } else {
          _OpenElements.Add(tagName);
          children = this.ParseContent(tagName, elementStart, out hasEndTag);
          _OpenElements.RemoveAt(_OpenElements.Count - 1);
        }
      }
      ElementNode element;
      if (lowerTagName == "u-block") {
        this.EnsureNoPlainAttributes(attributes, tagName);
        element = new BlockNode(elementStart, children);
      } else if (lowerTagName == "u-include") {
        element = this.CreateInclude(elementStart, attributes, children);
      } else if (lowerTagName == "u-slot") {
        string slotName = string.Empty;
        foreach (ParsedAttribute attribute in attributes) {
          if (attribute.IsDirective) {
            continue;
          }
          if (attribute.Name != "name") {
            throw _Document.CreateError(attribute.Offset, "Unsupported attribute '" + attribute.Name + "' on <u-slot>.", null);
          }
          slotName = this.GetLiteralValue(attribute);
        }
        element = new SlotNode(elementStart, slotName, children);
      } else if (lowerTagName == "u-fill") {
        if (parentTag == null || !string.Equals(parentTag, "u-include", StringComparison.OrdinalIgnoreCase)) {
          throw _Document.CreateError(elementStart, "<u-fill> is only allowed as direct child of <u-include>.", null);
        }
        string slotName = null;
        foreach (ParsedAttribute attribute in attributes) {
          if (attribute.Name != "slot") {
            throw _Document.CreateError(attribute.Offset, "Unsupported attribute '" + attribute.Name + "' on <u-fill>.", null);
          }
          slotName = this.GetLiteralValue(attribute);
        }
        if (slotName == null) {
          throw _Document.CreateError(elementStart, "<u-fill> requires a 'slot' attribute.", null);
        }
        return new FillNode(elementStart, slotName, children);
      } else if (lowerTagName.StartsWith("u-", StringComparison.Ordinal)) {
        throw _Document.CreateError(elementStart, "Unknown directive element <" + tagName + ">.", null);
      } else {
        List<AttributeNode> attributeNodes = new List<AttributeNode>();
        foreach (ParsedAttribute attribute in attributes) {
          if (!attribute.IsDirective) {
            attributeNodes.Add(new AttributeNode(attribute.Name, attribute.Quote, attribute.ValueParts));
          }
        }
        element = new HtmlElementNode(elementStart, tagName, attributeNodes.ToArray(), children, selfClosing, hasEndTag);
      }
      this.ApplyDirectives(element, attributes, tagName);
      return element;
    }

    /// <summary>
    /// Finds the start of the end tag of a raw text element (script, style, textarea, title).
    /// </summary>
    private int FindRawTextEnd(string tagName, int elementStart) {
      string closeTag = "</" + tagName;
      int closeStart = _Text.IndexOf(closeTag, _Position, StringComparison.OrdinalIgnoreCase);
      if (closeStart < 0) {
        throw _Document.CreateError(elementStart, "Element <" + tagName + "> is not closed.", null);
      }
      return closeStart;
    }

    /// <summary>
    /// Parses one attribute (name, optional value) at the current position.
    /// </summary>
    private ParsedAttribute ParseAttribute(string tagName) {
      int attributeStart = _Position;
      string name = this.ReadName(_Position);
      if (name.Length == 0) {
        throw _Document.CreateError(_Position, "Invalid character '" + _Text[_Position] + "' in start tag <" + tagName + ">.", null);
      }
      _Position += name.Length;
      int lookAhead = _Position;
      while (lookAhead < _Text.Length && char.IsWhiteSpace(_Text[lookAhead])) {
        lookAhead++;
      }
      bool isDirective = name.StartsWith("u-", StringComparison.OrdinalIgnoreCase);
      if (lookAhead >= _Text.Length || _Text[lookAhead] != '=') {
        return new ParsedAttribute(name, attributeStart, isDirective, '\0', -1, -1, null);
      }
      _Position = lookAhead + 1;
      while (_Position < _Text.Length && char.IsWhiteSpace(_Text[_Position])) {
        _Position++;
      }
      if (_Position >= _Text.Length) {
        throw _Document.CreateError(attributeStart, "Missing value for attribute '" + name + "'.", null);
      }
      char quote = _Text[_Position];
      int valueStart;
      int valueEnd;
      if (quote == '"' || quote == '\'') {
        valueStart = _Position + 1;
        valueEnd = this.FindAttributeValueEnd(valueStart, quote, isDirective, attributeStart);
        _Position = valueEnd + 1;
      } else {
        quote = '\0';
        valueStart = _Position;
        while (_Position < _Text.Length && !char.IsWhiteSpace(_Text[_Position]) && _Text[_Position] != '>') {
          if (this.StartsWith("/>")) {
            break;
          }
          _Position++;
        }
        valueEnd = _Position;
      }
      TemplateNode[] valueParts = null;
      if (!isDirective) {
        valueParts = this.ParseTextWithInterpolations(valueStart, valueEnd);
      }
      return new ParsedAttribute(name, attributeStart, isDirective, quote, valueStart, valueEnd, valueParts);
    }

    /// <summary>
    /// Finds the closing quote of an attribute value. In ordinary attributes, interpolations (including their
    /// string literals) are skipped; in directive attributes, string literals using the other quote are skipped.
    /// </summary>
    private int FindAttributeValueEnd(int valueStart, char quote, bool isDirective, int attributeStart) {
      int position = valueStart;
      while (position < _Text.Length) {
        char current = _Text[position];
        if (!isDirective && current == '{' && position + 1 < _Text.Length && _Text[position + 1] == '{') {
          position = this.FindInterpolationEnd(position) + 2;
          continue;
        }
        if (current == quote) {
          return position;
        }
        position++;
      }
      throw _Document.CreateError(attributeStart, "Unclosed attribute value.", null);
    }

    /// <summary>
    /// Returns the literal text of an attribute value; interpolations are not allowed.
    /// </summary>
    private string GetLiteralValue(ParsedAttribute attribute) {
      if (attribute.ValueStart < 0) {
        throw _Document.CreateError(attribute.Offset, "Attribute '" + attribute.Name + "' requires a value.", null);
      }
      string value = _Text.Substring(attribute.ValueStart, attribute.ValueEnd - attribute.ValueStart);
      if (value.Contains("{{")) {
        throw _Document.CreateError(attribute.Offset, "Attribute '" + attribute.Name + "' must be a literal value.", null);
      }
      return value;
    }

    /// <summary>
    /// Ensures that a directive element carries only directive attributes.
    /// </summary>
    private void EnsureNoPlainAttributes(List<ParsedAttribute> attributes, string tagName) {
      foreach (ParsedAttribute attribute in attributes) {
        if (!attribute.IsDirective) {
          throw _Document.CreateError(attribute.Offset, "Unsupported attribute '" + attribute.Name + "' on <" + tagName + ">.", null);
        }
      }
    }

    /// <summary>
    /// Creates an include node from its attributes (template name and parameter expressions) and its fill children.
    /// </summary>
    private IncludeNode CreateInclude(int elementStart, List<ParsedAttribute> attributes, TemplateNode[] children) {
      string templateName = null;
      List<string> parameterNames = new List<string>();
      List<ExpressionNode> parameterExpressions = new List<ExpressionNode>();
      foreach (ParsedAttribute attribute in attributes) {
        if (attribute.IsDirective) {
          continue;
        }
        if (attribute.Name == "template") {
          templateName = this.GetLiteralValue(attribute);
          continue;
        }
        if (!TemplateContext.IsIdentifier(attribute.Name) || attribute.Name.StartsWith("_", StringComparison.Ordinal)) {
          throw _Document.CreateError(attribute.Offset, "Include parameter '" + attribute.Name + "' must be a valid local variable name.", null);
        }
        if (attribute.ValueStart < 0) {
          throw _Document.CreateError(attribute.Offset, "Include parameter '" + attribute.Name + "' requires an expression.", null);
        }
        if (parameterNames.Contains(attribute.Name)) {
          throw _Document.CreateError(attribute.Offset, "Duplicate include parameter '" + attribute.Name + "'.", null);
        }
        parameterNames.Add(attribute.Name);
        parameterExpressions.Add(ExpressionParser.ParseExpression(_Document, attribute.ValueStart, attribute.ValueEnd));
      }
      if (string.IsNullOrWhiteSpace(templateName)) {
        throw _Document.CreateError(elementStart, "<u-include> requires a 'template' attribute.", null);
      }
      Dictionary<string, TemplateNode[]> fills = new Dictionary<string, TemplateNode[]>(StringComparer.Ordinal);
      List<TemplateNode> defaultContent = new List<TemplateNode>();
      bool hasDefaultContent = false;
      foreach (TemplateNode child in children) {
        FillNode fill = child as FillNode;
        if (fill != null) {
          if (fills.ContainsKey(fill.SlotName)) {
            throw _Document.CreateError(fill.Offset, "Duplicate fill for slot '" + fill.SlotName + "'.", null);
          }
          fills.Add(fill.SlotName, fill.Children);
          continue;
        }
        TextNode textNode = child as TextNode;
        if (textNode == null || textNode.Text.Trim().Length > 0) {
          hasDefaultContent = true;
        }
        defaultContent.Add(child);
      }
      if (hasDefaultContent) {
        if (fills.ContainsKey(string.Empty)) {
          throw _Document.CreateError(elementStart, "The default slot is filled twice.", null);
        }
        fills.Add(string.Empty, defaultContent.ToArray());
      }
      return new IncludeNode(elementStart, templateName, parameterNames.ToArray(), parameterExpressions.ToArray(), fills);
    }

    /// <summary>
    /// Applies the 'u-for', 'u-if', 'u-else-if' and 'u-else' directive attributes to an element.
    /// </summary>
    private void ApplyDirectives(ElementNode element, List<ParsedAttribute> attributes, string tagName) {
      int conditionDirectives = 0;
      foreach (ParsedAttribute attribute in attributes) {
        if (!attribute.IsDirective) {
          continue;
        }
        string directive = attribute.Name.ToLowerInvariant();
        if (directive == "u-else") {
          if (attribute.ValueStart >= 0 && attribute.ValueEnd > attribute.ValueStart) {
            throw _Document.CreateError(attribute.Offset, "'u-else' does not take a value.", null);
          }
          element.IsElse = true;
          conditionDirectives++;
          continue;
        }
        if (attribute.ValueStart < 0) {
          throw _Document.CreateError(attribute.Offset, "Directive '" + attribute.Name + "' requires a value.", null);
        }
        if (directive == "u-for") {
          string itemName;
          string indexName;
          ExpressionNode collection = ExpressionParser.ParseIteration(_Document, attribute.ValueStart, attribute.ValueEnd, out itemName, out indexName);
          element.SetIteration(collection, itemName, indexName);
        } else if (directive == "u-if") {
          element.IfExpression = ExpressionParser.ParseExpression(_Document, attribute.ValueStart, attribute.ValueEnd);
          conditionDirectives++;
        } else if (directive == "u-else-if") {
          element.ElseIfExpression = ExpressionParser.ParseExpression(_Document, attribute.ValueStart, attribute.ValueEnd);
          conditionDirectives++;
        } else {
          throw _Document.CreateError(attribute.Offset, "Unknown directive '" + attribute.Name + "' on <" + tagName + ">.", null);
        }
      }
      if (conditionDirectives > 1) {
        throw _Document.CreateError(element.Offset, "Only one of 'u-if', 'u-else-if' and 'u-else' is allowed per element.", null);
      }
    }

    /// <summary>
    /// Groups 'u-if' / 'u-else-if' / 'u-else' sibling chains into conditional nodes.
    /// Whitespace between chained siblings is dropped. Fill elements are converted into fill nodes.
    /// </summary>
    private TemplateNode[] GroupConditionals(List<TemplateNode> nodes) {
      List<TemplateNode> result = new List<TemplateNode>();
      int index = 0;
      while (index < nodes.Count) {
        ElementNode element = nodes[index] as ElementNode;
        if (element == null || (element.IfExpression == null && element.ElseIfExpression == null && !element.IsElse)) {
          result.Add(nodes[index]);
          index++;
          continue;
        }
        if (element.IfExpression == null) {
          throw _Document.CreateError(element.Offset, "'u-else-if' / 'u-else' must directly follow an element with 'u-if' or 'u-else-if'.", null);
        }
        List<ExpressionNode> conditions = new List<ExpressionNode>();
        List<ElementNode> branches = new List<ElementNode>();
        conditions.Add(element.IfExpression);
        branches.Add(element);
        index++;
        bool chainOpen = true;
        while (chainOpen) {
          int next = index;
          while (next < nodes.Count && nodes[next] is TextNode && ((TextNode)nodes[next]).Text.Trim().Length == 0) {
            next++;
          }
          ElementNode follower = null;
          if (next < nodes.Count) {
            follower = nodes[next] as ElementNode;
          }
          if (follower != null && follower.ElseIfExpression != null) {
            conditions.Add(follower.ElseIfExpression);
            branches.Add(follower);
            index = next + 1;
          } else if (follower != null && follower.IsElse) {
            conditions.Add(null);
            branches.Add(follower);
            index = next + 1;
            chainOpen = false;
          } else {
            chainOpen = false;
          }
        }
        result.Add(new ConditionalNode(element.Offset, conditions.ToArray(), branches.ToArray()));
      }
      return result.ToArray();
    }

    /// <summary>
    /// Intermediate attribute representation used while parsing a start tag.
    /// </summary>
    private sealed class ParsedAttribute {

      private readonly string _Name;
      private readonly int _Offset;
      private readonly bool _IsDirective;
      private readonly char _Quote;
      private readonly int _ValueStart;
      private readonly int _ValueEnd;
      private readonly TemplateNode[] _ValueParts;

      /// <summary>
      /// Creates a parsed attribute.
      /// </summary>
      public ParsedAttribute(string name, int offset, bool isDirective, char quote, int valueStart, int valueEnd, TemplateNode[] valueParts) {
        _Name = name;
        _Offset = offset;
        _IsDirective = isDirective;
        _Quote = quote;
        _ValueStart = valueStart;
        _ValueEnd = valueEnd;
        _ValueParts = valueParts;
      }

      /// <summary>
      /// The attribute name as written.
      /// </summary>
      public string Name {
        get {
          return _Name;
        }
      }

      /// <summary>
      /// The absolute offset of the attribute.
      /// </summary>
      public int Offset {
        get {
          return _Offset;
        }
      }

      /// <summary>
      /// True for 'u-*' directive attributes.
      /// </summary>
      public bool IsDirective {
        get {
          return _IsDirective;
        }
      }

      /// <summary>
      /// The quote character, or '\0' for unquoted or valueless attributes.
      /// </summary>
      public char Quote {
        get {
          return _Quote;
        }
      }

      /// <summary>
      /// The absolute start offset of the value, or -1 when the attribute has no value.
      /// </summary>
      public int ValueStart {
        get {
          return _ValueStart;
        }
      }

      /// <summary>
      /// The absolute end offset (exclusive) of the value, or -1 when the attribute has no value.
      /// </summary>
      public int ValueEnd {
        get {
          return _ValueEnd;
        }
      }

      /// <summary>
      /// The parsed value parts of ordinary attributes, or null.
      /// </summary>
      public TemplateNode[] ValueParts {
        get {
          return _ValueParts;
        }
      }

    }

  }

}
