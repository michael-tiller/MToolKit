# Changelog

All notable changes to MToolKit will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [0.5.0] - 2026-05-03

### Added
- **Startup profiling system**: `IStartupProfiler` interface and `StartupProfiling` facade for timing plugin initialization phases. Game-level profiler can be injected to capture detailed startup metrics.
- **Async plugin initialization**: `PerformRuntimeInitializationAsync` in `AbstractRuntimePlugin` and corresponding async path in `PluginRegistry` for plugins requiring async runtime work before scene transitions.
- **Slog enrichers**: New logging enrichers for improved log output:
  - `ShortSourceContextEnricher` - shortens fully-qualified type names and extracts namespaces
  - `MethodFromStackEnricher` - captures method names from call stack
  - `RenamePropertyEnricher` - renames log properties for output formatting
  - `RenamingCompactJsonFormatter` - compact JSON formatter with property renaming
- **StartupFlowState**: Static carrier for tracking whether startup is NewGame vs Continue flow.
- **IDomainMessage interface**: New message bus abstraction for domain-scoped messages.
- **VisualGraphs Event system**: `EventDefinition` and `EventGraphAsset` for event-driven graph authoring.
- **MasterPackageId config**: New mod settings configuration for master package loading.
- **VS Code workspace file**: `MToolKit.code-workspace` for editor setup.

### Changed
- **Log verbosity reduction**: Adjusted many Information/Debug logs to Verbose across plugins to reduce log noise during normal operation. Critical paths remain at appropriate levels.
- **Auto-refresh defaults**: Diagnostic windows (`PluginDiagnosisWindow`, `QuestManagerDiagnosticWindow`) now default to `autoRefresh = false` to reduce editor overhead.
- **PluginRegistry logging**: Uses cached logger instance instead of creating per-call.

### Fixed
- Various log message template fixes (using structured logging placeholders consistently).

## [0.4.2] - Previous Release

See git history for earlier changes.
