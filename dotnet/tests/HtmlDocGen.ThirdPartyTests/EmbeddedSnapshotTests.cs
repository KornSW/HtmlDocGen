using System.Collections.Generic;
using HtmlDocGen.CopyForHtmlDocGen.ThirdPartyTests;

namespace HtmlDocGen.ThirdPartyTests {

  /// <summary>
  /// Verifies that the embedded T4 snapshot compiles and renders without any reference to the HtmlDocGen library.
  /// </summary>
  [TestClass]
  public class EmbeddedSnapshotTests {

    /// <summary>
    /// Renders iteration, encoding and an include using the embedded copy.
    /// </summary>
    [TestMethod]
    public void EmbeddedCopy_RendersTemplate() {
      Dictionary<string, string> templates = new Dictionary<string, string>();
      templates.Add("Main", "<ul><li u-for=\"name in _Names\">{{ name }}</li></ul><u-include template=\"Footer\" text=\"'a&b'\"></u-include>");
      templates.Add("Footer", "<p>{{ text }}</p>");
      TemplateEngine engine = new TemplateEngine(new MemoryTemplateSource(templates));
      TemplateContext context = new TemplateContext();
      context.AddSource("_Names", new string[] { "X", "Y" });
      string result = engine.Render("Main", context);
      Assert.AreEqual("<ul><li>X</li><li>Y</li></ul><p>a&amp;b</p>", result);
    }

  }

}
