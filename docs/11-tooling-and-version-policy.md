# Tooling and Version Policy

## SQL projects

Use SDK-style SQL projects with `Microsoft.Build.Sql` where the selected development and CI tooling supports them. Pin the SDK centrally in `global.json` (`msbuild-sdks.Microsoft.Build.Sql`, currently `2.3.0`) rather than scattering preview versions through project files. Build dacpacs with `tools/dacpac/Invoke-DacpacReady.ps1` or `MigrationTool.Host --build-dacpac`.

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
