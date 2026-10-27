# Requirements

## Motivation

Provide HTML-first, dependency-free server-side rendering for embedded web views, ordinary HTML responses, letters and PDF conversion workflows without coupling the renderer to ASP.NET or PDF libraries.

## Requirements

| ID | Requirement | Status |
| --- | --- | --- |
| R01 | .NET 8 and .NET 10 targets | Initial project shells |
| R02 | No third-party packages | Implemented in core |
| R03 | Named underscore-prefixed sources | Implemented |
| R04 | Explicit component-local variables | Implemented |
| R05 | Public property navigation | Implemented |
| R06 | Public instance methods | Basic implementation; needs safe overload resolution |
| R07 | IEnumerable iteration | Implemented via u-for element |
| R08 | .NET format strings and culture | Implemented |
| R09 | Conditions and Boolean expressions | Simple truthiness only; operators pending |
| R10 | Includes with inherited sources and isolated locals | Implemented |
| R11 | Layouts, named slots, explicit include parameters | Pending |
| R12 | Embedded, filesystem and memory sources | Implemented |
| R13 | HTML encoding by default | Implemented |
| R14 | Explicit trusted raw HTML | Implemented; requires security review |
| R15 | T4 composition snapshot and third-party compilation tests | Pending |
| R16 | Full automated tests | Pending |
| R17 | Detailed source diagnostics | Pending |

## Scope boundaries

No ASP.NET types, runtime C# compilation, PDF conversion dependency, Windows interop, or external parser libraries in the core. Rendering must not be used as a sandbox for untrusted templates: public method invocation is currently unrestricted.
