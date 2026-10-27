using System;
using System.Collections.Generic;
using System.Reflection;

namespace HtmlDocGen {

  // v 2.0.0

  /// <summary>
  /// Decides which public members template expressions may access.
  /// Template rendering is NOT a security boundary by itself; the policy is the deliberate place
  /// where method invocation is restricted. Method names are never used as a heuristic.
  /// </summary>
  public abstract class MemberAccessPolicy {

    private static readonly MemberAccessPolicy _Trusted = new TrustedMemberAccessPolicy();

    /// <summary>
    /// The default policy for templates under the control of the application developer:
    /// all public instance properties and methods except reflection-sensitive members.
    /// </summary>
    public static MemberAccessPolicy Trusted {
      get {
        return _Trusted;
      }
    }

    /// <summary>
    /// Returns true when the specified public instance property (or indexer) may be read.
    /// </summary>
    /// <param name="property">The property to check.</param>
    public abstract bool IsPropertyAccessible(PropertyInfo property);

    /// <summary>
    /// Returns true when the specified public instance method may be invoked.
    /// </summary>
    /// <param name="method">The method to check.</param>
    public abstract bool IsMethodAccessible(MethodInfo method);

    /// <summary>
    /// Returns true for members that would allow escaping into reflection or runtime internals
    /// (System.Type, System.Reflection, delegates, GetType and the members of System.Object other than ToString).
    /// Every built-in policy denies these members.
    /// </summary>
    /// <param name="member">The member to check.</param>
    protected static bool IsReflectionSensitive(MemberInfo member) {
      Type declaringType = member.DeclaringType;
      if (declaringType == null) {
        return true;
      }
      if (declaringType == typeof(object)) {
        if (member.Name == "ToString") {
          return false;
        }
        return true;
      }
      if (member.Name == "GetType") {
        return true;
      }
      if (typeof(MemberInfo).IsAssignableFrom(declaringType) || typeof(Assembly).IsAssignableFrom(declaringType) || typeof(Module).IsAssignableFrom(declaringType)) {
        return true;
      }
      if (typeof(Delegate).IsAssignableFrom(declaringType)) {
        return true;
      }
      if (declaringType == typeof(AppDomain)) {
        return true;
      }
      string declaringNamespace = declaringType.Namespace;
      if (declaringNamespace != null && declaringNamespace.StartsWith("System.Reflection", StringComparison.Ordinal)) {
        return true;
      }
      return false;
    }

  }

  /// <summary>
  /// Allows all public instance properties and methods except reflection-sensitive members.
  /// Use only for trusted templates: method calls may have side effects.
  /// </summary>
  public sealed class TrustedMemberAccessPolicy : MemberAccessPolicy {

    /// <summary>
    /// Allows every non-sensitive public instance property.
    /// </summary>
    public override bool IsPropertyAccessible(PropertyInfo property) {
      return !MemberAccessPolicy.IsReflectionSensitive(property);
    }

    /// <summary>
    /// Allows every non-sensitive public instance method.
    /// </summary>
    public override bool IsMethodAccessible(MethodInfo method) {
      return !MemberAccessPolicy.IsReflectionSensitive(method);
    }

  }

  /// <summary>
  /// Marks a public instance method as invocable by templates under the <see cref="RestrictedMemberAccessPolicy"/>.
  /// </summary>
  [AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
  public sealed class TemplateCallableAttribute : Attribute {
  }

  /// <summary>
  /// Allows reading non-sensitive public instance properties, but invoking only methods that are
  /// marked with <see cref="TemplateCallableAttribute"/> or explicitly registered via <see cref="AllowMethod"/>.
  /// </summary>
  public sealed class RestrictedMemberAccessPolicy : MemberAccessPolicy {

    private readonly object _SyncRoot = new object();
    private readonly HashSet<string> _AllowedMethods = new HashSet<string>(StringComparer.Ordinal);

    /// <summary>
    /// Explicitly allows all public instance overloads of a method declared on (or inherited by) the specified type.
    /// </summary>
    /// <param name="type">The type declaring or inheriting the method.</param>
    /// <param name="methodName">The method name.</param>
    public void AllowMethod(Type type, string methodName) {
      if (type == null) {
        throw new ArgumentNullException(nameof(type));
      }
      if (string.IsNullOrWhiteSpace(methodName)) {
        throw new ArgumentException("A method name is required.", nameof(methodName));
      }
      lock (_SyncRoot) {
        _AllowedMethods.Add(RestrictedMemberAccessPolicy.CreateKey(type, methodName));
      }
    }

    /// <summary>
    /// Allows every non-sensitive public instance property.
    /// </summary>
    public override bool IsPropertyAccessible(PropertyInfo property) {
      return !MemberAccessPolicy.IsReflectionSensitive(property);
    }

    /// <summary>
    /// Allows only attributed or explicitly registered methods.
    /// </summary>
    public override bool IsMethodAccessible(MethodInfo method) {
      if (MemberAccessPolicy.IsReflectionSensitive(method)) {
        return false;
      }
      if (method.IsDefined(typeof(TemplateCallableAttribute), true)) {
        return true;
      }
      lock (_SyncRoot) {
        Type current = method.ReflectedType;
        while (current != null) {
          if (_AllowedMethods.Contains(RestrictedMemberAccessPolicy.CreateKey(current, method.Name))) {
            return true;
          }
          current = current.BaseType;
        }
        if (method.DeclaringType != null && _AllowedMethods.Contains(RestrictedMemberAccessPolicy.CreateKey(method.DeclaringType, method.Name))) {
          return true;
        }
      }
      return false;
    }

    /// <summary>
    /// Creates the lookup key for a type/method pair.
    /// </summary>
    private static string CreateKey(Type type, string methodName) {
      return type.AssemblyQualifiedName + "|" + methodName;
    }

  }

}
