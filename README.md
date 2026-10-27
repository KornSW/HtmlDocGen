# HtmlDocGen V2

Dependency-free .NET HTML template engine for web pages, letters and document-to-PDF pipelines. Templates are plain HTML with a few `u-` directives; data comes from explicitly registered .NET objects. No ASP.NET dependency, no runtime compilation, no third-party packages.

## Motivation

Server-rendered HTML for embedded web views, mails, letters or PDF conversion often pulls in a full web framework or a C#-compiling view engine. HtmlDocGen keeps templates valid HTML, renders them from any .NET 8 / .NET 10 process and can even be embedded as internal source code via T4.

## Examples

```html
<u-include template="Layout" title="'Orders'">
  <table><tr u-for="order in _Repo.GetOrders()"><td>{{ order.Date:d }}</td><td>{{ order.Total:N2 }}</td></tr></table>
  <p u-if="_Repo.GetOrders().Count == 0">No orders.</p>
</u-include>
```

```csharp
TemplateContext context = new TemplateContext();
context.AddSource("_Repo", repository);
TemplateEngine engine = new TemplateEngine(new FileTemplateSource(templateDirectory), CultureInfo.GetCultureInfo("de-DE"));
string html = engine.Render("Orders", context);
```

More in the [quickstart](doc/quickstart.md).

## Documentation

- [Requirements](doc/requirements.md)
- [Architecture](doc/architecture.md)
- [Quickstart](doc/quickstart.md)
- [Ecosystem](doc/ecosystem.md)
- [Changelog](doc/changelog.md)
- [Decisions](doc/decisions/)
- [Work: backlog](doc/work/backlog.md), [technical debt](doc/work/tech-debt.md)

The previous VB implementation is preserved on the master branch.

## Build

Open dotnet/HtmlDocGen.sln. The engine is a shared project (dotnet/src/HtmlDocGen) with one target shell per framework (net8.0, net10.0), nested in the virtual folder `targets`. Library output lands in dotnet/dist/Shared/<tfm>, which dotnet/HtmlDocGen.nuspec packs into one NuGet package. dotnet/tests contains the MSTest suite and the T4 third-party consumer test.

## License

MIT + Commons Clause, see [LICENSE](LICENSE). Versions 1.x on master were published under GPL-3.0-only.
