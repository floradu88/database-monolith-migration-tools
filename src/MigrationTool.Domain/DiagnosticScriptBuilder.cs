namespace MigrationTool.Domain;

public static class DiagnosticScriptBuilder
{
    public static string Build(DatabaseProviderKind provider, string schema, string table)
    {
        var schemaLiteral = SqlIdentifier.Literal(provider, schema);
        var tableLiteral = SqlIdentifier.Literal(provider, table);

        if (provider == DatabaseProviderKind.PostgreSql)
        {
            return $"""
                SELECT table_schema, table_name, column_name, data_type, character_maximum_length, numeric_precision, numeric_scale, is_nullable
                FROM information_schema.columns
                WHERE table_schema = {schemaLiteral} AND table_name = {tableLiteral}
                ORDER BY ordinal_position;

                SELECT c.relname AS table_name, c.reltuples::bigint AS estimated_rows
                FROM pg_class c
                JOIN pg_namespace n ON n.oid = c.relnamespace
                WHERE n.nspname = {schemaLiteral} AND c.relname = {tableLiteral};
                """;
        }

        return $"""
            SELECT TABLE_SCHEMA, TABLE_NAME, COLUMN_NAME, DATA_TYPE, CHARACTER_MAXIMUM_LENGTH, NUMERIC_PRECISION, NUMERIC_SCALE, IS_NULLABLE
            FROM INFORMATION_SCHEMA.COLUMNS
            WHERE TABLE_SCHEMA = {schemaLiteral} AND TABLE_NAME = {tableLiteral}
            ORDER BY ORDINAL_POSITION;

            SELECT t.name AS table_name, SUM(p.rows) AS row_count
            FROM sys.tables t
            INNER JOIN sys.schemas s ON s.schema_id = t.schema_id
            INNER JOIN sys.partitions p ON p.object_id = t.object_id AND p.index_id IN (0, 1)
            WHERE s.name = {schemaLiteral} AND t.name = {tableLiteral}
            GROUP BY t.name;
            """;
    }
}
