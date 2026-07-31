using DbIntelligence.Domain;
using Microsoft.Data.SqlClient;

namespace DbIntelligence.SqlScanner;

public sealed class SqlScannerService
{
    private const string ObjectsSql = """
        SELECT
            DB_NAME() AS DatabaseName,
            s.name AS SchemaName,
            o.name AS ObjectName,
            o.type_desc AS TypeDesc
        FROM sys.objects AS o
        JOIN sys.schemas AS s ON s.schema_id = o.schema_id
        WHERE o.is_ms_shipped = 0
          AND o.type IN ('P','PC','FN','FS','FT','IF','TF','V','TR','U');
        """;

    private const string DependenciesSql = """
        SELECT
            OBJECT_SCHEMA_NAME(d.referencing_id) AS ReferencingSchema,
            OBJECT_NAME(d.referencing_id) AS ReferencingObject,
            ro.type_desc AS ReferencingType,
            COALESCE(d.referenced_schema_name, 'dbo') AS ReferencedSchema,
            d.referenced_entity_name AS ReferencedObject,
            d.is_ambiguous AS IsAmbiguous
        FROM sys.sql_expression_dependencies AS d
        LEFT JOIN sys.objects AS ro ON ro.object_id = d.referencing_id
        WHERE d.referenced_entity_name IS NOT NULL;
        """;

    public async Task<EvidenceGraph> ScanAsync(
        string connectionString,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new ArgumentException("SQL connection string is required for SqlScanner.", nameof(connectionString));

        var graph = new EvidenceGraph();
        graph.Meta.Sources.Add("sql-scanner");

        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);

        await using (var cmd = new SqlCommand(ObjectsSql, connection))
        await using (var reader = await cmd.ExecuteReaderAsync(cancellationToken))
        {
            while (await reader.ReadAsync(cancellationToken))
            {
                var database = reader.GetString(0);
                var schema = reader.GetString(1);
                var name = reader.GetString(2);
                var typeDesc = reader.GetString(3);
                var kind = MapKind(typeDesc);
                var id = GraphIds.DbObject(database, schema, name, kind);
                graph.UpsertNode(new GraphNode
                {
                    Id = id,
                    Label = $"{schema}.{name}",
                    Kind = kind,
                    Schema = schema,
                    Database = database,
                    Community = "sql-inventory"
                });
            }
        }

        await using (var cmd = new SqlCommand(DependenciesSql, connection))
        await using (var reader = await cmd.ExecuteReaderAsync(cancellationToken))
        {
            while (await reader.ReadAsync(cancellationToken))
            {
                if (reader.IsDBNull(1) || reader.IsDBNull(4))
                    continue;

                var refSchema = reader.IsDBNull(0) ? "dbo" : reader.GetString(0);
                var refName = reader.GetString(1);
                var refType = reader.IsDBNull(2) ? "SQL_STORED_PROCEDURE" : reader.GetString(2);
                var depSchema = reader.IsDBNull(3) ? "dbo" : reader.GetString(3);
                var depName = reader.GetString(4);
                var ambiguous = !reader.IsDBNull(5) && reader.GetBoolean(5);

                var sourceKind = MapKind(refType);
                var targetKind = GuessKindFromName(depName);
                var database = connection.Database;

                var sourceId = GraphIds.DbObject(database, refSchema, refName, sourceKind);
                var targetId = GraphIds.DbObject(database, depSchema, depName, targetKind);

                graph.UpsertNode(new GraphNode
                {
                    Id = sourceId,
                    Label = $"{refSchema}.{refName}",
                    Kind = sourceKind,
                    Schema = refSchema,
                    Database = database
                });
                graph.UpsertNode(new GraphNode
                {
                    Id = targetId,
                    Label = $"{depSchema}.{depName}",
                    Kind = targetKind,
                    Schema = depSchema,
                    Database = database
                });

                graph.UpsertEdge(new GraphEdge
                {
                    Source = sourceId,
                    Target = targetId,
                    Relation = EdgeRelation.DependsOn,
                    Confidence = ambiguous ? Confidence.Ambiguous : Confidence.Extracted,
                    Evidence = new EdgeEvidence
                    {
                        Pattern = "sys.sql_expression_dependencies",
                        RawReference = $"{refSchema}.{refName} -> {depSchema}.{depName}"
                    }
                });
            }
        }

        return graph;
    }

    /// <summary>
    /// Offline inventory from a simple JSON/CSV-free in-memory list — used by tests and demos without SQL Server.
    /// </summary>
    public EvidenceGraph FromInventory(IEnumerable<SqlObjectRecord> objects, IEnumerable<SqlDependencyRecord>? dependencies = null)
    {
        var graph = new EvidenceGraph();
        graph.Meta.Sources.Add("sql-scanner");

        foreach (var obj in objects)
        {
            var kind = MapKind(obj.TypeDesc);
            graph.UpsertNode(new GraphNode
            {
                Id = GraphIds.DbObject(obj.Database, obj.Schema, obj.Name, kind),
                Label = $"{obj.Schema}.{obj.Name}",
                Kind = kind,
                Schema = obj.Schema,
                Database = obj.Database,
                Community = "sql-inventory"
            });
        }

        if (dependencies is not null)
        {
            foreach (var dep in dependencies)
            {
                var sourceKind = MapKind(dep.ReferencingType);
                var targetKind = GuessKindFromName(dep.ReferencedObject);
                var sourceId = GraphIds.DbObject(dep.Database, dep.ReferencingSchema, dep.ReferencingObject, sourceKind);
                var targetId = GraphIds.DbObject(dep.Database, dep.ReferencedSchema, dep.ReferencedObject, targetKind);
                graph.UpsertEdge(new GraphEdge
                {
                    Source = sourceId,
                    Target = targetId,
                    Relation = EdgeRelation.DependsOn,
                    Confidence = dep.IsAmbiguous ? Confidence.Ambiguous : Confidence.Extracted
                });
            }
        }

        return graph;
    }

    private static NodeKind MapKind(string typeDesc) => typeDesc.ToUpperInvariant() switch
    {
        "SQL_STORED_PROCEDURE" or "CLR_STORED_PROCEDURE" or "P" => NodeKind.StoredProcedure,
        "VIEW" or "V" => NodeKind.View,
        "SQL_TRIGGER" or "CLR_TRIGGER" or "TR" => NodeKind.Trigger,
        "USER_TABLE" or "U" => NodeKind.Table,
        "SQL_SCALAR_FUNCTION" or "SQL_TABLE_VALUED_FUNCTION" or "SQL_INLINE_TABLE_VALUED_FUNCTION"
            or "CLR_SCALAR_FUNCTION" or "CLR_TABLE_VALUED_FUNCTION" or "FN" or "IF" or "TF" or "FS" or "FT"
            => NodeKind.Function,
        _ => NodeKind.Concept
    };

    private static NodeKind GuessKindFromName(string name)
    {
        if (name.StartsWith("usp_", StringComparison.OrdinalIgnoreCase) ||
            name.StartsWith("sp_", StringComparison.OrdinalIgnoreCase))
            return NodeKind.StoredProcedure;
        if (name.StartsWith("fn_", StringComparison.OrdinalIgnoreCase))
            return NodeKind.Function;
        if (name.StartsWith("vw_", StringComparison.OrdinalIgnoreCase))
            return NodeKind.View;
        return NodeKind.Table;
    }
}

public sealed record SqlObjectRecord(string Database, string Schema, string Name, string TypeDesc);

public sealed record SqlDependencyRecord(
    string Database,
    string ReferencingSchema,
    string ReferencingObject,
    string ReferencingType,
    string ReferencedSchema,
    string ReferencedObject,
    bool IsAmbiguous);
