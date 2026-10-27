using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using System.Net;

namespace HtmlDocGen {

  /// <summary>Resolves template text from a logical name.</summary>
  public interface ITemplateSource {
    /// <summary>Reads a named template.</summary>
    string Read(string name);
  }

  /// <summary>Resolves templates from an in-memory dictionary.</summary>
  public sealed class MemoryTemplateSource : ITemplateSource {
    private readonly IDictionary<string, string> _Templates;
    /// <summary>Creates an in-memory source.</summary>
    public MemoryTemplateSource(IDictionary<string, string> templates) {
      _Templates = templates ?? throw new ArgumentNullException(nameof(templates));
    }
    /// <summary>Reads a named template.</summary>
    public string Read(string name) {
      string result;
      if (!_Templates.TryGetValue(name, out result)) {
        throw new FileNotFoundException("Template was not found: " + name);
      }
      return result;
    }
  }

  /// <summary>Resolves templates embedded in a .NET assembly.</summary>
  public sealed class EmbeddedTemplateSource : ITemplateSource {
    private readonly Assembly _Assembly;
    private readonly string _Prefix;
    /// <summary>Creates a source for embedded resources.</summary>
    public EmbeddedTemplateSource(Assembly assembly, string prefix) {
      _Assembly = assembly ?? throw new ArgumentNullException(nameof(assembly));
      _Prefix = prefix ?? string.Empty;
    }
    /// <summary>Reads a named resource using its complete logical name.</summary>
    public string Read(string name) {
      string resourceName = _Prefix + name.Replace('/', '.');
      using (Stream stream = _Assembly.GetManifestResourceStream(resourceName)) {
        if (stream == null) {
          throw new FileNotFoundException("Embedded template was not found: " + resourceName);
        }
        using (StreamReader reader = new StreamReader(stream)) {
          return reader.ReadToEnd();
        }
      }
    }
  }

  /// <summary>Resolves templates below a designated filesystem directory.</summary>
  public sealed class FileTemplateSource : ITemplateSource {
    private readonly string _Root;
    /// <summary>Creates a filesystem template source.</summary>
    public FileTemplateSource(string root) {
      if (string.IsNullOrWhiteSpace(root)) {
        throw new ArgumentException("A template root is required.", nameof(root));
      }
      _Root = Path.GetFullPath(root);
    }
    /// <summary>Reads a template while rejecting directory traversal.</summary>
    public string Read(string name) {
      string full = Path.GetFullPath(Path.Combine(_Root, name.Replace('/', Path.DirectorySeparatorChar)));
      string prefix = _Root.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
      if (!full.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) {
        throw new InvalidOperationException("Template path escapes the configured root.");
      }
      return File.ReadAllText(full);
    }
  }

  /// <summary>Holds named data sources and explicit component-local variables.</summary>
  public sealed class TemplateContext {
    private readonly Dictionary<string, object> _Sources = new Dictionary<string, object>(StringComparer.Ordinal);
    private readonly Dictionary<string, object> _Locals = new Dictionary<string, object>(StringComparer.Ordinal);
    /// <summary>Registers a root source; names must begin with an underscore.</summary>
    public void AddSource(string name, object value) {
      if (string.IsNullOrWhiteSpace(name) || !name.StartsWith("_", StringComparison.Ordinal)) {
        throw new ArgumentException("Source names must begin with an underscore.", nameof(name));
      }
      _Sources[name] = value;
    }
    /// <summary>Registers an explicit local variable.</summary>
    public void AddLocal(string name, object value) {
      if (string.IsNullOrWhiteSpace(name) || name.StartsWith("_", StringComparison.Ordinal)) {
        throw new ArgumentException("Local names must not begin with an underscore.", nameof(name));
      }
      _Locals[name] = value;
    }
    /// <summary>Creates an isolated child context retaining sources and only explicit locals.</summary>
    public TemplateContext CreateChild() {
      TemplateContext child = new TemplateContext();
      foreach (KeyValuePair<string, object> pair in _Sources) {
        child._Sources.Add(pair.Key, pair.Value);
      }
      return child;
    }
    /// <summary>Creates a loop scope with one additional local variable.</summary>
    internal TemplateContext CreateIteration(string name, object value) {
      TemplateContext child = this.CreateChild();
      foreach (KeyValuePair<string, object> pair in _Locals) {
        child._Locals[pair.Key] = pair.Value;
      }
      child.AddLocal(name, value);
      return child;
    }
    /// <summary>Resolves a root name.</summary>
    internal object ResolveRoot(string name) {
      object value;
      if (name.StartsWith("_", StringComparison.Ordinal)) {
        if (_Sources.TryGetValue(name, out value)) {
          return value;
        }
      } else if (_Locals.TryGetValue(name, out value)) {
        return value;
      }
      throw new InvalidOperationException("Unknown template binding: " + name);
    }
  }

  /// <summary>Minimal dependency-free HTML template renderer.</summary>
  public sealed class TemplateEngine {
    private readonly ITemplateSource _Source;
    private readonly CultureInfo _Culture;
    /// <summary>Creates a renderer using the specified source and culture.</summary>
    public TemplateEngine(ITemplateSource source, CultureInfo culture) {
      _Source = source ?? throw new ArgumentNullException(nameof(source));
      _Culture = culture ?? CultureInfo.InvariantCulture;
    }
    /// <summary>Renders a named template with a fresh include stack.</summary>
    public string Render(string name, TemplateContext context) {
      if (context == null) {
        throw new ArgumentNullException(nameof(context));
      }
      return this.RenderInternal(name, context, new HashSet<string>(StringComparer.Ordinal));
    }
    /// <summary>Writes a rendered template to a text writer.</summary>
    public void RenderTo(string name, TemplateContext context, TextWriter writer) {
      if (writer == null) {
        throw new ArgumentNullException(nameof(writer));
      }
      writer.Write(this.Render(name, context));
    }
    /// <summary>Renders a named template and rejects recursive inclusion.</summary>
    private string RenderInternal(string name, TemplateContext context, HashSet<string> stack) {
      if (!stack.Add(name)) {
        throw new InvalidOperationException("Recursive template inclusion: " + name);
      }
      try {
        return this.RenderText(_Source.Read(name), context, stack);
      } finally {
        stack.Remove(name);
      }
    }
    /// <summary>Processes text interpolation and structural directives.</summary>
    private string RenderText(string template, TemplateContext context, HashSet<string> stack) {
      StringBuilder output = new StringBuilder();
      int position = 0;
      while (position < template.Length) {
        int expression = template.IndexOf("{{", position, StringComparison.Ordinal);
        int directive = template.IndexOf("<u-", position, StringComparison.Ordinal);
        int next = template.Length;
        if (expression >= 0 && expression < next) {
          next = expression;
        }
        if (directive >= 0 && directive < next) {
          next = directive;
        }
        output.Append(template.Substring(position, next - position));
        if (next == template.Length) {
          break;
        }
        if (next == expression) {
          int end = template.IndexOf("}}", next + 2, StringComparison.Ordinal);
          if (end < 0) {
            throw new FormatException("Unclosed template expression.");
          }
          string expressionText = template.Substring(next + 2, end - next - 2).Trim();
          bool raw = expressionText.StartsWith("raw ", StringComparison.Ordinal);
          if (raw) {
            expressionText = expressionText.Substring(4).Trim();
          }
          string format = null;
          int colon = expressionText.IndexOf(':');
          if (colon >= 0) {
            format = expressionText.Substring(colon + 1);
            expressionText = expressionText.Substring(0, colon);
          }
          object value = this.Evaluate(expressionText, context);
          string rendered = this.Format(value, format);
          output.Append(raw ? rendered : WebUtility.HtmlEncode(rendered));
          position = end + 2;
        } else {
          int openEnd = template.IndexOf('>', next);
          if (openEnd < 0) {
            throw new FormatException("Unclosed template directive.");
          }
          string tag = template.Substring(next + 1, openEnd - next - 1);
          if (tag.StartsWith("u-include ", StringComparison.Ordinal)) {
            string name = this.ReadAttribute(tag, "template");
            output.Append(this.RenderInternal(name, context.CreateChild(), stack));
            position = openEnd + 1;
            continue;
          }
          bool loop = tag.StartsWith("u-for ", StringComparison.Ordinal);
          bool condition = tag.StartsWith("u-if ", StringComparison.Ordinal);
          if (!loop && !condition) {
            throw new FormatException("Unsupported directive: " + tag);
          }
          string close = loop ? "</u-for>" : "</u-if>";
          string open = loop ? "<u-for " : "<u-if ";
          int depth = 1;
          int scan = openEnd + 1;
          int endStart = -1;
          while (scan < template.Length) {
            int nestedOpen = template.IndexOf(open, scan, StringComparison.Ordinal);
            int nestedClose = template.IndexOf(close, scan, StringComparison.Ordinal);
            if (nestedClose < 0) {
              throw new FormatException("Missing closing directive: " + close);
            }
            if (nestedOpen >= 0 && nestedOpen < nestedClose) {
              depth++;
              scan = nestedOpen + open.Length;
            } else {
              depth--;
              scan = nestedClose + close.Length;
              if (depth == 0) {
                endStart = nestedClose;
                break;
              }
            }
          }
          string body = template.Substring(openEnd + 1, endStart - openEnd - 1);
          if (loop) {
            string declaration = this.ReadAttribute(tag, "each");
            int separator = declaration.IndexOf(" in ", StringComparison.Ordinal);
            if (separator <= 0) {
              throw new FormatException("Expected 'item in expression' in u-for.");
            }
            string itemName = declaration.Substring(0, separator).Trim();
            string collectionExpression = declaration.Substring(separator + 4).Trim();
            object collection = this.Evaluate(collectionExpression, context);
            if (collection != null && !(collection is string)) {
              IEnumerable enumerable = collection as IEnumerable;
              if (enumerable == null) {
                throw new InvalidOperationException("u-for requires IEnumerable.");
              }
              foreach (object item in enumerable) {
                output.Append(this.RenderText(body, context.CreateIteration(itemName, item), stack));
              }
            }
          } else {
            object value = this.Evaluate(this.ReadAttribute(tag, "test"), context);
            if (this.IsTrue(value)) {
              output.Append(this.RenderText(body, context, stack));
            }
          }
          position = scan;
        }
      }
      return output.ToString();
    }
    /// <summary>Extracts a quoted directive attribute.</summary>
    private string ReadAttribute(string tag, string name) {
      string marker = name + "=\"";
      int start = tag.IndexOf(marker, StringComparison.Ordinal);
      if (start < 0) {
        throw new FormatException("Missing directive attribute: " + name);
      }
      start += marker.Length;
      int end = tag.IndexOf('"', start);
      if (end < 0) {
        throw new FormatException("Unclosed directive attribute: " + name);
      }
      return tag.Substring(start, end - start);
    }
    /// <summary>Resolves member paths and explicit public method calls.</summary>
    private object Evaluate(string expression, TemplateContext context) {
      string text = expression.Trim();
      if (text.Length == 0) {
        throw new FormatException("Empty binding expression.");
      }
      if (text == "true") {
        return true;
      }
      if (text == "false") {
        return false;
      }
      if (text == "null") {
        return null;
      }
      if (text.StartsWith("\"", StringComparison.Ordinal) && text.EndsWith("\"", StringComparison.Ordinal)) {
        return text.Substring(1, text.Length - 2);
      }
      int number;
      if (Int32.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out number)) {
        return number;
      }
      string[] parts = text.Split('.');
      object current = context.ResolveRoot(parts[0]);
      for (int index = 1; index < parts.Length; index++) {
        if (current == null) {
          return null;
        }
        string part = parts[index];
        int open = part.IndexOf('(');
        if (open >= 0) {
          if (!part.EndsWith(")", StringComparison.Ordinal)) {
            throw new FormatException("Invalid method expression: " + part);
          }
          string methodName = part.Substring(0, open);
          string arguments = part.Substring(open + 1, part.Length - open - 2);
          object[] values;
          if (arguments.Trim().Length == 0) {
            values = new object[0];
          } else {
            string[] argumentParts = arguments.Split(',');
            values = new object[argumentParts.Length];
            for (int argumentIndex = 0; argumentIndex < argumentParts.Length; argumentIndex++) {
              values[argumentIndex] = this.Evaluate(argumentParts[argumentIndex], context);
            }
          }
          MethodInfo method = null;
          foreach (MethodInfo candidate in current.GetType().GetMethods(BindingFlags.Instance | BindingFlags.Public)) {
            if (candidate.Name == methodName && candidate.GetParameters().Length == values.Length) {
              method = candidate;
              break;
            }
          }
          if (method == null) {
            throw new MissingMethodException(current.GetType().FullName, methodName);
          }
          current = method.Invoke(current, values);
        } else {
          PropertyInfo property = current.GetType().GetProperty(part, BindingFlags.Instance | BindingFlags.Public);
          if (property == null || property.GetIndexParameters().Length != 0) {
            throw new MissingMemberException(current.GetType().FullName, part);
          }
          current = property.GetValue(current, null);
        }
      }
      return current;
    }
    /// <summary>Formats a resolved value using .NET formatting conventions.</summary>
    private string Format(object value, string format) {
      if (value == null) {
        return string.Empty;
      }
      IFormattable formattable = value as IFormattable;
      if (formattable != null) {
        return formattable.ToString(format, _Culture);
      }
      return value.ToString();
    }
    /// <summary>Evaluates a value for a conditional block.</summary>
    private bool IsTrue(object value) {
      if (value == null) {
        return false;
      }
      if (value is bool) {
        return (bool)value;
      }
      if (value is string) {
        return !string.IsNullOrWhiteSpace((string)value);
      }
      if (value is int) {
        return (int)value != 0;
      }
      return true;
    }
  }
}
