# Plan — Upgrade FootballManagementEngine to .NET 10

## Goal
Upgrade FootballManagementEngine.csproj from net8.0 to net10.0 and ensure the solution builds warning-free and tests pass.

## Steps
1. Start task `01-upgrade-footballmanagementengine`.
2. Inspect FootballManagementEngine.csproj and list packages that may need updates.
3. Update the project's TargetFramework to `net10.0`.
4. Build the solution and fix any compile/package issues and warnings.
5. Run tests for affected projects and fix test failures.
6. Write progress-details.md and complete the task.

## Notes
- No git repository detected; changes will be made directly in the workspace.
- Target framework chosen: net10.0 (LTS).
