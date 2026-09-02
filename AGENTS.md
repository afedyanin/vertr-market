# Agent Instructions

Instructions for AI coding agents.

### Technology Stack

* .NET 10.0
* C# 14
* Test framework: **nUnit**
* Multi-platform support (Windows, Linux, macOS, containers)

## General

* Make only high confidence suggestions when reviewing code changes.
* Always use the latest version C#, currently C# 14 features.

## Formatting

* Apply code-formatting style defined in `.editorconfig`.
* Prefer file-scoped namespace declarations and single-line using directives.
* Insert a newline before the opening curly brace of any code block (e.g., after `if`, `for`, `while`, `foreach`, `using`, `try`, etc.).
* Ensure that the final return statement of a method is on its own line.
* Use pattern matching and switch expressions wherever possible.
* Use `nameof` instead of string literals when referring to member names.
* Place private class declarations at the bottom of the file.
* Use Global analyzer: `Workleap.DotNet.CodingStandards`.
* `Directory.Build.props` enforces: `<Nullable>enable</Nullable>`, `<ImplicitUsings>enable</ImplicitUsings>`, `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>`.
* Prefer ?. if applicable (e.g. scope?.Dispose()).
* Use ObjectDisposedException.ThrowIf where applicable.
* There should be no trailing whitespace in any lines.

### Code comments

* Err on the side of over-commenting code when the reasoning is not obvious. Comments should explain **WHY** code is written a particular way; the **WHY** is the most important part.
* Do comment non-obvious implementation details: concurrency hazards, lifecycle constraints, compatibility requirements, platform quirks, upstream workarounds, and intentional deviations from the obvious helper or API.
* When parsing strings, logs, command output, protocol payloads, or other loosely structured data, include a comment with an example of the raw format being parsed. Show edge cases, escaping rules, delimiters, optional fields, or malformed-but-observed inputs when they affect the parser.
* When code follows an external standard, protocol, or ecosystem convention, include valid links to the relevant source material so future readers can verify the rule and understand why the code follows it.
* Do not add comments that simply narrate clear code, such as "set the timeout" immediately before assigning a timeout.
* Keep workaround comments close to the workaround. Include an issue link when the workaround is tied to an upstream bug, and describe the condition for removing it when that is known.

### Nullable Reference Types

* Declare variables non-nullable, and check for `null` at entry points.
* Always use `is null` or `is not null` instead of `== null` or `!= null`.
* Trust the C# null annotations and don't add null checks when the type system says a value cannot be null.

### Building 

#### Build Commands

- **Build**: `dotnet build`
- **Start**: `dotnet run`

### Testing
* Do not emit "Act", "Arrange" or "Assert" comments.
* We do not use any mocking framework at the moment.
* Copy existing style in nearby files for test method names and capitalization.
* Do not leave newly-added tests commented out. All added tests should be building and passing.
* Do not use Directory.SetCurrentDirectory in tests as it can cause side effects when tests execute concurrently.
* When adding new unit tests, strongly prefer to add them to existing test code files rather than creating new code files.
* When running tests, if possible use filters and check test run counts, or look at test logs, to ensure they actually ran.
* Do not finish work with any tests commented out or disabled that were not previously commented out or disabled.

## Running tests

- **Test**: `dotnet test`

## Project Layout and Architecture

### Directory Structure
- **`/src`**: Main source code for all Aspire packages
  - `Vertr.Market.Host/`: ASP.NET Web API Host
- **`/tests`**: Comprehensive test suites mirroring src structure
- **`/docs`**: Documentation including contributing guides and area ownership

### Key Configuration Files
- **`.editorconfig`**: Code formatting rules, null annotations, diagnostic configurations
- **`Directory.Build.props`**: Shared MSBuild properties across all projects
- **`Market.slnx`**: Main solution file (XML-based solution format)

## Markdown files
* Markdown files should not have multiple consecutive blank lines.
* Code blocks should be formatted with triple backticks (```) and include the language identifier for syntax highlighting.
* JSON code blocks should be indented properly.

## Available Skills

The following specialized skills are available in `agents/skills/`:

- **modern-csharp**: Modern C# language features for .NET 10 and C# 14.
- **clean-architecture**: Clean Architecture for .NET applications.
- **configuration**: Configuration patterns for .NET 10 applications.
- **convention-learner**: Detects and enforces project-specific coding conventions by analyzing existing codebase patterns.

