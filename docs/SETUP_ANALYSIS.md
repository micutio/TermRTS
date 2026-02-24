# TermRTS – Setup analysis and best-practice recommendations

This document summarizes the current setup and suggests changes aligned with modern C# and .NET practices for a terminal-based solution (engine, tests, examples).

---

## 1. Current structure

- **Solution**: Single `TermRTS.sln` with three projects:
  - **TermRTS** – game engine library (SDK-style, `OutputType` Library)
  - **TermRTS.Test** – xUnit tests
  - **TermRTS.Examples** – executable examples (Console app)
- **Target framework**: All projects use `net10.0`.
- **Tooling**: `.editorconfig` with C# and ReSharper-style rules; Dependabot for NuGet.

---

## 2. Issues and recommendations

### 2.1 CI workflow (critical)

- **Issue**: Workflow uses `TermRTS.slnx`; the actual solution is `TermRTS.sln`, so restore/build/test would fail.
- **Issue**: `dotnet-version: 9.0.x` does not match project `TargetFramework` `net10.0`.
- **Recommendation**:
  - Use `TermRTS.sln` in all `dotnet` commands.
  - Set `dotnet-version` to match your target (e.g. `10.0.x` if you stay on .NET 10, or `9.0.x` and change projects to `net9.0` for wider compatibility).

### 2.2 Centralized build properties

- **Issue**: `TargetFramework`, `Nullable`, `ImplicitUsings`, and other shared settings are repeated in each `.csproj`.
- **Recommendation**: Add a **`Directory.Build.props`** at the repo root to define:
  - `TargetFramework`
  - `Nullable`, `ImplicitUsings`
  - `LangVersion` (e.g. `latest` or `12`)
  - `TreatWarningsAsErrors` or `EnforceCodeStyleInBuild` if you want strict builds
  - Optional: `AnalysisLevel` (e.g. `latest-all` or `latest`) for .NET analyzers  
  Then keep only project-specific properties in each `.csproj` (e.g. `OutputType`, `IsTestProject`, `IsPackable`).

### 2.3 SDK and runtime version (reproducibility)

- **Issue**: No `global.json`; builds use whatever SDK is installed.
- **Recommendation**: Add **`global.json`** with a `sdk.version` (e.g. `10.0.1xx` or `9.0.3xx`) and optionally `rollForward` (e.g. `latestFeature`) so all developers and CI use a consistent SDK band.

### 2.4 TermRTS (engine) project

- **Issue**: Duplicate log4net reference:
  - A legacy `<Reference Include="log4net" HintPath="..\..\..\.nuget\packages\...">` pointing to a netstandard2.0 path.
  - A `<PackageReference Include="log4net" Version="3.3.0" />`.
- **Recommendation**: Remove the `<Reference>` block and keep only the **PackageReference**. SDK-style projects resolve packages via NuGet; the `HintPath` is brittle and unnecessary.

### 2.5 TermRTS.Test project

- **Issue**: References **TermRTS.Examples** in addition to **TermRTS**. The test code only uses types from `TermRTS` (e.g. `TermRTS.Algorithms`, `TermRTS.Event`, `TermRTS.Serialization`); no usage of Examples was found.
- **Recommendation**: Remove the **ProjectReference** to `TermRTS.Examples`. Test projects should reference only what they need (the engine and test/utility libraries). If you later add integration tests that run example scenarios, you can introduce a separate project (e.g. `TermRTS.IntegrationTests`) that references Examples.

### 2.6 Solution file consistency

- **Issue**: TermRTS and TermRTS.Test use the SDK-style project GUID `{9A19103F-16F7-4668-BE54-9A1E7A4F7556}`; TermRTS.Examples uses the older `{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}`.
- **Recommendation**: Use the same SDK-style GUID for **TermRTS.Examples** so all projects are clearly SDK-style. This is cosmetic but keeps the solution consistent.

### 2.7 TermRTS.Examples project file layout

- **Issue**: `PropertyGroup` (with `OutputType`, `TargetFramework`, etc.) appears after `ItemGroup`s; the usual convention is `PropertyGroup` first.
- **Recommendation**: Move the single `PropertyGroup` to the top of the file for readability and consistency with the other projects.

### 2.8 Target framework and compatibility

- **Current**: `net10.0` (cutting-edge; .NET 10 may still be preview depending on timing).
- **Recommendation**:
  - If you want maximum compatibility (e.g. typical CI runners, other machines): consider **`net8.0`** (LTS) or **`net9.0`** and align CI `dotnet-version` and `global.json` accordingly.
  - If you intentionally target .NET 10: keep `net10.0`, set CI and `global.json` to a 10.0.x SDK, and document the requirement in the README.

### 2.9 .editorconfig

- **Current**: Solid C# and naming rules; `trim_trailing_whitespace = false` and `insert_final_newline = false`.
- **Recommendation**: For consistency and cleaner diffs, consider `trim_trailing_whitespace = true` and `insert_final_newline = true` (common in modern C# repos).

### 2.10 NuGet

- **Current**: No repo-level `NuGet.config`; Dependabot runs weekly for NuGet.
- **Recommendation**: Add a root **`NuGet.config`** only if you need custom feeds or restore behavior. Otherwise the default is fine. If you add one, consider `clear` and then explicit `packageSource` entries so behavior is explicit.

### 2.11 .gitignore

- **Current**: Comprehensive Visual Studio / .NET ignore list including `bin/`, `obj/`, coverage files, etc.
- **Recommendation**: No change required; optionally ensure `global.json` and `Directory.Build.props` are **not** ignored (they should not be by default).

---

## 3. Summary of suggested changes (checklist)

| Area              | Action |
|-------------------|--------|
| CI                | Use `TermRTS.sln` and align `dotnet-version` with `TargetFramework` (e.g. 10.0.x or 9.0.x). |
| Build             | Add `Directory.Build.props` with shared `TargetFramework`, `Nullable`, `ImplicitUsings`, etc. |
| Reproducibility   | Add `global.json` with `sdk.version` (and optional `rollForward`). |
| TermRTS.csproj    | Remove legacy `<Reference>` for log4net; keep only `PackageReference`. |
| TermRTS.Test      | Remove `ProjectReference` to TermRTS.Examples. |
| TermRTS.sln       | Use SDK-style project GUID for TermRTS.Examples. |
| TermRTS.Examples  | Move `PropertyGroup` to the top of the `.csproj`. |
| .editorconfig     | Optionally set `trim_trailing_whitespace = true`, `insert_final_newline = true`. |
| Docs              | In README, state required .NET SDK version (e.g. “.NET 10 SDK” or “.NET 9 SDK”). |

---

## 4. Terminal-specific notes

For a terminal/console-focused setup you already have:

- A library (TermRTS) with no UI dependency.
- An executable (Examples) that can use `ConsoleRenderer` and similar.
- Tests that don’t depend on the examples project.

Keeping the engine as a pure library and examples as a thin console host is a good separation. If you add more examples or a single “main” CLI, consider:

- One entry-point project (e.g. `TermRTS.Cli` or keep `TermRTS.Examples`) that parses subcommands or example indices and runs the right scenario.
- Optional use of `System.Console` APIs or a small CLI library (e.g. `System.CommandLine`) for argument parsing and help, without tying the engine to a specific UI.

This analysis focuses on setup and structure; the above checklist and sections give you a path to align the repo with modern C# and terminal-based program best practices.
