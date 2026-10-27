# Architecture

## Overview

A TemplateEngine resolves text via ITemplateSource and renders it against TemplateContext. TemplateContext holds explicitly named sources (underscore prefix) and local variables. Nested iterations inherit the current local scope. Includes create a child context with inherited sources but without locals. Values are formatted with CultureInfo and HTML encoded unless explicitly marked raw.

## Data flow

Template name -> ITemplateSource.Read -> TemplateEngine.RenderText -> expression evaluation -> formatted HTML string.

## Extensibility

Implement ITemplateSource to retrieve templates from additional stores. Keep any HTTP or PDF integration in separate host adapters. The canonical code lives in dotnet/src/HtmlDocGen/TemplateEngine.cs; framework shells link the same source.

## Security considerations

Only trusted templates may be rendered. Public method calls may have side effects. A future access policy must explicitly restrict invocations; names such as Delete or Update are not sufficient security boundaries. Escaping is HTML-text-oriented, not context-aware for JavaScript, CSS or URLs.
