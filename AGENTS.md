# Repository Guidelines

## Project Structure & Module Organization
- `CraftimizerCN/`: main Dalamud plugin code (UI windows in `Windows/`, helpers in `Utils/`, graphics in `Graphics/`).
- `Simulator/`: deterministic crafting simulation engine and action implementations (`Actions/`).
- `Solver/`: search and MCTS solver logic built on top of `Simulator`.
- `Test/`: MSTest suite for simulator/solver behavior.
- `Benchmark/`: performance benchmarking harness.
- `.github/workflows/build.yml`: CI build/release pipeline.

## Build, Test, and Development Commands
- `dotnet restore -r win`: restore packages for the Windows target runtime used by the plugin.
- `dotnet build CraftimizerCN.sln -c Debug`: local development build.
- `dotnet build CraftimizerCN.sln -c Release`: release-equivalent build.
- `dotnet test Test/CraftimizerCN.Test.csproj -c Release`: run unit tests.
- `dotnet run --project Benchmark/CraftimizerCN.Benchmark.csproj -c Release`: run benchmarks.

## Coding Style & Naming Conventions
- Follow `.editorconfig`: UTF-8, LF line endings, 4-space indentation, and explicit accessibility on non-interface members.
- C# conventions in this repo:
  - `PascalCase` for types/methods/properties.
  - private instance fields use `camelCase`; private static fields/readonly fields use `PascalCase`.
  - events are `OnPascalCase`.
- Keep namespaces file-scoped and aligned with folders where possible.

## Testing Guidelines
- Framework: MSTest (`Microsoft.NET.Test.Sdk`, `MSTest.TestFramework`).
- Place tests under `Test/` mirroring source areas (for example `Test/Simulator/`, `Test/Solver/`).
- Name test files and methods by behavior (example: `Simulator_StopsWhenDurabilityZero`).
- Add/adjust tests for every solver or simulator logic change before opening a PR.

## Commit & Pull Request Guidelines
- Commit style in history is short, imperative subjects (for example `Remove redundant check`, `Update manifest version`), optionally prefixed with version bumps.
- Keep commits focused to one change area (simulator, solver, UI, or tooling).
- PRs should include:
  - clear summary of functional impact,
  - linked issue (if applicable),
  - screenshots/GIFs for UI changes under `CraftimizerCN/Windows/`,
  - test evidence (`dotnet test` output or benchmark notes when performance-sensitive).

## Environment & Release Notes
- CI currently targets `.NET 10.0` and packages from `CraftimizerCN/bin/x64/Release/latest.zip`.
- Local plugin testing requires a valid `DALAMUD_HOME` setup compatible with your XIVLauncher/Dalamud installation.
