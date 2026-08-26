# 01-upgrade-footballmanagementengine: Upgrade FootballManagementEngine

Start task `01-upgrade-footballmanagementengine`.

## Scope Inventory
- Projects affected: FootballManagementEngine.csproj
- Files to modify: FootballManagementEngine.csproj
- Known assessment issues: Project.0002 — target framework change required

## Findings
- Current TargetFramework in project file: net8.0
- No PackageReference entries found in the project file; packages may be defined centrally or in other projects.
- Project appears SDK-style and suitable for `dotnet build`.

## Plan
1. Change TargetFramework to `net10.0` in the project file.
2. Restore and build the project; fix any package or API issues.
3. Run solution-level validation and tests (if present).

2. Inspect FootballManagementEngine.csproj and list packages that may need updates.
3. Update the project's TargetFramework to `net10.0`.
4. Build the solution and fix any compile/package issues and warnings.
5. Run tests for affected projects and fix test failures.
6. Write progress-details.md and complete the task.
