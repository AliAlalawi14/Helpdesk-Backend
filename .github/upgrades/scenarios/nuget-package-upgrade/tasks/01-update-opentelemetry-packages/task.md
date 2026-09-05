# 01-update-opentelemetry-packages: Update OpenTelemetry package versions

## Objective
Update the vulnerable OpenTelemetry exporter through Central Package Management and verify the transitive `OpenTelemetry.Api` resolution for the `net10.0` backend.

## Research findings
- Affected project: `backend/backend/backend.csproj`.
- Central Package Management is enabled by `Directory.Packages.props` (`ManagePackageVersionsCentrally=true`).
- `backend.csproj` references `OpenTelemetry.Exporter.OpenTelemetryProtocol` without a version; its version is centrally defined in `Directory.Packages.props`.
- The assessment recommends `OpenTelemetry.Exporter.OpenTelemetryProtocol` 1.18.0, unified for the scoped project, with no detected source-breaking API changes.
- `OpenTelemetry.Api` has no direct scoped `PackageReference`; it is transitive and must be verified after the exporter update.

## Scope inventory
- Project: `backend/backend/backend.csproj`.
- Concern: central OpenTelemetry package version management and restore vulnerability validation.

## Done when
- `Directory.Packages.props` uses exporter version 1.18.0.
- Restore completes without NU1902 vulnerability errors for the scoped project.
- The backend project builds without package-related errors.
