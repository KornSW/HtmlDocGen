# Quickstart

## Minimal example

```csharp
Dictionary<string, string> templates = new Dictionary<string, string>();
templates.Add("hello", "<h1>{{_Customer.Name}}</h1>");
TemplateContext context = new TemplateContext();
context.AddSource("_Customer", new Customer { Name = "Ada" });
TemplateEngine engine = new TemplateEngine(new MemoryTemplateSource(templates), CultureInfo.InvariantCulture);
string html = engine.Render("hello", context);
```

## Iteration

```html
<u-for each="item in _Repository.GetItems()"><p>{{item.Name}}</p></u-for>
```

## Conditions and includes

```html
<u-if test="_Flags.ShowDetails"><u-include template="details" /></u-if>
```

Includes inherit registered underscore-prefixed sources, not loop-local variables. Use explicit data sources for cross-template data until parameterized includes are implemented.
