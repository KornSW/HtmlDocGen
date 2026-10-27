# Backlog for IDE continuation

## Critical

- Complete full HTML-first directive syntax: u-for and u-if attributes on arbitrary HTML tags, with nested HTML and else branches.
- Implement an expression lexer/parser supporting chained method calls, quoted strings, nested arguments, boolean operators, comparisons and parentheses.
- Add explicit method access policies and deterministic overload selection; disallow dangerous side effects by policy, not method-name heuristics.
- Implement include parameters, layouts, named slots, and recursive cycle diagnostics.
- Add a comprehensive test suite (no third-party test framework if strict no-external-libraries applies), and compile both .NET targets.
- Create canonical composition-mode T4 template and checked-in generated snapshot, plus a consumer compilation project. Follow SmartStandards/AI-SKILLS-SMART dotnet-t4-source-embedding skill exactly.
- Add SmartStandards-standard .sln, .shproj, .projitems, full csproj metadata, .editorconfig mirror, standard skill copies and release metadata.
- Remove legacy VB sources and binaries from V2 only after new tests and T4 snapshot compile successfully.

## Important

- Parser cache and member accessor cache; proper error locations.
- Output-context-aware encoding and explicit trusted HTML value wrapper.
- Nullable and dictionary member resolution; lazy source delegates.
- Type-specific serializers, truth evaluators and iteration adapters.
- Port any required helper logic from its original KornSW repository into internal C# code, avoiding package references.
