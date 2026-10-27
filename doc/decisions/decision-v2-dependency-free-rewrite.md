# Decision: V2 as dependency-free C# rewrite

Date: 2026-10-27

## Context

HtmlDocGen 1.x was a VB.NET library for .NET Framework 4.6 that bundled wkhtmltopdf/pdftk binaries and depended on kSystemExtensions and ComponentDiscovery. Consumers on .NET 8/10 need HTML rendering without native binaries, framework coupling or third-party packages, and some need to embed the engine as source.

## Decision

V2 is a new C# HTML template engine in a shared project with net8.0 and net10.0 target shells. It has no package dependencies, no PDF conversion and no runtime C# compilation. A canonical T4 composition template (variant C) allows embedding as internal source code.

## Alternatives considered

- Port the VB code: rejected, it was string-replacement based and tied to wkhtmltopdf.
- Razor or another view engine: rejected, requires ASP.NET or runtime compilation and third-party packages.
- SDK multi-targeting: rejected by landscape convention (shared project plus target shells).

## Consequences

Breaking change for 1.x consumers (recorded in the changelog). PDF conversion is the consumer's responsibility. The 1.x implementation remains on master.
