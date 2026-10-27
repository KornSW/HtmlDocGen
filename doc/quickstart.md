# Quickstart

## Minimal example

```csharp
Dictionary<string, string> templates = new Dictionary<string, string>();
templates.Add("hello", "<h1>{{ _Customer.Name }}</h1>");
TemplateContext context = new TemplateContext();
context.AddSource("_Customer", new Customer { Name = "Ada" });
TemplateEngine engine = new TemplateEngine(new MemoryTemplateSource(templates), CultureInfo.InvariantCulture);
string html = engine.Render("hello", context);
```

## Expressions and formatting

```html
<p>{{ _Order.Total:N2 }} on {{ _Order.Date:yyyy-MM-dd }}</p>
<p>{{ _Repo.GetItem(1).Name.ToUpperInvariant() }}</p>
<div>{{ raw _Repo.TrustedFragment }}</div>
```

Values are HTML encoded unless marked with `raw` or wrapped in `TrustedHtml`. The culture comes from `TemplateEngineOptions.Culture` and can be overridden by `TemplateContext.Culture`.

## Iteration and conditions

```html
<ul><li u-for="item in _Repo.Items">{{ item.Name }}</li></ul>
<b u-if="item.Id == 1 && item.Name != 'x'">one</b>
<i u-else-if="item.Id >= 3 or item.Name == 'Zeta'">big</i>
<s u-else>other</s>
```

`u-block` renders only its content and can carry `u-for` / `u-if` when no wrapping element is wanted.

## Includes, layouts and slots

```html
<u-include template="Layout" title="pageTitle">
  <u-fill slot="head"><meta name="t" content="{{ pageTitle }}"></u-fill>
  <p>{{ pageTitle }} body</p>
</u-include>
```

```html
<html><head><title>{{ title }}</title><u-slot name="head"></u-slot></head>
<body><u-slot></u-slot><footer><u-slot name="footer">default</u-slot></footer></body></html>
```

Include attributes are expressions passed as locals; includes inherit underscore-prefixed sources but not the caller's locals.

## Restricting method calls

```csharp
RestrictedMemberAccessPolicy policy = new RestrictedMemberAccessPolicy();
policy.AllowMethod(typeof(Repository), "Add");
TemplateEngineOptions options = new TemplateEngineOptions();
options.Policy = policy;
TemplateEngine engine = new TemplateEngine(source, options);
```

Methods marked with `[TemplateCallable]` are allowed as well.

## Embedding as source (T4)

Copy `dotnet/tests/HtmlDocGen.ThirdPartyTests/HtmlDocGen.tt` into the consuming project and run the custom tool. It generates an internal copy in the namespace `HtmlDocGen.CopyFor<YourNamespace>` and reports when a newer template version exists.
