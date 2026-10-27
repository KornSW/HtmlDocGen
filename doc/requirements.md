# Requirements

## Motivation

Provide HTML-first, dependency-free server-side rendering for embedded web views, ordinary HTML responses, letters and PDF conversion workflows without coupling the renderer to ASP.NET or PDF libraries.

## Requirements

| ID | Requirement | Status |
| --- | --- | --- |
| R01 | .NET 8 and .NET 10 targets | Implemented (shared project + target shells) |
| R02 | No third-party packages | Implemented in core |
| R03 | Named underscore-prefixed sources | Implemented, including lazy sources |
| R04 | Explicit component-local variables | Implemented |
| R05 | Public property navigation | Implemented, including indexers and null-safe navigation |
| R06 | Public instance methods | Implemented with deterministic overload resolution and access policies |
| R07 | IEnumerable iteration | Implemented via u-for attribute on any element or u-block |
| R08 | .NET format strings and culture | Implemented (engine default, per-context override) |
| R09 | Conditions and Boolean expressions | Implemented: u-if / u-else-if / u-else, comparisons, and/or/not, parentheses |
| R10 | Includes with inherited sources and isolated locals | Implemented |
| R11 | Layouts, named slots, explicit include parameters | Implemented (u-include, u-fill, u-slot) |
| R12 | Embedded, filesystem and memory sources | Implemented, plus composite source with collision detection |
| R13 | HTML encoding by default | Implemented |
| R14 | Explicit trusted raw HTML | Implemented (raw keyword, TrustedHtml) |
| R15 | T4 composition snapshot and third-party compilation tests | Implemented (HtmlDocGen.ThirdPartyTests) |
| R16 | Full automated tests | Implemented (MSTest) |
| R17 | Detailed source diagnostics | Implemented (template name, line, column, include chain) |

## Scope boundaries

No ASP.NET types, runtime C# compilation, PDF conversion dependency, Windows interop, or external parser libraries in the core. Escaping is HTML-text and attribute oriented, not context-aware for JavaScript, CSS or URLs. The default policy is trusted; untrusted templates require RestrictedMemberAccessPolicy and are still not a full sandbox.
