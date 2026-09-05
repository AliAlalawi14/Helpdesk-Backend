# NuGet Package Upgrade

## Strategy
Use Central Package Management for the scoped backend project, upgrade the exporter to the assessed stable version, and resolve the compile error with the provider-supported migrations history table name.

## Preferences
- **Flow Mode**: Automatic
- **Commit Strategy**: Manual (no Git repository detected)
- **Pace**: Standard
- **Scope**: `backend/backend/backend.csproj`
- **Packages**: `OpenTelemetry.Api`, `OpenTelemetry.Exporter.OpenTelemetryProtocol`
- **Version Policy**: Latest stable versions compatible with `net10.0`
- **Assessment Scan**: Quick assessment; full source scan was not requested

## Decisions
- **OpenTelemetry.Exporter.OpenTelemetryProtocol**: Upgrade from 1.11.1 to 1.18.0, unified for the scoped project.
- **OpenTelemetry.Api**: No direct scoped reference or supported standalone target was found; validate its resolved transitive version after the exporter upgrade.
- **Package management**: Update `Directory.Packages.props` because Central Package Management is enabled.
- **History repository configuration**: Replace the undefined `HistoryRepository` symbol with the standard EF Core migrations history table name.

## User Preferences
### Technical Preferences
- **OpenTelemetry packages**: Resolve the reported vulnerabilities by upgrading to the latest stable compatible versions.

### Execution Style
- **Flow**: Proceed automatically after confirmation; pause only if blocked.
