# 3. Discovery, Repository Parsing, and AI Indexing

## Hybrid discovery model

Do not use embeddings or an LLM as the exact dependency engine. Combine:

1. compiler/AST parsing;
2. T-SQL parsing;
3. exact symbol and string matching;
4. SQL Server metadata;
5. runtime observations;
6. semantic indexing and AI classification;
7. human ownership approval.

## Repository scanner

For .NET repositories detect:

- `SqlConnection`, `SqlCommand`, `CommandType.StoredProcedure`;
- Dapper `Query*`, `Execute*`, `QueryMultiple*`;
- EF Core `FromSqlRaw`, `FromSqlInterpolated`, `ExecuteSqlRaw*`;
- EF mappings to stored procedures/functions;
- database contexts and migration projects;
- wrappers and repository abstractions;
- embedded SQL, `.sql` resources, and deployment scripts;
- connection strings, database names, schemas, and application names;
- dynamic SQL construction and string concatenation;
- scheduled/background jobs and report exporters.

Each finding records repository, commit, project, file, member, line, raw reference, normalized database object, access type, dynamic flag, and confidence.

## SQL scanner

Export and parse:

- tables, columns, keys, constraints;
- procedures, scalar functions, inline TVFs, multi-statement TVFs;
- views, triggers, synonyms;
- Agent jobs and job steps;
- permissions and role membership;
- SQL modules and definitions;
- SQL expression dependencies;
- cross-database three-part names, linked servers, OPENQUERY, and EXECUTE AS;
- dynamic SQL (`EXEC(...)`, `sp_executesql`);
- transactions and side effects.

## Evidence graph

Nodes:

```text
Application, repository, project, code member, identity, database,
schema, table, column, procedure, function, view, trigger, job, report,
business capability, target service, migration wave
```

Edges:

```text
CALLS, READS, WRITES, EXECUTES, DEPENDS_ON, OWNS, AUTHENTICATES_AS,
SCHEDULES, PUBLISHES, CONSUMES, MIGRATES_TO, REPLACED_BY
```

## AI use cases

AI may:

- summarize procedure/function behavior;
- propose business capability and target namespace;
- identify duplicate or near-duplicate SQL logic;
- propose API operations and events;
- detect cross-domain transaction candidates;
- draft migration manifests and tests;
- explain blockers and conflicting ownership evidence.

Every result must contain confidence, supporting evidence, conflicting evidence, and a required reviewer.

AI must not autonomously delete objects, alter permissions, execute production migrations, or make final ownership decisions.

## Ownership scoring

A practical weighted model:

```text
35% authoritative write activity
20% source-code references
15% runtime call frequency
10% business vocabulary alignment
10% incident/data-quality responsibility
10% team approval signal
```

Read frequency alone does not establish ownership.
