using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace HtmlDocGen {

  /// <summary>
  /// Edge cases and API contracts of the template engine that complement <see cref="TemplateEngineTests"/>.
  /// </summary>
  [TestClass]
  public class TemplateEngineEdgeCaseTests {

    /// <summary>
    /// Creates an engine for the specified name/text pairs.
    /// </summary>
    private static TemplateEngine CreateEngine(TemplateEngineOptions options, params string[] nameTextPairs) {
      Dictionary<string, string> templates = new Dictionary<string, string>();
      for (int index = 0; index < nameTextPairs.Length; index += 2) {
        templates.Add(nameTextPairs[index], nameTextPairs[index + 1]);
      }
      return new TemplateEngine(new MemoryTemplateSource(templates), options);
    }

    /// <summary>
    /// Renders the template 'Main' with default options.
    /// </summary>
    private static string Render(TemplateContext context, params string[] nameTextPairs) {
      return TemplateEngineEdgeCaseTests.CreateEngine(new TemplateEngineOptions(), nameTextPairs).Render("Main", context);
    }

    /// <summary>
    /// Creates a context with a sample repository as '_Repo'.
    /// </summary>
    private static TemplateContext CreateContext() {
      TemplateContext context = new TemplateContext();
      context.AddSource("_Repo", new TemplateEngineTests.SampleRepository());
      return context;
    }

    /// <summary>
    /// Literals, negative numbers, null and Boolean values are formatted as expected.
    /// </summary>
    [TestMethod]
    public void Render_Literals_AreFormatted() {
      string result = TemplateEngineEdgeCaseTests.Render(new TemplateContext(), "Main", "{{ 'a' }}|{{ -5 }}|{{ 1.25 }}|[{{ null }}]|{{ true }}|{{ 1 < 2 }}");
      Assert.AreEqual("a|-5|1.25|[]|true|true", result);
    }

    /// <summary>
    /// Comparison operators compare numbers by value across numeric types.
    /// </summary>
    [TestMethod]
    public void Render_NumericComparisons_CrossTypes() {
      TemplateContext context = TemplateEngineEdgeCaseTests.CreateContext();
      string result = TemplateEngineEdgeCaseTests.Render(context, "Main", "{{ _Repo.Price == 1234.5 }}|{{ _Repo.Add(1, 1) == 2.0 }}|{{ 3 <= 3 }}|{{ 2 > 3 }}|{{ 'x' != 'y' }}");
      Assert.AreEqual("true|true|true|false|true", result);
    }

    /// <summary>
    /// 'not' / '!' negate, and parentheses override operator precedence.
    /// </summary>
    [TestMethod]
    public void Render_BooleanPrecedence_RespectsParentheses() {
      string result = TemplateEngineEdgeCaseTests.Render(new TemplateContext(), "Main", "{{ true or false and false }}|{{ (true or false) and false }}|{{ not true }}|{{ !(1 == 2) }}");
      Assert.AreEqual("true|false|false|true", result);
    }

    /// <summary>
    /// 'and' short-circuits, so the right side is not evaluated when the left side is false.
    /// </summary>
    [TestMethod]
    public void Render_And_ShortCircuits() {
      TemplateContext context = new TemplateContext();
      context.AddLazySource("_Fails", () => {
        throw new InvalidOperationException("must not be evaluated");
      });
      string result = TemplateEngineEdgeCaseTests.Render(context, "Main", "{{ false and _Fails }}|{{ true or _Fails }}");
      Assert.AreEqual("false|true", result);
    }

    /// <summary>
    /// Ordering comparisons of incompatible values are reported as template errors.
    /// </summary>
    [TestMethod]
    public void Render_IncompatibleComparison_IsReported() {
      Assert.ThrowsExactly<TemplateException>(() => {
        TemplateEngineEdgeCaseTests.Render(new TemplateContext(), "Main", "{{ 'a' < 1 }}");
      });
    }

    /// <summary>
    /// Values inside attributes are HTML encoded, including quotes.
    /// </summary>
    [TestMethod]
    public void Render_AttributeValues_AreEncoded() {
      TemplateContext context = new TemplateContext();
      context.AddLocal("value", "a\"b<c");
      string result = TemplateEngineEdgeCaseTests.Render(context, "Main", "<input value=\"{{ value }}\">");
      Assert.AreEqual("<input value=\"a&quot;b&lt;c\">", result);
    }

    /// <summary>
    /// Boolean attributes without a value are written unchanged.
    /// </summary>
    [TestMethod]
    public void Render_BooleanAttributes_AreKept() {
      string template = "<input type=\"checkbox\" checked disabled>";
      string result = TemplateEngineEdgeCaseTests.Render(new TemplateContext(), "Main", template);
      Assert.AreEqual(template, result);
    }

    /// <summary>
    /// Iterating null renders nothing; iterating a string is rejected.
    /// </summary>
    [TestMethod]
    public void Render_ForOverNullOrString_IsHandled() {
      TemplateContext context = TemplateEngineEdgeCaseTests.CreateContext();
      Assert.AreEqual("[]", TemplateEngineEdgeCaseTests.Render(context, "Main", "[<b u-for=\"x in _Repo.Nothing\">{{ x }}</b>]"));
      Assert.ThrowsExactly<TemplateException>(() => {
        TemplateEngineEdgeCaseTests.Render(TemplateEngineEdgeCaseTests.CreateContext(), "Main", "<b u-for=\"c in _Repo.Title\">{{ c }}</b>");
      });
    }

    /// <summary>
    /// Iterating an empty collection renders nothing and may be combined with an else branch.
    /// </summary>
    [TestMethod]
    public void Render_EmptyCollection_UsesElseBranch() {
      TemplateContext context = new TemplateContext();
      context.AddLocal("items", new string[0]);
      string result = TemplateEngineEdgeCaseTests.Render(context, "Main", "<ul u-if=\"items\"><li u-for=\"i in items\">{{ i }}</li></ul><p u-else>none</p>");
      Assert.AreEqual("<p>none</p>", result);
    }

    /// <summary>
    /// Lazily enumerated sequences (iterators) are supported by u-for and truthiness.
    /// </summary>
    [TestMethod]
    public void Render_LazySequence_IsIterated() {
      TemplateContext context = new TemplateContext();
      context.AddLocal("numbers", TemplateEngineEdgeCaseTests.CountTo(3));
      string result = TemplateEngineEdgeCaseTests.Render(context, "Main", "<u-block u-if=\"numbers\"><i u-for=\"n in numbers\">{{ n }}</i></u-block>");
      Assert.AreEqual("<i>1</i><i>2</i><i>3</i>", result);
    }

    /// <summary>
    /// Enum values compare against their names.
    /// </summary>
    [TestMethod]
    public void Render_EnumComparison_UsesNames() {
      TemplateContext context = new TemplateContext();
      context.AddLocal("day", DayOfWeek.Monday);
      string result = TemplateEngineEdgeCaseTests.Render(context, "Main", "{{ day == 'Monday' }}|{{ day }}");
      Assert.AreEqual("true|Monday", result);
    }

    /// <summary>
    /// An invalid format string is reported as template error with the format in the message.
    /// </summary>
    [TestMethod]
    public void Render_InvalidFormat_IsReported() {
      TemplateContext context = new TemplateContext();
      context.AddLocal("n", 5);
      TemplateException exception = Assert.ThrowsExactly<TemplateException>(() => {
        TemplateEngineEdgeCaseTests.Render(context, "Main", "{{ n:Q }}");
      });
      StringAssert.Contains(exception.Message, "format");
    }

    /// <summary>
    /// The engine culture applies when the context does not override it.
    /// </summary>
    [TestMethod]
    public void Render_EngineCulture_IsDefault() {
      TemplateEngineOptions options = new TemplateEngineOptions();
      options.Culture = new CultureInfo("de-DE");
      TemplateEngine engine = TemplateEngineEdgeCaseTests.CreateEngine(options, "Main", "{{ 1.5 }}");
      Assert.AreEqual("1,5", engine.Render("Main", new TemplateContext()));
      TemplateContext context = new TemplateContext();
      context.Culture = CultureInfo.InvariantCulture;
      Assert.AreEqual("1.5", engine.Render("Main", context));
    }

    /// <summary>
    /// Free function calls are rejected with a hint to use data sources.
    /// </summary>
    [TestMethod]
    public void Render_FreeFunction_IsRejected() {
      TemplateException exception = Assert.ThrowsExactly<TemplateException>(() => {
        TemplateEngineEdgeCaseTests.Render(new TemplateContext(), "Main", "{{ format(1) }}");
      });
      StringAssert.Contains(exception.Message, "Free functions");
    }

    /// <summary>
    /// Unknown members are reported.
    /// </summary>
    [TestMethod]
    public void Render_UnknownMember_IsReported() {
      TemplateException exception = Assert.ThrowsExactly<TemplateException>(() => {
        TemplateEngineEdgeCaseTests.Render(TemplateEngineEdgeCaseTests.CreateContext(), "Main", "{{ _Repo.DoesNotExist }}");
      });
      StringAssert.Contains(exception.Message, "DoesNotExist");
    }

    /// <summary>
    /// Unclosed interpolations are syntax errors.
    /// </summary>
    [TestMethod]
    public void Validate_UnclosedInterpolation_IsReported() {
      TemplateEngine engine = TemplateEngineEdgeCaseTests.CreateEngine(new TemplateEngineOptions(), "Main", "<p>{{ 1 </p>");
      Assert.ThrowsExactly<TemplateException>(() => {
        engine.Validate("Main");
      });
    }

    /// <summary>
    /// Validate parses without rendering, so missing data sources are not an error.
    /// </summary>
    [TestMethod]
    public void Validate_DoesNotEvaluate() {
      TemplateEngine engine = TemplateEngineEdgeCaseTests.CreateEngine(new TemplateEngineOptions(), "Main", "<p>{{ _NotRegistered.Name }}</p>");
      engine.Validate("Main");
    }

    /// <summary>
    /// Missing templates throw FileNotFoundException, also for includes.
    /// </summary>
    [TestMethod]
    public void Render_MissingTemplate_IsReported() {
      TemplateEngine engine = TemplateEngineEdgeCaseTests.CreateEngine(new TemplateEngineOptions(), "Main", "<u-include template=\"Nope\"></u-include>");
      Assert.ThrowsExactly<FileNotFoundException>(() => {
        engine.Render("Other", new TemplateContext());
      });
      Exception includeError = Assert.Throws<Exception>(() => {
        engine.Render("Main", new TemplateContext());
      });
      StringAssert.Contains(includeError.Message, "Nope");
    }

    /// <summary>
    /// The maximum include depth limits deep (non-cyclic) include chains.
    /// </summary>
    [TestMethod]
    public void Render_MaxIncludeDepth_IsEnforced() {
      TemplateEngineOptions options = new TemplateEngineOptions();
      options.MaxIncludeDepth = 2;
      TemplateEngine engine = TemplateEngineEdgeCaseTests.CreateEngine(options,
        "Main", "<u-include template=\"A\"></u-include>",
        "A", "<u-include template=\"B\"></u-include>",
        "B", "<u-include template=\"C\"></u-include>",
        "C", "c");
      TemplateException exception = Assert.ThrowsExactly<TemplateException>(() => {
        engine.Render("Main", new TemplateContext());
      });
      StringAssert.Contains(exception.Message, "include depth");
    }

    /// <summary>
    /// Parsed templates are cached until ClearCache is called; caching can be disabled.
    /// </summary>
    [TestMethod]
    public void Engine_Cache_CanBeClearedAndDisabled() {
      Dictionary<string, string> templates = new Dictionary<string, string>();
      templates.Add("Main", "v1");
      MutableTemplateSource source = new MutableTemplateSource(templates);
      TemplateEngine cached = new TemplateEngine(source);
      Assert.AreEqual("v1", cached.Render("Main", new TemplateContext()));
      templates["Main"] = "v2";
      Assert.AreEqual("v1", cached.Render("Main", new TemplateContext()));
      cached.ClearCache();
      Assert.AreEqual("v2", cached.Render("Main", new TemplateContext()));

      TemplateEngineOptions options = new TemplateEngineOptions();
      options.CacheParsedTemplates = false;
      TemplateEngine uncached = new TemplateEngine(source, options);
      Assert.AreEqual("v2", uncached.Render("Main", new TemplateContext()));
      templates["Main"] = "v3";
      Assert.AreEqual("v3", uncached.Render("Main", new TemplateContext()));
    }

    /// <summary>
    /// RenderTo streams into the writer and yields the same output as Render.
    /// </summary>
    [TestMethod]
    public void RenderTo_WritesToWriter() {
      TemplateEngine engine = TemplateEngineEdgeCaseTests.CreateEngine(new TemplateEngineOptions(), "Main", "<p>{{ 1 }}</p>");
      using (StringWriter writer = new StringWriter(CultureInfo.InvariantCulture)) {
        writer.Write("pre:");
        engine.RenderTo("Main", new TemplateContext(), writer);
        Assert.AreEqual("pre:<p>1</p>", writer.ToString());
      }
    }

    /// <summary>
    /// Rendering is safe for concurrent use of one engine instance.
    /// </summary>
    [TestMethod]
    public void Render_Concurrent_IsConsistent() {
      TemplateEngine engine = TemplateEngineEdgeCaseTests.CreateEngine(new TemplateEngineOptions(),
        "Main", "<i u-for=\"n in numbers\">{{ n }}</i><u-include template=\"Part\" value=\"id\"></u-include>",
        "Part", "|{{ value }}");
      string[] results = new string[32];
      System.Threading.Tasks.Parallel.For(0, results.Length, (index) => {
        TemplateContext context = new TemplateContext();
        context.AddLocal("numbers", new int[] { 1, 2 });
        context.AddLocal("id", index);
        results[index] = engine.Render("Main", context);
      });
      for (int index = 0; index < results.Length; index++) {
        Assert.AreEqual("<i>1</i><i>2</i>|" + index.ToString(CultureInfo.InvariantCulture), results[index]);
      }
    }

    /// <summary>
    /// Fills without a matching slot in the included template are ignored.
    /// </summary>
    [TestMethod]
    public void Render_UnusedFill_IsIgnored() {
      string result = TemplateEngineEdgeCaseTests.Render(new TemplateContext(),
        "Main", "<u-include template=\"Layout\"><u-fill slot=\"unused\">x</u-fill>body</u-include>",
        "Layout", "[<u-slot></u-slot>]");
      Assert.AreEqual("[body]", result);
    }

    /// <summary>
    /// Embedded resources are resolved via the resource prefix; '/' maps to '.'.
    /// </summary>
    [TestMethod]
    public void EmbeddedSource_ResolvesResources() {
      EmbeddedTemplateSource source = new EmbeddedTemplateSource(typeof(TemplateEngineEdgeCaseTests).Assembly, "HtmlDocGen.Tests.");
      CollectionAssert.Contains(source.GetTemplateNames(), "Templates.Embedded.html");
      TemplateEngine engine = new TemplateEngine(source);
      TemplateContext context = new TemplateContext();
      context.AddLocal("greeting", "Hi & bye");
      Assert.AreEqual("<p>Hi &amp; bye</p>", engine.Render("Templates/Embedded.html", context).Trim());
      string text;
      Assert.IsFalse(source.TryRead("Templates/Missing.html", out text));
    }

    /// <summary>
    /// Composite sources resolve templates from all inner sources and combine their names.
    /// </summary>
    [TestMethod]
    public void CompositeSource_ResolvesAcrossSources() {
      Dictionary<string, string> first = new Dictionary<string, string>();
      first.Add("Main", "<u-include template=\"Part\"></u-include>!");
      Dictionary<string, string> second = new Dictionary<string, string>();
      second.Add("Part", "part");
      CompositeTemplateSource source = new CompositeTemplateSource(new ITemplateSource[] { new MemoryTemplateSource(first), new MemoryTemplateSource(second) });
      Assert.AreEqual(2, source.GetTemplateNames().Length);
      Assert.AreEqual("part!", new TemplateEngine(source).Render("Main", new TemplateContext()));
    }

    /// <summary>
    /// Constructor and method arguments are validated.
    /// </summary>
    [TestMethod]
    public void Api_NullArguments_AreRejected() {
      Assert.ThrowsExactly<ArgumentNullException>(() => {
        new TemplateEngine(null);
      });
      Assert.ThrowsExactly<ArgumentNullException>(() => {
        new MemoryTemplateSource(null);
      });
      Assert.ThrowsExactly<ArgumentException>(() => {
        new FileTemplateSource(" ");
      });
      TemplateEngine engine = TemplateEngineEdgeCaseTests.CreateEngine(new TemplateEngineOptions(), "Main", "x");
      Assert.ThrowsExactly<ArgumentNullException>(() => {
        engine.Render("Main", null);
      });
      Assert.ThrowsExactly<ArgumentNullException>(() => {
        engine.RenderTo("Main", new TemplateContext(), null);
      });
      TemplateContext context = new TemplateContext();
      Assert.ThrowsExactly<ArgumentNullException>(() => {
        context.AddLazySource("_X", null);
      });
    }

    /// <summary>
    /// Re-registering a source replaces the previous value.
    /// </summary>
    [TestMethod]
    public void Context_SourceRegistration_Replaces() {
      TemplateContext context = new TemplateContext();
      context.AddSource("_Value", "old");
      context.AddSource("_Value", "new");
      Assert.IsTrue(context.HasSource("_Value"));
      Assert.IsFalse(context.HasSource("_Other"));
      Assert.AreEqual("new", TemplateEngineEdgeCaseTests.Render(context, "Main", "{{ _Value }}"));
    }

    /// <summary>
    /// Yields 1..count lazily.
    /// </summary>
    private static IEnumerable<int> CountTo(int count) {
      for (int value = 1; value <= count; value++) {
        yield return value;
      }
    }

    /// <summary>
    /// Template source whose dictionary can be changed after construction (to test caching).
    /// </summary>
    private sealed class MutableTemplateSource : ITemplateSource {

      private readonly Dictionary<string, string> _Templates;

      /// <summary>
      /// Creates the source around a live dictionary.
      /// </summary>
      public MutableTemplateSource(Dictionary<string, string> templates) {
        _Templates = templates;
      }

      /// <summary>
      /// Reads the current text.
      /// </summary>
      public bool TryRead(string templateName, out string templateText) {
        return _Templates.TryGetValue(templateName, out templateText);
      }

      /// <summary>
      /// Returns the current names.
      /// </summary>
      public string[] GetTemplateNames() {
        string[] names = new string[_Templates.Count];
        _Templates.Keys.CopyTo(names, 0);
        return names;
      }

    }

  }

}
