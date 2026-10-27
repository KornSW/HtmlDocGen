using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;

namespace HtmlDocGen {

  // v 2.0.0

  /// <summary>
  /// Resolves template text from a logical template name.
  /// Implementations must be thread-safe for concurrent reads.
  /// </summary>
  public interface ITemplateSource {

    /// <summary>
    /// Tries to read the template with the specified logical name.
    /// </summary>
    /// <param name="templateName">The logical template name, using '/' as path separator.</param>
    /// <param name="templateText">Receives the template text when the template exists.</param>
    /// <returns>True when the template exists in this source.</returns>
    bool TryRead(string templateName, out string templateText);

    /// <summary>
    /// Returns the logical names of all templates provided by this source.
    /// Used for collision detection when sources are composed.
    /// </summary>
    /// <returns>An array of logical template names.</returns>
    string[] GetTemplateNames();

  }

  /// <summary>
  /// Provides templates from an in-memory dictionary (typically used for tests or generated templates).
  /// </summary>
  public sealed class MemoryTemplateSource : ITemplateSource {

    private readonly Dictionary<string, string> _Templates;

    /// <summary>
    /// Creates an in-memory source holding a copy of the specified templates.
    /// </summary>
    /// <param name="templates">Template texts keyed by logical template name.</param>
    public MemoryTemplateSource(IDictionary<string, string> templates) {
      if (templates == null) {
        throw new ArgumentNullException(nameof(templates));
      }
      _Templates = new Dictionary<string, string>(StringComparer.Ordinal);
      foreach (KeyValuePair<string, string> template in templates) {
        _Templates.Add(template.Key, template.Value);
      }
    }

    /// <summary>
    /// Tries to read a template from the dictionary.
    /// </summary>
    public bool TryRead(string templateName, out string templateText) {
      if (templateName == null) {
        throw new ArgumentNullException(nameof(templateName));
      }
      return _Templates.TryGetValue(templateName, out templateText);
    }

    /// <summary>
    /// Returns the names of all templates in the dictionary.
    /// </summary>
    public string[] GetTemplateNames() {
      string[] names = new string[_Templates.Count];
      _Templates.Keys.CopyTo(names, 0);
      return names;
    }

  }

  /// <summary>
  /// Provides templates from files below a designated root directory.
  /// Template names are relative paths; paths escaping the root directory are rejected.
  /// </summary>
  public sealed class FileTemplateSource : ITemplateSource {

    private readonly string _RootDirectory;
    private readonly string _DefaultExtension;

    /// <summary>
    /// Creates a filesystem source without a default file extension.
    /// </summary>
    /// <param name="rootDirectory">The directory containing the templates.</param>
    public FileTemplateSource(string rootDirectory) : this(rootDirectory, string.Empty) {
    }

    /// <summary>
    /// Creates a filesystem source with a default file extension that is appended
    /// when a template name cannot be found as written (for example ".html").
    /// </summary>
    /// <param name="rootDirectory">The directory containing the templates.</param>
    /// <param name="defaultExtension">The extension including the leading dot, or an empty string.</param>
    public FileTemplateSource(string rootDirectory, string defaultExtension) {
      if (string.IsNullOrWhiteSpace(rootDirectory)) {
        throw new ArgumentException("A template root directory is required.", nameof(rootDirectory));
      }
      if (defaultExtension == null) {
        throw new ArgumentNullException(nameof(defaultExtension));
      }
      _RootDirectory = Path.GetFullPath(rootDirectory).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
      _DefaultExtension = defaultExtension;
    }

    /// <summary>
    /// Tries to read a template file while rejecting directory traversal.
    /// </summary>
    public bool TryRead(string templateName, out string templateText) {
      if (templateName == null) {
        throw new ArgumentNullException(nameof(templateName));
      }
      string fullPath = this.GetFullPath(templateName);
      if (!File.Exists(fullPath) && _DefaultExtension.Length > 0) {
        fullPath = this.GetFullPath(templateName + _DefaultExtension);
      }
      if (!File.Exists(fullPath)) {
        templateText = null;
        return false;
      }
      templateText = File.ReadAllText(fullPath, Encoding.UTF8);
      return true;
    }

    /// <summary>
    /// Returns the relative paths of all files below the root directory.
    /// </summary>
    public string[] GetTemplateNames() {
      if (!Directory.Exists(_RootDirectory)) {
        return new string[0];
      }
      string[] files = Directory.GetFiles(_RootDirectory, "*", SearchOption.AllDirectories);
      string[] names = new string[files.Length];
      for (int index = 0; index < files.Length; index++) {
        names[index] = files[index].Substring(_RootDirectory.Length + 1).Replace(Path.DirectorySeparatorChar, '/');
      }
      return names;
    }

    /// <summary>
    /// Maps a logical template name to a full path and ensures that it stays below the root directory.
    /// </summary>
    private string GetFullPath(string templateName) {
      string relativePath = templateName.Replace('/', Path.DirectorySeparatorChar);
      string fullPath = Path.GetFullPath(Path.Combine(_RootDirectory, relativePath));
      string requiredPrefix = _RootDirectory + Path.DirectorySeparatorChar;
      if (!fullPath.StartsWith(requiredPrefix, StringComparison.OrdinalIgnoreCase)) {
        throw new InvalidOperationException("The template name '" + templateName + "' escapes the configured root directory.");
      }
      return fullPath;
    }

  }

  /// <summary>
  /// Provides templates embedded as manifest resources in an assembly.
  /// A template name 'Pages/Home.html' maps to the resource '{prefix}Pages.Home.html'.
  /// </summary>
  public sealed class EmbeddedTemplateSource : ITemplateSource {

    private readonly Assembly _Assembly;
    private readonly string _ResourcePrefix;

    /// <summary>
    /// Creates a source for embedded resources.
    /// </summary>
    /// <param name="assembly">The assembly containing the resources.</param>
    /// <param name="resourcePrefix">The resource name prefix including the trailing dot, for example 'MyApp.Templates.'.</param>
    public EmbeddedTemplateSource(Assembly assembly, string resourcePrefix) {
      if (assembly == null) {
        throw new ArgumentNullException(nameof(assembly));
      }
      if (resourcePrefix == null) {
        throw new ArgumentNullException(nameof(resourcePrefix));
      }
      _Assembly = assembly;
      _ResourcePrefix = resourcePrefix;
    }

    /// <summary>
    /// Tries to read an embedded resource.
    /// </summary>
    public bool TryRead(string templateName, out string templateText) {
      if (templateName == null) {
        throw new ArgumentNullException(nameof(templateName));
      }
      string resourceName = _ResourcePrefix + templateName.Replace('/', '.');
      using (Stream stream = _Assembly.GetManifestResourceStream(resourceName)) {
        if (stream == null) {
          templateText = null;
          return false;
        }
        using (StreamReader reader = new StreamReader(stream, Encoding.UTF8)) {
          templateText = reader.ReadToEnd();
          return true;
        }
      }
    }

    /// <summary>
    /// Returns the names of all resources starting with the configured prefix (without the prefix).
    /// </summary>
    public string[] GetTemplateNames() {
      List<string> names = new List<string>();
      foreach (string resourceName in _Assembly.GetManifestResourceNames()) {
        if (resourceName.StartsWith(_ResourcePrefix, StringComparison.Ordinal)) {
          names.Add(resourceName.Substring(_ResourcePrefix.Length));
        }
      }
      return names.ToArray();
    }

  }

  /// <summary>
  /// Composes multiple template sources (for example one per application module).
  /// Template names must be unique across all sources; collisions are detected on construction.
  /// </summary>
  public sealed class CompositeTemplateSource : ITemplateSource {

    private readonly ITemplateSource[] _Sources;

    /// <summary>
    /// Creates a composite source and rejects template names provided by more than one source.
    /// </summary>
    /// <param name="sources">The sources to compose.</param>
    public CompositeTemplateSource(ITemplateSource[] sources) {
      if (sources == null) {
        throw new ArgumentNullException(nameof(sources));
      }
      Dictionary<string, int> owners = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
      List<string> collisions = new List<string>();
      for (int index = 0; index < sources.Length; index++) {
        if (sources[index] == null) {
          throw new ArgumentException("The source at index " + index + " is null.", nameof(sources));
        }
        foreach (string templateName in sources[index].GetTemplateNames()) {
          int owner;
          if (owners.TryGetValue(templateName, out owner)) {
            if (owner != index) {
              collisions.Add(templateName);
            }
          } else {
            owners.Add(templateName, index);
          }
        }
      }
      if (collisions.Count > 0) {
        throw new InvalidOperationException("Template names are provided by more than one source: " + string.Join(", ", collisions.ToArray()));
      }
      _Sources = (ITemplateSource[])sources.Clone();
    }

    /// <summary>
    /// Tries to read the template from the first source providing it.
    /// </summary>
    public bool TryRead(string templateName, out string templateText) {
      foreach (ITemplateSource source in _Sources) {
        if (source.TryRead(templateName, out templateText)) {
          return true;
        }
      }
      templateText = null;
      return false;
    }

    /// <summary>
    /// Returns the template names of all composed sources.
    /// </summary>
    public string[] GetTemplateNames() {
      List<string> names = new List<string>();
      foreach (ITemplateSource source in _Sources) {
        names.AddRange(source.GetTemplateNames());
      }
      return names.ToArray();
    }

  }

}
