# DbIntelligence.RepositoryScanner

Roslyn-based scan of a repository folder for SQL / stored-procedure usage. Emits code→DB and SP evidence edges (including `AMBIGUOUS` for dynamic SQL).

Used during index jobs after Codegraph/Graphify. Does not require Node/npm itself — those are for the Angular UI and Codegraph CLI install.

Parent how-to: [`../README.md`](../README.md) · [`../../../HOW-TO-USE.md`](../../../HOW-TO-USE.md).
