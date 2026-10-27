using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Globalization;
using System.IO;

namespace HtmlDocGen {

  // v 2.0.0

  /// <summary>
  /// Options of a <see cref="TemplateEngine"/>.
  /// </summary>
  public sealed class TemplateEngineOptions {

    private CultureInfo _Culture = CultureInfo.InvariantCulture;
    private MemberAccessPolicy _Policy = MemberAccessPolicy.Trusted;
    private int _MaxIncludeDepth = 32;
    private bool _CacheParsedTemplates = true;

    /// <summary>
    /// The culture used for formatting values (default: invariant culture).
    /// Can be overridden per rendering operation via <see cref="TemplateContext.Culture"/>.
    /// </summary>
    public CultureInfo Culture {
      get {
        return _Culture;
      }
      set {
        if (value == null) {
          throw new ArgumentNullException(nameof(value));
        }
        _Culture = value;
      }
    }

    /// <summary>
    /// The member access policy (default: <see cref="MemberAccessPolicy.Trusted"/>).
    /// </summary>
    public MemberAccessPolicy Policy {
      get {
        return _Policy;
      }
      set {
        if (value == null) {
          throw new ArgumentNullException(nameof(value));
        }
        _Policy = value;
      }
    }

    /// <summary>
    /// The maximum include nesting depth; exceeding it reports a cyclic include (default: 32).
    /// </summary>
    public int MaxIncludeDepth {
      get {
        return _MaxIncludeDepth;
      }
      set {
        if (value < 1) {
          throw new ArgumentOutOfRangeException(nameof(value));
        }
        _MaxIncludeDepth = value;
      }
    }

    /// <summary>
    /// True to cache parsed templates by name (default). Disable during template development with a file source.
    /// </summary>
    public bool CacheParsedTemplates {
      get {
        return _CacheParsedTemplates;
      }
      set {
        _CacheParsedTemplates = value;
      }
    }

  }

  /// <summary>
  /// Dependency-free HTML-first template renderer. Thread-safe; create one instance per template source and reuse it.
  /// </summary>
  public sealed class TemplateEngine {

    private readonly TemplateRuntime _Runtime;

    /// <summary>
    /// Creates an engine with default options.
    /// </summary>
    /// <param name="source">The template source.</param>
    public TemplateEngine(ITemplateSource source) : this(source, new TemplateEngineOptions()) {
    }

    /// <summary>
    /// Creates an engine with default options and the specified culture.
    /// </summary>
    /// <param name="source">The template source.</param>
    /// <param name="culture">The formatting culture.</param>
    public TemplateEngine(ITemplateSource source, CultureInfo culture) : this(source, TemplateEngine.CreateOptions(culture)) {
    }

    /// <summary>
    /// Creates an engine with the specified options.
    /// </summary>
    /// <param name="source">The template source.</param>
    /// <param name="options">The engine options.</param>
    public TemplateEngine(ITemplateSource source, TemplateEngineOptions options) {
      if (source == null) {
        throw new ArgumentNullException(nameof(source));
      }
      if (options == null) {
        throw new ArgumentNullException(nameof(options));
      }
      _Runtime = new TemplateRuntime(source, options.Culture, options.Policy, options.MaxIncludeDepth, options.CacheParsedTemplates);
    }

    /// <summary>
    /// Renders a named template and returns the HTML.
    /// </summary>
    /// <param name="templateName">The logical template name.</param>
    /// <param name="context">The data for this rendering operation.</param>
    public string Render(string templateName, TemplateContext context) {
      using (StringWriter writer = new StringWriter(CultureInfo.InvariantCulture)) {
        this.RenderTo(templateName, context, writer);
        return writer.ToString();
      }
    }

    /// <summary>
    /// Renders a named template directly to a writer (streaming).
    /// </summary>
    /// <param name="templateName">The logical template name.</param>
    /// <param name="context">The data for this rendering operation.</param>
    /// <param name="writer">The target writer.</param>
    public void RenderTo(string templateName, TemplateContext context, TextWriter writer) {
      if (templateName == null) {
        throw new ArgumentNullException(nameof(templateName));
      }
      if (context == null) {
        throw new ArgumentNullException(nameof(context));
      }
      if (writer == null) {
        throw new ArgumentNullException(nameof(writer));
      }
      TemplateDocument document = this.LoadRequired(templateName);
      CultureInfo culture = context.Culture;
      if (culture == null) {
        culture = _Runtime.Culture;
      }
      EvaluationContext evaluationContext = new EvaluationContext(_Runtime, document, context.CreateRootScope(templateName), culture);
      TemplateNode.RenderAll(document.Nodes, writer, evaluationContext);
    }

    /// <summary>
    /// Parses a named template without rendering it, reporting syntax errors as <see cref="TemplateException"/>.
    /// </summary>
    /// <param name="templateName">The logical template name.</param>
    public void Validate(string templateName) {
      if (templateName == null) {
        throw new ArgumentNullException(nameof(templateName));
      }
      this.LoadRequired(templateName);
    }

    /// <summary>
    /// Removes all cached parsed templates.
    /// </summary>
    public void ClearCache() {
      _Runtime.ClearCache();
    }

    /// <summary>
    /// Loads a template or throws when the source does not provide it.
    /// </summary>
    private TemplateDocument LoadRequired(string templateName) {
      TemplateDocument document = _Runtime.LoadDocument(templateName);
      if (document == null) {
        throw new FileNotFoundException("Template '" + templateName + "' was not found.", templateName);
      }
      return document;
    }

    /// <summary>
    /// Creates options for the specified culture.
    /// </summary>
    private static TemplateEngineOptions CreateOptions(CultureInfo culture) {
      TemplateEngineOptions options = new TemplateEngineOptions();
      if (culture != null) {
        options.Culture = culture;
      }
      return options;
    }

  }

  /// <summary>
  /// Engine-wide configuration, caches and value semantics shared by all rendering operations.
  /// </summary>
  internal sealed class TemplateRuntime {

    private readonly ITemplateSource _Source;
    private readonly CultureInfo _Culture;
    private readonly MemberAccessPolicy _Policy;
    private readonly int _MaxIncludeDepth;
    private readonly bool _CacheParsedTemplates;
    private readonly MemberCache _Members = new MemberCache();
    private readonly ConcurrentDictionary<string, TemplateDocument> _Documents = new ConcurrentDictionary<string, TemplateDocument>(StringComparer.Ordinal);

    /// <summary>
    /// Creates a runtime.
    /// </summary>
    public TemplateRuntime(ITemplateSource source, CultureInfo culture, MemberAccessPolicy policy, int maxIncludeDepth, bool cacheParsedTemplates) {
      _Source = source;
      _Culture = culture;
      _Policy = policy;
      _MaxIncludeDepth = maxIncludeDepth;
      _CacheParsedTemplates = cacheParsedTemplates;
    }

    /// <summary>
    /// The default formatting culture.
    /// </summary>
    public CultureInfo Culture {
      get {
        return _Culture;
      }
    }

    /// <summary>
    /// The member access policy.
    /// </summary>
    public MemberAccessPolicy Policy {
      get {
        return _Policy;
      }
    }

    /// <summary>
    /// The maximum include depth.
    /// </summary>
    public int MaxIncludeDepth {
      get {
        return _MaxIncludeDepth;
      }
    }

    /// <summary>
    /// The member lookup cache.
    /// </summary>
    public MemberCache Members {
      get {
        return _Members;
      }
    }

    /// <summary>
    /// Loads and parses a template (cached), or returns null when the source does not provide it.
    /// </summary>
    public TemplateDocument LoadDocument(string templateName) {
      TemplateDocument document;
      if (_CacheParsedTemplates && _Documents.TryGetValue(templateName, out document)) {
        return document;
      }
      string templateText;
      if (!_Source.TryRead(templateName, out templateText)) {
        return null;
      }
      document = TemplateParser.Parse(templateName, templateText);
      if (_CacheParsedTemplates) {
        document = _Documents.GetOrAdd(templateName, document);
      }
      return document;
    }

    /// <summary>
    /// Removes all cached parsed templates.
    /// </summary>
    public void ClearCache() {
      _Documents.Clear();
    }

    /// <summary>
    /// Template truthiness: null, false, numeric zero, empty strings and empty collections are false.
    /// </summary>
    public bool IsTrue(object value) {
      if (value == null) {
        return false;
      }
      if (value is bool) {
        return (bool)value;
      }
      string text = value as string;
      if (text != null) {
        return text.Length > 0;
      }
      if (ValueOperations.IsNumeric(value)) {
        return Convert.ToDouble(value, CultureInfo.InvariantCulture) != 0d;
      }
      ICollection collection = value as ICollection;
      if (collection != null) {
        return collection.Count > 0;
      }
      IEnumerable enumerable = value as IEnumerable;
      if (enumerable != null) {
        IEnumerator enumerator = enumerable.GetEnumerator();
        try {
          return enumerator.MoveNext();
        } finally {
          IDisposable disposable = enumerator as IDisposable;
          if (disposable != null) {
            disposable.Dispose();
          }
        }
      }
      return true;
    }

    /// <summary>
    /// Formats a value using an optional .NET format string and the culture.
    /// Booleans are written as 'true' / 'false' unless a format is given.
    /// </summary>
    public string Format(object value, string format, CultureInfo culture) {
      if (value == null) {
        return string.Empty;
      }
      if (format != null) {
        IFormattable formattable = value as IFormattable;
        if (formattable != null) {
          return formattable.ToString(format, culture);
        }
        return string.Format(culture, "{0:" + format + "}", value);
      }
      if (value is bool) {
        if ((bool)value) {
          return "true";
        }
        return "false";
      }
      IFormattable defaultFormattable = value as IFormattable;
      if (defaultFormattable != null) {
        return defaultFormattable.ToString(null, culture);
      }
      string result = value.ToString();
      if (result == null) {
        return string.Empty;
      }
      return result;
    }

  }

}

