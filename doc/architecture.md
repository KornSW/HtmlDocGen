# Architecture

## Overview

TemplateEngine resolves template text via ITemplateSource, parses it once into a TemplateDocument (cached per engine) and renders the node tree against a TemplateContext. The context holds explicitly named sources (underscore prefix, optionally lazy) and local variables. Iteration scopes inherit the lexical locals; includes create an isolated scope that inherits sources and receives only explicit parameters and slot fills.

## Data flow

Template name -> ITemplateSource.TryRead -> TemplateParser (HTML-aware, raw text for script/style) -> node tree with parsed expressions -> TemplateRuntime render pass -> formatted, encoded HTML written to a TextWriter.

## Components

| File | Responsibility |
| --- | --- |
| TemplateSources.cs | ITemplateSource, memory, file (root-protected), embedded and composite sources |
| TemplateContext.cs | Public context, render scopes, TrustedHtml |
| MemberAccessPolicy.cs | Trusted and restricted policies, TemplateCallableAttribute |
| TemplateExpressions.cs | Lexer, expression parser, evaluator, overload resolver, member cache |
| TemplateParser.cs | HTML parser, directives, node model, TemplateException diagnostics |
| TemplateEngine.cs | Public API, options, runtime, formatting and truthiness |

See [decision-v2-dependency-free-rewrite](decisions/decision-v2-dependency-free-rewrite.md).

## Packaging and embedding

The canonical code lives in the shared project dotnet/src/HtmlDocGen; the target shells HtmlDocGen.net8.0 and HtmlDocGen.net10.0 import it and build to dotnet/dist/Shared/<tfm>. For consumers without a package reference, dotnet/tests/HtmlDocGen.ThirdPartyTests/HtmlDocGen.tt is the canonical T4 composition template (variant C, version check enabled). Its checked-in snapshot is compiled and tested there without any library reference.

## Security considerations

Only trusted templates may be rendered with the default trusted policy, because public methods may have side effects. RestrictedMemberAccessPolicy allows only methods marked with TemplateCallableAttribute or registered explicitly; reflection-sensitive members are always denied. Escaping is HTML-oriented, not context-aware for JavaScript, CSS or URLs.
