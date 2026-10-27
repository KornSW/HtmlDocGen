using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace HtmlDocGen {

  /// <summary>
  /// Behavioral tests of the template engine: interpolation, expressions, directives, includes, slots, policies and diagnostics.
  /// </summary>
  [TestClass]
  public class TemplateEngineTests {

    /// <summary>
    /// Renders the template 'Main' from the specified templates with the specified context.
    /// </summary>
    private static string Render(TemplateContext context, params string[] nameTextPairs) {
      return TemplateEngineTests.Render(new TemplateEngineOptions(), context, nameTextPairs);
    }

    /// <summary>
    /// Renders the template 'Main' with explicit engine options.
    /// </summary>
    private static string Render(TemplateEngineOptions options, TemplateContext context, params string[] nameTextPairs) {
      Dictionary<string, string> templates = new Dictionary<string, string>();
      for (int index = 0; index < nameTextPairs.Length; index += 2) {
        templates.Add(nameTextPairs[index], nameTextPairs[index + 1]);
      }
      TemplateEngine engine = new TemplateEngine(new MemoryTemplateSource(templates), options);
      return engine.Render("Main", context);
    }

    /// <summary>
    /// Creates a context with a sample repository as '_Repo'.
    /// </summary>
    private static TemplateContext CreateContext() {
      TemplateContext context = new TemplateContext();
      context.AddSource("_Repo", new SampleRepository());
      return context;
    }

    /// <summary>
    /// Plain HTML passes through unchanged, including comments and doctype.
    /// </summary>
    [TestMethod]
    public void Render_PlainHtml_IsUnchanged() {
      string template = "<!DOCTYPE html>\n<html><head><meta charset=\"utf-8\"></head><body><!-- c --><p class=a>Hi<br>there</p><img src='x.png' /></body></html>";
      string result = TemplateEngineTests.Render(new TemplateContext(), "Main", template);
      Assert.AreEqual(template, result);
    }

    /// <summary>
    /// Interpolated values are HTML encoded by default.
    /// </summary>
    [TestMethod]
    public void Render_Interpolation_EncodesHtml() {
      TemplateContext context = TemplateEngineTests.CreateContext();
      string result = TemplateEngineTests.Render(context, "Main", "<p>{{ _Repo.Title }}</p>");
      Assert.AreEqual("<p>Tom &amp; Jerry &lt;3</p>", result);
    }

    /// <summary>
    /// 'raw' and TrustedHtml values are written without encoding.
    /// </summary>
    [TestMethod]
    public void Render_RawAndTrustedHtml_AreNotEncoded() {
      TemplateContext context = TemplateEngineTests.CreateContext();
      context.AddLocal("fragment", new TrustedHtml("<b>x</b>"));
      string result = TemplateEngineTests.Render(context, "Main", "{{ raw _Repo.Title }}|{{ fragment }}");
      Assert.AreEqual("Tom & Jerry <3|<b>x</b>", result);
    }

    /// <summary>
    /// Format strings use the engine culture or the context culture.
    /// </summary>
    [TestMethod]
    public void Render_FormatString_UsesCulture() {
      TemplateContext context = TemplateEngineTests.CreateContext();
      context.Culture = new CultureInfo("de-DE");
      string result = TemplateEngineTests.Render(context, "Main", "{{ _Repo.Price:N2 }} {{ _Repo.Date:yyyy-MM-dd }}");
      Assert.AreEqual("1.234,50 2024-03-01", result);
    }

    /// <summary>
    /// Method calls with nested arguments, string literals and chained members are evaluated.
    /// </summary>
    [TestMethod]
    public void Render_MethodCalls_AreResolved() {
      TemplateContext context = TemplateEngineTests.CreateContext();
      string result = TemplateEngineTests.Render(context, "Main", "{{ _Repo.Greet(\"Ann\") }}|{{ _Repo.Add(_Repo.Add(1, 2), 3) }}|{{ _Repo.GetItem(1).Name.ToUpperInvariant() }}");
      Assert.AreEqual("Hello Ann|6|BETA", result);
    }

    /// <summary>
    /// Overload resolution prefers exact matches and applies optional parameters.
    /// </summary>
    [TestMethod]
    public void Render_Overloads_AreResolvedDeterministically() {
      TemplateContext context = TemplateEngineTests.CreateContext();
      string result = TemplateEngineTests.Render(context, "Main", "{{ _Repo.Describe(1) }}|{{ _Repo.Describe('a') }}|{{ _Repo.Describe(1.5) }}|{{ _Repo.Pad('x') }}");
      Assert.AreEqual("int|string|decimal|x--", result);
    }

    /// <summary>
    /// Index access works for lists and dictionaries; member access on dictionaries uses keys.
    /// </summary>
    [TestMethod]
    public void Render_IndexAccess_Works() {
      TemplateContext context = TemplateEngineTests.CreateContext();
      string result = TemplateEngineTests.Render(context, "Main", "{{ _Repo.Items[2].Name }}|{{ _Repo.Settings['mode'] }}|{{ _Repo.Settings.mode }}");
      Assert.AreEqual("Gamma|dark|dark", result);
    }

    /// <summary>
    /// Navigation through null yields an empty string instead of an exception.
    /// </summary>
    [TestMethod]
    public void Render_NullNavigation_YieldsEmpty() {
      TemplateContext context = TemplateEngineTests.CreateContext();
      string result = TemplateEngineTests.Render(context, "Main", "[{{ _Repo.Nothing.Name }}]");
      Assert.AreEqual("[]", result);
    }

    /// <summary>
    /// u-for repeats arbitrary elements and exposes an optional index.
    /// </summary>
    [TestMethod]
    public void Render_ForAttribute_RepeatsElement() {
      TemplateContext context = TemplateEngineTests.CreateContext();
      string result = TemplateEngineTests.Render(context, "Main", "<ul><li u-for=\"item, i in _Repo.Items\" data-id=\"{{ item.Id }}\">{{ i }}:{{ item.Name }}</li></ul>");
      Assert.AreEqual("<ul><li data-id=\"1\">0:Alpha</li><li data-id=\"2\">1:Beta</li><li data-id=\"3\">2:Gamma</li></ul>", result);
    }

    /// <summary>
    /// Nested iterations see the locals of outer iterations.
    /// </summary>
    [TestMethod]
    public void Render_NestedFor_InheritsOuterLocals() {
      TemplateContext context = TemplateEngineTests.CreateContext();
      string result = TemplateEngineTests.Render(context, "Main", "<u-block u-for=\"item in _Repo.Items\"><u-block u-for=\"tag in item.Tags\">{{ item.Name }}/{{ tag }};</u-block></u-block>");
      Assert.AreEqual("Alpha/a1;Alpha/a2;Gamma/g1;", result);
    }

    /// <summary>
    /// u-if / u-else-if / u-else chains with comparisons and Boolean operators.
    /// </summary>
    [TestMethod]
    public void Render_ConditionalChain_SelectsBranch() {
      TemplateContext context = TemplateEngineTests.CreateContext();
      string template = "<u-block u-for=\"item in _Repo.Items\"><b u-if=\"item.Id == 1 && item.Name != 'x'\">one</b>\n<i u-else-if=\"item.Id >= 3 or item.Name == 'Zeta'\">big</i>\n<s u-else>other</s></u-block>";
      string result = TemplateEngineTests.Render(context, "Main", template);
      Assert.AreEqual("<b>one</b><s>other</s><i>big</i>", result);
    }

    /// <summary>
    /// Truthiness: null, false, zero, empty strings and empty collections are false.
    /// </summary>
    [TestMethod]
    public void Render_Truthiness_FollowsRules() {
      TemplateContext context = TemplateEngineTests.CreateContext();
      context.AddLocal("emptyList", new List<int>());
      context.AddLocal("zero", 0);
      context.AddLocal("empty", "");
      string template = "<u-block u-if=\"emptyList\">A</u-block><u-block u-if=\"zero\">B</u-block><u-block u-if=\"empty\">C</u-block><u-block u-if=\"!_Repo.Nothing\">D</u-block><u-block u-if=\"_Repo.Items\">E</u-block>";
      string result = TemplateEngineTests.Render(context, "Main", template);
      Assert.AreEqual("DE", result);
    }

    /// <summary>
    /// Includes inherit sources and receive only explicitly passed parameters.
    /// </summary>
    [TestMethod]
    public void Render_Include_PassesParametersAndSources() {
      TemplateContext context = TemplateEngineTests.CreateContext();
      string result = TemplateEngineTests.Render(context,
        "Main", "<u-include u-for=\"item in _Repo.Items\" template=\"Row\" row=\"item\" label=\"'#'\"></u-include>",
        "Row", "<tr><td>{{ label }}{{ row.Id }}</td><td>{{ _Repo.Greet(row.Name) }}</td></tr>");
      Assert.AreEqual("<tr><td>#1</td><td>Hello Alpha</td></tr><tr><td>#2</td><td>Hello Beta</td></tr><tr><td>#3</td><td>Hello Gamma</td></tr>", result);
    }

    /// <summary>
    /// Locals of the includer are not visible in the included template.
    /// </summary>
    [TestMethod]
    public void Render_Include_DoesNotLeakLocals() {
      TemplateContext context = TemplateEngineTests.CreateContext();
      context.AddLocal("secret", "x");
      TemplateException exception = Assert.ThrowsExactly<TemplateException>(() => {
        TemplateEngineTests.Render(context, "Main", "<u-include template=\"Part\"></u-include>", "Part", "\n  {{ secret }}");
      });
      Assert.AreEqual("Part", exception.TemplateName);
      Assert.AreEqual(2, exception.Line);
      Assert.AreEqual(6, exception.Column);
    }

    /// <summary>
    /// Layouts receive default and named slot content rendered in the caller context; unfilled slots use defaults.
    /// </summary>
    [TestMethod]
    public void Render_LayoutWithSlots_ComposesPage() {
      TemplateContext context = TemplateEngineTests.CreateContext();
      context.AddLocal("pageTitle", "Start");
      string result = TemplateEngineTests.Render(context,
        "Main", "<u-include template=\"Layout\" title=\"pageTitle\"><u-fill slot=\"head\"><meta name=\"t\" content=\"{{ pageTitle }}\"></u-fill><p>{{ pageTitle }} body</p></u-include>",
        "Layout", "<html><head><title>{{ title }}</title><u-slot name=\"head\"></u-slot></head><body><u-slot></u-slot><footer><u-slot name=\"footer\">default</u-slot></footer></body></html>");
      Assert.AreEqual("<html><head><title>Start</title><meta name=\"t\" content=\"Start\"></head><body><p>Start body</p><footer>default</footer></body></html>", result);
    }

    /// <summary>
    /// Cyclic includes are reported with the include chain.
    /// </summary>
    [TestMethod]
    public void Render_CyclicInclude_IsReported() {
      TemplateException exception = Assert.ThrowsExactly<TemplateException>(() => {
        TemplateEngineTests.Render(new TemplateContext(), "Main", "<u-include template=\"A\"></u-include>", "A", "<u-include template=\"Main\"></u-include>");
      });
      StringAssert.Contains(exception.Message, "Main -> A -> Main");
    }

    /// <summary>
    /// Syntax errors carry template name, line and column.
    /// </summary>
    [TestMethod]
    public void Render_SyntaxErrors_AreLocated() {
      TemplateException unclosed = Assert.ThrowsExactly<TemplateException>(() => {
        TemplateEngineTests.Render(new TemplateContext(), "Main", "<div>\n<span>{{ 1 }}</div>");
      });
      Assert.AreEqual(2, unclosed.Line);
      TemplateException expression = Assert.ThrowsExactly<TemplateException>(() => {
        TemplateEngineTests.Render(new TemplateContext(), "Main", "<p>{{ a == }}</p>");
      });
      Assert.AreEqual(1, expression.Line);
      Assert.AreEqual(12, expression.Column);
      Assert.ThrowsExactly<TemplateException>(() => {
        TemplateEngineTests.Render(new TemplateContext(), "Main", "<p u-else>x</p>");
      });
      Assert.ThrowsExactly<TemplateException>(() => {
        TemplateEngineTests.Render(new TemplateContext(), "Main", "<p u-foo=\"1\">x</p>");
      });
    }

    /// <summary>
    /// Unknown sources produce a helpful diagnostic.
    /// </summary>
    [TestMethod]
    public void Render_UnknownSource_IsReported() {
      TemplateException exception = Assert.ThrowsExactly<TemplateException>(() => {
        TemplateEngineTests.Render(new TemplateContext(), "Main", "{{ _Missing.X }}");
      });
      StringAssert.Contains(exception.Message, "_Missing");
    }

    /// <summary>
    /// Script and style content is not parsed as markup, but interpolations still work.
    /// </summary>
    [TestMethod]
    public void Render_ScriptContent_IsRawText() {
      TemplateContext context = TemplateEngineTests.CreateContext();
      string result = TemplateEngineTests.Render(context, "Main", "<script>if (a < b && c) { x(\"</div>\"); } var n = {{ _Repo.Add(1, 1) }};</script>");
      Assert.AreEqual("<script>if (a < b && c) { x(\"</div>\"); } var n = 2;</script>", result);
    }

    /// <summary>
    /// The restricted policy only allows attributed or registered methods; reflection is always denied.
    /// </summary>
    [TestMethod]
    public void Render_RestrictedPolicy_BlocksMethods() {
      RestrictedMemberAccessPolicy policy = new RestrictedMemberAccessPolicy();
      TemplateEngineOptions options = new TemplateEngineOptions();
      options.Policy = policy;
      TemplateContext context = TemplateEngineTests.CreateContext();
      Assert.AreEqual("Hello X", TemplateEngineTests.Render(options, context, "Main", "{{ _Repo.Greet('X') }}"));
      Assert.ThrowsExactly<TemplateException>(() => {
        TemplateEngineTests.Render(options, context, "Main", "{{ _Repo.Add(1, 2) }}");
      });
      policy.AllowMethod(typeof(SampleRepository), "Add");
      Assert.AreEqual("3", TemplateEngineTests.Render(options, context, "Main", "{{ _Repo.Add(1, 2) }}"));
      TemplateException reflection = Assert.ThrowsExactly<TemplateException>(() => {
        TemplateEngineTests.Render(TemplateEngineTests.CreateContext(), "Main", "{{ _Repo.GetType().Assembly }}");
      });
      StringAssert.Contains(reflection.Message, "GetType");
    }

    /// <summary>
    /// Lazy sources are only evaluated when referenced, and at most once.
    /// </summary>
    [TestMethod]
    public void Render_LazySource_IsEvaluatedOnDemand() {
      int calls = 0;
      TemplateContext context = new TemplateContext();
      context.AddLazySource("_Lazy", () => {
        calls++;
        return "v";
      });
      context.AddLazySource("_Unused", () => {
        throw new InvalidOperationException("must not be called");
      });
      string result = TemplateEngineTests.Render(context, "Main", "{{ _Lazy }}{{ _Lazy }}");
      Assert.AreEqual("vv", result);
      Assert.AreEqual(1, calls);
    }

    /// <summary>
    /// Composite sources reject duplicate template names.
    /// </summary>
    [TestMethod]
    public void CompositeSource_DuplicateNames_AreRejected() {
      Dictionary<string, string> first = new Dictionary<string, string>();
      first.Add("A", "1");
      Dictionary<string, string> second = new Dictionary<string, string>();
      second.Add("A", "2");
      Assert.ThrowsExactly<InvalidOperationException>(() => {
        new CompositeTemplateSource(new ITemplateSource[] { new MemoryTemplateSource(first), new MemoryTemplateSource(second) });
      });
    }

    /// <summary>
    /// The file source rejects paths escaping the root and resolves default extensions.
    /// </summary>
    [TestMethod]
    public void FileSource_ResolvesAndProtectsRoot() {
      string root = Path.Combine(Path.GetTempPath(), "HtmlDocGenTests_" + Guid.NewGuid().ToString("N"));
      Directory.CreateDirectory(Path.Combine(root, "Pages"));
      try {
        File.WriteAllText(Path.Combine(root, "Pages", "Home.html"), "<p>{{ 1 }}</p>");
        TemplateEngine engine = new TemplateEngine(new FileTemplateSource(root, ".html"));
        Assert.AreEqual("<p>1</p>", engine.Render("Pages/Home", new TemplateContext()));
        Assert.ThrowsExactly<InvalidOperationException>(() => {
          engine.Render("../outside", new TemplateContext());
        });
      } finally {
        Directory.Delete(root, true);
      }
    }

    /// <summary>
    /// Source and local names are validated.
    /// </summary>
    [TestMethod]
    public void Context_InvalidNames_AreRejected() {
      TemplateContext context = new TemplateContext();
      Assert.ThrowsExactly<ArgumentException>(() => {
        context.AddSource("Repo", 1);
      });
      Assert.ThrowsExactly<ArgumentException>(() => {
        context.AddLocal("_x", 1);
      });
    }

    /// <summary>
    /// Sample data source used by the tests.
    /// </summary>
    public sealed class SampleRepository {

      private readonly List<SampleItem> _Items = new List<SampleItem>();
      private readonly Dictionary<string, string> _Settings = new Dictionary<string, string>();

      /// <summary>
      /// Creates the sample data.
      /// </summary>
      public SampleRepository() {
        _Items.Add(new SampleItem(1, "Alpha", new string[] { "a1", "a2" }));
        _Items.Add(new SampleItem(2, "Beta", new string[0]));
        _Items.Add(new SampleItem(3, "Gamma", new string[] { "g1" }));
        _Settings.Add("mode", "dark");
      }

      /// <summary>
      /// A title containing HTML special characters.
      /// </summary>
      public string Title {
        get {
          return "Tom & Jerry <3";
        }
      }

      /// <summary>
      /// A decimal price.
      /// </summary>
      public decimal Price {
        get {
          return 1234.5m;
        }
      }

      /// <summary>
      /// A date.
      /// </summary>
      public DateTime Date {
        get {
          return new DateTime(2024, 3, 1);
        }
      }

      /// <summary>
      /// Always null.
      /// </summary>
      public SampleItem Nothing {
        get {
          return null;
        }
      }

      /// <summary>
      /// The sample items.
      /// </summary>
      public List<SampleItem> Items {
        get {
          return _Items;
        }
      }

      /// <summary>
      /// Sample settings.
      /// </summary>
      public Dictionary<string, string> Settings {
        get {
          return _Settings;
        }
      }

      /// <summary>
      /// Returns a greeting; callable under the restricted policy.
      /// </summary>
      [TemplateCallable]
      public string Greet(string name) {
        return "Hello " + name;
      }

      /// <summary>
      /// Adds two numbers.
      /// </summary>
      public int Add(int left, int right) {
        return left + right;
      }

      /// <summary>
      /// Returns an item by index.
      /// </summary>
      public SampleItem GetItem(int index) {
        return _Items[index];
      }

      /// <summary>
      /// Overload for Int32.
      /// </summary>
      public string Describe(int value) {
        return "int";
      }

      /// <summary>
      /// Overload for String.
      /// </summary>
      public string Describe(string value) {
        return "string";
      }

      /// <summary>
      /// Overload for Decimal.
      /// </summary>
      public string Describe(decimal value) {
        return "decimal";
      }

      /// <summary>
      /// Method with an optional parameter.
      /// </summary>
      public string Pad(string value, string suffix = "--") {
        return value + suffix;
      }

    }

    /// <summary>
    /// Sample item used by the tests.
    /// </summary>
    public sealed class SampleItem {

      private readonly int _Id;
      private readonly string _Name;
      private readonly string[] _Tags;

      /// <summary>
      /// Creates an item.
      /// </summary>
      public SampleItem(int id, string name, string[] tags) {
        _Id = id;
        _Name = name;
        _Tags = tags;
      }

      /// <summary>
      /// The identifier.
      /// </summary>
      public int Id {
        get {
          return _Id;
        }
      }

      /// <summary>
      /// The name.
      /// </summary>
      public string Name {
        get {
          return _Name;
        }
      }

      /// <summary>
      /// The tags.
      /// </summary>
      public string[] Tags {
        get {
          return _Tags;
        }
      }

    }

  }

}
