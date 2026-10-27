---
name: most-important-details-for-htmldocgen
description: Mandatory repository-specific guidance for implementing and maintaining HtmlDocGen V2, a dependency-free .NET HTML templating engine and T4-embeddable source library.
---

# HtmlDocGen V2 — IDE Agent Handover

## Mission

Complete a production-quality HTML-first server-side rendering engine that works in ASP.NET hosts, desktop WebView2 applications, console tools and document/PDF workflows WITHOUT referencing ASP.NET, PDF, Windows-only APIs, NuGet libraries or other external packages in the engine. Treat the old VB HtmlDocGen on master as requirements evidence, not a design to port mechanically.

## Authoritative standards

Read the leading skills in SmartStandards/AI-SKILLS-SMART before editing: csharp-coding-standards, dotnet-sdk-style-project-files, smart-solution-and-project-structure, dotnet-t4-source-embedding, include-additional-mandatory-skills, smart-skill-maintaining-guidelines, master-architect. Mirror the leading .editorconfig and mandatory skill copies. Follow shared-project plus per-target-shell layout: NOT TargetFrameworks. Keep code/comments/docs in English, user communication in German. No var, ternary, async/await, expression-bodied properties, or unbraced control flow; fields _PascalCase, this. for instance nonfields, XML summaries on methods.

## Product decisions

- Root data sources have explicit underscore-prefixed names, e.g. _Repository; any number may be registered.
- Iteration aliases are unprefixed, e.g. product in _Repository.GetProducts().
- Support public property paths, method calls with arguments, nested paths, standard .NET format strings and configurable CultureInfo.
- Render HTML with default encoding; raw HTML must be explicitly trusted.
- Structural directives should be HTML-first: u-for, u-if, u-else, u-include, layouts, named slots.
- Every include inherits registered underscore-prefixed data sources; ordinary local variables are NOT inherited and must be passed explicitly.
- Nested iteration bodies DO inherit their lexical locals.
- No runtime C# compilation; no external parser package; do not parse nested HTML with naive regex.
- Embedded resources, filesystem and in-memory template sources; future modular provider composition with collision detection.
- Output should support TextWriter and streaming where practical. PDF conversion is outside core.
- Strict errors with template identity, line and column, and include-cycle detection.
- Do not assume rendering is a security boundary. Implement a deliberate method-access policy before supporting untrusted templates.

## T4 embedding is mandatory

The engine MUST support source embedding into consumer projects with zero package/project references. Follow composition variant C in dotnet-t4-source-embedding/SKILL.md. Maintain canonical .tt in origin test project, ttVersion and remote version check, namespace rewrite (CopyFor), public-to-internal and partial transformations, and a checked-in generated .cs. Include a third-party consumer test project proving that the snapshot compiles. The generated .cs is never edited manually. Minimize origin source file count without creating monolithic unmaintainable classes. Keep all namespaces block-scoped and all usings explicit.

## Legacy requirements mining

Inspect HtmlDocGen master: DocumentDataSource.vb, TemplateBlock.vb, MultipleDocumentBlocks.vb, HtmlDocumentFormatProvider.vb, GeneratorV2/Class1.vb and GeneratorV2/TODO.txt. Port only required utility behavior, after locating original KornSW helper sources in GitHub. Avoid third-party libraries entirely.

## Status and immediate work

This branch currently contains a preliminary engine skeleton, not a finished product. Read doc/requirements.md, doc/architecture.md and doc/work/backlog.md. DO NOT declare completion or a passing build before running actual builds and tests. Prioritize finishing grammar, safe method binding, layouts/slots, T4, tests, solution standardization and then removing the legacy VB tree on V2. Preserve master. Do not merge automatically.

## Acceptance criteria

1. Clean .NET 8 and .NET 10 builds, no external PackageReference in core.
2. Passing tests for binding, formatting, nested iteration, conditions, includes, layouts, slots, escaping, missing sources and recursive templates.
3. T4 generation verified, checked-in output builds in an independent consumer without library references.
4. Complete English documentation and standard solution structure.
5. All code and docs committed on V2; master unchanged.
