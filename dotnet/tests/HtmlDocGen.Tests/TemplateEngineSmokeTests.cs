using System.Collections.Generic;
using System.Globalization;

namespace HtmlDocGen {

  /// <summary>
  /// Smoke tests proving that the solution structure (shared project, target shell, test project) is wired correctly.
  /// </summary>
  [TestClass]
  public class TemplateEngineSmokeTests {

    /// <summary>
    /// Renders a template without any expressions and expects the text to be passed through unchanged.
    /// </summary>
    [TestMethod]
    public void Render_PlainHtmlTemplate_ReturnsTemplateTextUnchanged() {

      Dictionary<string, string> templates = new Dictionary<string, string>();
      templates.Add("Plain", "<p>Hello</p>");

      MemoryTemplateSource source = new MemoryTemplateSource(templates);
      TemplateEngine engine = new TemplateEngine(source, CultureInfo.InvariantCulture);
      TemplateContext context = new TemplateContext();

      string result = engine.Render("Plain", context);

      Assert.AreEqual("<p>Hello</p>", result);
    }

  }

}
