# NuGet Package Upgrade Plan

## Overview

**Target**: Remove the reported OpenTelemetry package vulnerabilities and restore the `net10.0` backend build.
**Scope**: One backend project using Central Package Management, plus one compile-time configuration fix.

## Tasks

### 01-update-opentelemetry-packages: Update OpenTelemetry package versions

Update the centrally managed `OpenTelemetry.Exporter.OpenTelemetryProtocol` reference in `Directory.Packages.props` from 1.11.1 to the assessed stable version 1.18.0. Restore the project and confirm the transitive `OpenTelemetry.Api` version is no longer the vulnerable 1.11.1 release. The assessment API diff reports no source-breaking changes for the exporter upgrade.

**Done when**: Central package version is updated, restore completes without NU1902 vulnerability errors, and the backend project builds without package-related errors.

---

### 02-fix-history-repository: Fix migrations history table configuration

Replace the undefined `HistoryRepository` reference in `backend/Program.cs` with the standard EF Core migrations history table name while preserving the configured application schema. This keeps the existing PostgreSQL migration behavior without depending on an unavailable internal type.

**Done when**: `Program.cs` compiles, the backend project builds with zero errors and warnings, and any available tests pass.
