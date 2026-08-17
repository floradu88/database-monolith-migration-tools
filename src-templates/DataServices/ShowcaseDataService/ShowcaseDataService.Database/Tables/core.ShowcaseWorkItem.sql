-- Ownership: SqlProject (ShowcaseDataService.Database)
-- Owned clone of dbo.ShowcaseWorkItem. SP writes only — no historical backfill, no EF/job DML.

CREATE TABLE [core].[ShowcaseWorkItem]
(
    [ExternalId] uniqueidentifier NOT NULL CONSTRAINT [PK_core_ShowcaseWorkItem] PRIMARY KEY,
    [Name] nvarchar(200) NOT NULL,
    [Status] nvarchar(50) NOT NULL,
    [UpdatedAt] datetime2(3) NOT NULL CONSTRAINT [DF_core_ShowcaseWorkItem_UpdatedAt] DEFAULT SYSUTCDATETIME()
);
GO
