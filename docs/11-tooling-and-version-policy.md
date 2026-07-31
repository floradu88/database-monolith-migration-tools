# Tooling and Version Policy

## SQL projects

Use SDK-style SQL projects with `Microsoft.Build.Sql` where the selected development and CI tooling supports them. Pin the SDK centrally in `global.json` or an approved dependency-management mechanism rather than scattering preview versions through project files.

The project templates intentionally omit an explicit SDK package version. The implementation repository must pin and validate one approved version before first build.

## EF Core migrations

A separate migrations project must:

- reference the project containing the `DbContext`;
- configure the migrations assembly;
- contain an initial migration/model snapshot or follow the documented bootstrap process;
- use an explicit startup project for tooling;
- generate reviewed, idempotent production scripts where required.

## Package policy

- pin package versions centrally;
- run vulnerability and license scans;
- upgrade through pull requests;
- validate SQL generation and migration scripts after upgrades;
- never assume template versions are production-approved.
