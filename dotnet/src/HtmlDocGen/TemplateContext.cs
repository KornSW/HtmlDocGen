using System;
using System.Collections.Generic;
using System.Globalization;

namespace HtmlDocGen {

  // v 2.0.0

  /// <summary>
  /// Holds the data for one rendering operation: explicitly named root data sources
  /// (names starting with an underscore, inherited by every include) and explicit
  /// top-level local variables (visible only in the rendered top-level template).
  /// </summary>
  public sealed class TemplateContext {

    private readonly Dictionary<string, TemplateDataSource> _Sources = new Dictionary<string, TemplateDataSource>(StringComparer.Ordinal);
    private readonly Dictionary<string, object> _Locals = new Dictionary<string, object>(StringComparer.Ordinal);
    private CultureInfo _Culture;

    /// <summary>
    /// Optional culture overriding the engine culture for this rendering operation (null = engine culture).
    /// </summary>
    public CultureInfo Culture {
      get {
        return _Culture;
      }
      set {
        _Culture = value;
      }
    }

    /// <summary>
    /// Registers a root data source. Source names must start with an underscore, for example '_Repository'.
    /// </summary>
    /// <param name="name">The source name including the leading underscore.</param>
    /// <param name="value">The source object (may be null).</param>
    public void AddSource(string name, object value) {
      TemplateContext.ValidateSourceName(name);
      _Sources[name] = new TemplateDataSource(value);
    }

    /// <summary>
    /// Registers a lazily evaluated root data source. The factory is invoked at most once,
    /// on first use, and only when a template actually references the source.
    /// </summary>
    /// <param name="name">The source name including the leading underscore.</param>
    /// <param name="factory">The factory creating the source object.</param>
    public void AddLazySource(string name, Func<object> factory) {
      TemplateContext.ValidateSourceName(name);
      if (factory == null) {
        throw new ArgumentNullException(nameof(factory));
      }
      _Sources[name] = new TemplateDataSource(factory);
    }

    /// <summary>
    /// Registers a local variable for the top-level template. Locals are NOT inherited by includes;
    /// pass them explicitly as include parameters.
    /// </summary>
    /// <param name="name">The local name; must not start with an underscore.</param>
    /// <param name="value">The value (may be null).</param>
    public void AddLocal(string name, object value) {
      if (!TemplateContext.IsIdentifier(name) || name.StartsWith("_", StringComparison.Ordinal)) {
        throw new ArgumentException("Local names must be identifiers and must not start with an underscore: '" + name + "'.", nameof(name));
      }
      _Locals[name] = value;
    }

    /// <summary>
    /// Returns true when a source with the specified name is registered.
    /// </summary>
    public bool HasSource(string name) {
      return _Sources.ContainsKey(name);
    }

    /// <summary>
    /// Creates the root render scope for this context.
    /// </summary>
    internal RenderScope CreateRootScope(string templateName) {
      Dictionary<string, TemplateDataSource> sources = new Dictionary<string, TemplateDataSource>(_Sources, StringComparer.Ordinal);
      RenderScope scope = RenderScope.CreateRoot(sources, templateName);
      foreach (KeyValuePair<string, object> local in _Locals) {
        scope.SetLocal(local.Key, local.Value);
      }
      return scope;
    }

    /// <summary>
    /// Ensures that a source name starts with an underscore and is a valid identifier.
    /// </summary>
    private static void ValidateSourceName(string name) {
      if (!TemplateContext.IsIdentifier(name) || !name.StartsWith("_", StringComparison.Ordinal) || name.Length < 2) {
        throw new ArgumentException("Source names must be identifiers starting with an underscore: '" + name + "'.", nameof(name));
      }
    }

    /// <summary>
    /// Returns true when the specified text is a valid template identifier.
    /// </summary>
    internal static bool IsIdentifier(string text) {
      if (string.IsNullOrEmpty(text)) {
        return false;
      }
      if (!char.IsLetter(text[0]) && text[0] != '_') {
        return false;
      }
      for (int index = 1; index < text.Length; index++) {
        if (!char.IsLetterOrDigit(text[index]) && text[index] != '_') {
          return false;
        }
      }
      return true;
    }

  }

  /// <summary>
  /// Wraps HTML markup that is explicitly trusted and therefore written without encoding.
  /// Only wrap markup from trusted origins; wrapping user input creates XSS vulnerabilities.
  /// </summary>
  public sealed class TrustedHtml {

    private readonly string _Html;

    /// <summary>
    /// Creates a trusted HTML fragment.
    /// </summary>
    /// <param name="html">The trusted markup.</param>
    public TrustedHtml(string html) {
      if (html == null) {
        throw new ArgumentNullException(nameof(html));
      }
      _Html = html;
    }

    /// <summary>
    /// The trusted markup.
    /// </summary>
    public string Html {
      get {
        return _Html;
      }
    }

    /// <summary>
    /// Returns the trusted markup.
    /// </summary>
    public override string ToString() {
      return _Html;
    }

  }

  /// <summary>
  /// A registered root data source, holding either a value or a lazy factory.
  /// </summary>
  internal sealed class TemplateDataSource {

    private readonly object _SyncRoot = new object();
    private Func<object> _Factory;
    private object _Value;

    /// <summary>
    /// Creates a source with an eagerly provided value.
    /// </summary>
    public TemplateDataSource(object value) {
      _Value = value;
    }

    /// <summary>
    /// Creates a source with a lazy factory.
    /// </summary>
    public TemplateDataSource(Func<object> factory) {
      _Factory = factory;
    }

    /// <summary>
    /// Returns the value, invoking the factory once if necessary.
    /// </summary>
    public object GetValue() {
      lock (_SyncRoot) {
        if (_Factory != null) {
          _Value = _Factory.Invoke();
          _Factory = null;
        }
        return _Value;
      }
    }

  }

  /// <summary>
  /// Runtime variable scope. Sources are shared by all scopes of a rendering operation.
  /// Nested scopes (iterations, conditions) see the locals of their lexical parent;
  /// isolated scopes (includes) see only sources and their explicit parameters.
  /// </summary>
  internal sealed class RenderScope {

    private readonly Dictionary<string, TemplateDataSource> _Sources;
    private readonly Dictionary<string, object> _Locals = new Dictionary<string, object>(StringComparer.Ordinal);
    private readonly RenderScope _Parent;
    private readonly string _TemplateName;
    private readonly Dictionary<string, SlotContent> _Slots;
    private readonly int _IncludeDepth;
    private readonly RenderScope _Includer;

    /// <summary>
    /// Creates a scope.
    /// </summary>
    private RenderScope(Dictionary<string, TemplateDataSource> sources, RenderScope parent, string templateName, Dictionary<string, SlotContent> slots, int includeDepth, RenderScope includer) {
      _Sources = sources;
      _Parent = parent;
      _TemplateName = templateName;
      _Slots = slots;
      _IncludeDepth = includeDepth;
      _Includer = includer;
    }

    /// <summary>
    /// Creates the root scope of a rendering operation.
    /// </summary>
    public static RenderScope CreateRoot(Dictionary<string, TemplateDataSource> sources, string templateName) {
      return new RenderScope(sources, null, templateName, new Dictionary<string, SlotContent>(StringComparer.Ordinal), 0, null);
    }

    /// <summary>
    /// The name of the template this scope belongs to.
    /// </summary>
    public string TemplateName {
      get {
        return _TemplateName;
      }
    }

    /// <summary>
    /// The number of includes between the root template and this scope.
    /// </summary>
    public int IncludeDepth {
      get {
        return _IncludeDepth;
      }
    }

    /// <summary>
    /// Creates a nested scope inheriting all lexical locals (used for iterations).
    /// </summary>
    public RenderScope CreateNested() {
      return new RenderScope(_Sources, this, _TemplateName, _Slots, _IncludeDepth, _Includer);
    }

    /// <summary>
    /// Creates an isolated scope for an included template: sources are inherited, locals are not.
    /// </summary>
    public RenderScope CreateInclude(string templateName, Dictionary<string, SlotContent> slots) {
      return new RenderScope(_Sources, null, templateName, slots, _IncludeDepth + 1, this);
    }

    /// <summary>
    /// Sets a local variable in this scope.
    /// </summary>
    public void SetLocal(string name, object value) {
      _Locals[name] = value;
    }

    /// <summary>
    /// Resolves a root identifier: underscore-prefixed names resolve to sources, other names to lexical locals.
    /// </summary>
    public bool TryResolve(string name, out object value) {
      if (name.StartsWith("_", StringComparison.Ordinal)) {
        TemplateDataSource source;
        if (_Sources.TryGetValue(name, out source)) {
          value = source.GetValue();
          return true;
        }
        value = null;
        return false;
      }
      RenderScope current = this;
      while (current != null) {
        if (current._Locals.TryGetValue(name, out value)) {
          return true;
        }
        current = current._Parent;
      }
      value = null;
      return false;
    }

    /// <summary>
    /// Tries to get the content passed for a named slot of the current template.
    /// </summary>
    public bool TryGetSlot(string slotName, out SlotContent content) {
      return _Slots.TryGetValue(slotName, out content);
    }

    /// <summary>
    /// Returns the chain of template names from the root template to this scope (for diagnostics).
    /// </summary>
    public string DescribeIncludeChain() {
      List<string> names = new List<string>();
      RenderScope current = this;
      while (current != null) {
        names.Insert(0, current._TemplateName);
        current = current._Includer;
      }
      return string.Join(" -> ", names.ToArray());
    }

  }

}
