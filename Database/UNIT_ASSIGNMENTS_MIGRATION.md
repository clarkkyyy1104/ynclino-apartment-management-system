# Removing TenantProfiles.UnitID

The application now obtains the current unit from the tenant's Active row in
TenantUnitAssignments. No active row means no current unit. Ended rows are kept
for history; they never count toward occupancy. The C# UnitID property remains
as an unmapped form/edit convenience, not a database column.

## Existing localhost database

1. Stop all running copies of the app (including older builds).
2. Back up YAMSDB, including its data.
3. Run `Database/remove_tenantprofiles_unitid.sql` in MySQL Workbench.
   It refuses to drop the column if current assignments disagree with profiles,
   an inactive tenant's latest unit would be lost, or the unique active-assignment
   index is missing. Do not bypass these checks; inspect/reconcile the affected
   records first. If the procedure call fails and your client stops, follow the
   helper-procedure cleanup comment at the end of the script before rerunning.
4. Start the updated application. Verify registration, first-unit approval,
   transfer, tenant details, unit occupancy, billing, reports, maintenance,
   archiving and account reactivation.

Do **not** run `ynclino_schema.sql` against an existing database: that file drops
and recreates YAMSDB. It has been updated only for fresh installations.

Do not use the old app after migration. Conversely, do not make assignments with
the new app before migration: it no longer maintains the old profile column.
For rollback, stop the app and restore the backup together with the old build.
MySQL schema alterations cannot be undone with a transaction rollback.

## Behavior preserved and clarified

- Assignment changes close the previous row before inserting its replacement,
  in one transaction, preserving the unique active-assignment guard.
- Registration without a unit is still supported.
- Archiving closes occupancy and updates the former unit's availability.
- Reactivation finds the latest unit in history and starts a fresh assignment;
  it refuses if that unit is now full or under maintenance.
- Move-out deposit settlement receives the former unit explicitly, so it does
  not rely on a current assignment after that assignment has ended.
- Maintenance and transfer snapshot UnitIDs are unchanged.
- Archived tenants have no current unit. Their former units remain in assignment
  history, rather than being presented as current occupants.

## Regression checks

Run `dotnet run --project Tests/AssignmentRegression -c Release`.
These tests use an in-memory SQLite database and compile MySQL queries without
connecting to a server. They do not read or modify your localhost database.

Optional read-only localhost preflight (from the repository root):
`dotnet run --project Tests/AssignmentRegression -c Release -- --check-localhost`
This reads the configured local database, reports mismatch counts, and checks
production-provider queries. It does not run the migration or change data.
