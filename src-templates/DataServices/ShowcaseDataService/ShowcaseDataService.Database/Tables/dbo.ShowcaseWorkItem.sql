-- Ownership: SqlProject. Legacy write table for dbo → core parallel-write demo (same database).
-- Not EF-owned. core receives SP writes only; this table may also receive EF/jobs/ad-hoc SQL.

CREATE TABLE [dbo].[ShowcaseWorkItem]
(
    [ExternalId] uniqueidentifier NOT NULL CONSTRAINT [PK_dbo_ShowcaseWorkItem] PRIMARY KEY,
    [Name] nvarchar(200) NOT NULL,
    [Status] nvarchar(50) NOT NULL,
    [UpdatedAt] datetime2(3) NOT NULL CONSTRAINT [DF_dbo_ShowcaseWorkItem_UpdatedAt] DEFAULT SYSUTCDATETIME()
);
GO
