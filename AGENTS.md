# Repository Guidelines

## Project Structure & Module Organization
The WPF app targets `net8.0-windows` and compiles to `RequiemGlamPatcher.exe`. UI markup lives in `App.xaml` and `Views/`, with `ViewModels/` (e.g., `MainViewModel`) implementing ReactiveUI presentation logic. Domain models in `Models/` describe armor matches and patcher settings, while `Services/` houses the Mutagen-backed discovery, matching, and ESP generation workflows behind interfaces for easy mocking. `Utilities/InverseBooleanConverter.cs` provides shared XAML helpers. Build outputs reside in `bin/` and `obj/`; keep edits focused on source folders.

## Build, Test, and Development Commands
- `dotnet restore` downloads Mutagen, ReactiveUI, and other NuGet dependencies.
- `dotnet build -c Release` validates the codebase and emits binaries under `bin/Release`.
- `dotnet run` launches the desktop app using the default Debug profile for manual smoke tests.
- `dotnet publish -c Release -r win-x64 /p:PublishSingleFile=true` produces a distributable single-file build.

## Coding Style & Naming Conventions
Use 4-space indentation and standard C# casing: PascalCase for types and public members, camelCase for locals, and `_camelCase` for private fields (see `ViewModels/MainViewModel.cs`). Respect nullable annotations (`<Nullable>enable</Nullable>` in the project file) and favor explicit types when they aid readability. Group related logic into services and avoid view model code-behind except for trivial event wiring. Run `dotnet format` before pushing to keep spacing, ordering, and analyzers aligned with SDK defaults.

## Testing Guidelines
Automated tests are not yet checked in. When adding behavior, create an `RequiemGlamPatcher.Tests` xUnit project and wire it with `dotnet test`. Name files `<Feature>Tests.cs` and follow the `MethodUnderTest_ShouldExpectedBehavior` pattern for clarity. Until a suite exists, perform manual verification: run `dotnet run`, load representative source and target ESPs, generate a patch, and confirm the resulting ESP behaves as expected inside Mod Organizer 2.

## Commit & Pull Request Guidelines
History currently contains the bootstrap commit, so adopt a conventional-commit prefix (`feat:`, `fix:`, `refactor:`) to improve traceability. Keep commits small, buildable, and accompanied by rationale in the body when needed. For pull requests, include a concise summary of changes, testing notes (commands executed, sample ESP checks), references to issues or feature requests, and screenshots or GIFs whenever UI elements change. Ensure Release builds succeed before requesting review.

## Configuration Notes
Develop and validate on Windows with Skyrim Special Edition data and Mod Organizer 2 available to exercise auto-detection logic. Do not commit artifacts from `bin/` or `obj/`; rely on `dotnet clean` for resets. Update `README.md` and in-app messaging when workflows or configuration steps shift.
