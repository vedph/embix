# History

## 5.0.0

- 2026-10-29: upgraded to NET 10.
- 2026-10-29: modernization and optimization (breaking changes):
  - indexing no longer depends on SqlKata: `SqlIndexWriter` and `IndexBuilderFactory` no longer take a SqlKata `Compiler`. Database-specific factories override `IndexBuilderFactory.CreateIndexWriter`. `IIndexRecordWriter` is now `IDisposable` and has only the bulk `Write` method.
  - PostgreSQL: new `PgSqlIndexWriter`/`PgSqlIndexRecordWriter` writing via `COPY` (about 9x faster than multi-row `INSERT`s).
  - PostgreSQL schema revised (requires PostgreSQL 15+ with `pg_trgm`): identity columns, `field` as `varchar(50)`, unique token (value, language), trigram and pattern indexes for token searches, covering index for occurrence searches, index on occurrence target. Scripts are idempotent and upgrade Embix 4 schemas. `PgSqlTableInitializer` adds `DropIndexes`/`CreateIndexes` for bulk loads, used by the CLI.
  - `IIndexWriter.Flush` added; `IndexBuilder.BuildAsync` flushes the writer after each document. `IndexBuilderFactory` is `IDisposable` and owns the writer shared by its builders.
  - default buffer size is now 10000; CLI default database type is now `pgsql`.
  - fixed: token multipliers emitted the source token instead of the multiplied ones; `*` fallback for multipliers; missing `*` filter/tokenizer threw; data loss for all the documents after the first one in the CLI (shared writer disposed after the first document); duplicate token IDs under concurrency; `TokenFieldLengths`/`OccurrenceFieldLengths` not loaded from profile; `StandardStringTokenizer` skipping the character after a separator; `StringBuilderExtensions.IndexOf` false negatives; null reference when building without a progress reporter; leaked connections and readers; reading past `RecordLimit`.
  - partitioning: `IndexBuilder` no longer pages the data query with `LIMIT/OFFSET` per partition (which required a deterministic, unique `ORDER BY`, else records were duplicated or missed, and rescanned skipped rows). A single reader streams the records once, in any order, to `PartitionCount` parallel workers through a bounded channel. `DataSql` placeholders are now optional: `{0}` is always 0, `{1}` the count of records to process. A failure in the reader or any worker cancels the others, and the original exception is thrown.
  - Search fixes: numeric and bitwise operators bound the field name as a value; `<` behaved as `<=`; LIKE wildcards in user values are now escaped; named flags take precedence over hex numbers and are case-insensitive.
  - added `Embix.PgSql.Test` (PostgreSQL integration tests) and many unit tests.
- 2026-10-29: **Search: SqlKata dropped**, replaced by plain parameterized SQL via ADO.NET. Migration notes for clients:
  - packages: `SqlKata` and `SqlKata.Execution` are no longer referenced (remove their `using`s). `QueryFactory`, `Query`, compilers and their execution extensions (`Get`, `Paginate`, etc.) are gone.
  - new types in `Embix.Search`:
    - `SqlDialect` (abstract): RDBMS-specific SQL (`QuoteIdentifier`, `BuildLike`, `BuildRegexMatch`, `BuildNumber`, `BuildSimilarity`, `BuildPaging`). Implemented by `PgSqlDialect` (`Embix.Search.PgSql`) and `MySqlDialect` (`Embix.Search.MySql`).
    - `SqlParameters`: collects named parameters; `Add(value)` returns the placeholder to embed in SQL (`@p0`, `@p1`...); `Add(name, value)` for explicit names.
    - `SqlWhereBuilder`: AND-ed conditions with their `Parameters`; `Add(condition)`, `AddParameter(value)`, `Quote(identifier)`, `ToSql(keyword = "WHERE")` (empty string when no conditions).
    - `SqlQuery(string Sql, IReadOnlyDictionary<string, object?> Parameters)`: a parameterized query, with `CreateCommand(DbConnection)`.
    - `PagedSqlQuery(SqlQuery Data, SqlQuery Count)`.
  - `QueryBuilder`:
    - `QueryFactory` property removed: build SQL text and execute it with the new `Query<T>(SqlQuery, Func<IDataRecord, T>)`, `QueryAsync<T>`, `Scalar<T>(SqlQuery)`, `ScalarAsync<T>`. Each execution opens and closes its own (pooled) connection, so builders are thread-safe and no longer hold a connection.
    - abstract `IDbConnection GetConnection(string)` now returns `DbConnection`; abstract `Compiler GetSqlCompiler()` replaced by `SqlDialect CreateDialect()`; the `Dialect` property exposes it.
    - protected field `_connString` replaced by the protected property `ConnectionString`.
    - added `CreateParameters()` and `CreateWhere(SqlParameters? parameters = null)`.
    - no longer `IDisposable`.
  - `NonPagedQueryBuilder<TRequest>.Build` returns `SqlQuery` (was SqlKata `Query`); added `Get<T>`/`GetAsync<T>(request, map)` to build and execute.
  - `PagedQueryBuilder<TRequest>.Build` returns `PagedSqlQuery` (was `Tuple<Query, Query>`, where Item1=data and Item2=count: now `Data` and `Count`). Added protected static `BuildPaging(SqlParameters, TRequest)` returning the dialect paging clause (empty when `PageSize` is 0), and `GetPage<T>`/`GetPageAsync<T>(request, map)` returning a `DataPage<T>` (the data query is skipped when the total is 0).
  - `QueryTextClauseBuilder`:
    - `AddClause(Query query, string name, string op, string value)` replaced by `AddClause(SqlWhereBuilder where, string name, string op, string value)`, returning the where builder; unknown operators add nothing, as before.
    - new `BuildClause(SqlParameters parameters, string name, string op, string value)` returning the condition SQL (or null for unknown operators), e.g. to combine conditions with `OR`.
    - field names are quoted by the dialect and may be qualified (`t.value` => `"t"."value"`); previously SqlKata did the same quoting.
    - protected virtual methods changed: `AddNumericClause`, `AddRegexClause`, `AddFuzzyMatchClause`, `AddBitTestClause` (taking a `Query`) are replaced by `BuildNumericClause`, `BuildRegexClause`, `BuildFuzzyClause`, `BuildBitTestClause`, plus the new `BuildLikeClause`: they take `(SqlParameters parameters, string column, ...)` with the column already quoted, and return the SQL condition.
    - `ParseFuzzyValue` returns a `(string Text, double Threshold)` value tuple (was `Tuple<string, double>`).
    - `DefaultFuzzyTreshold` renamed to `DefaultFuzzyThreshold`, and its default changed from 0.9 to 0.3 (see below).
    - generated SQL: values are always bound as parameters (`@p0`...), never inlined; text matches other than equality are case-insensitive (`ILIKE` in PostgreSQL, `LOWER(x) LIKE LOWER(@p)` in MySql); the `&:` bit test is `(f & @p) = @p`.
  - **PostgreSQL fuzzy matching (`%=`) now uses `pg_trgm` trigram similarity** instead of a normalized Levenshtein distance: `(value % @p0 AND similarity(value, @p0) >= @p1)`, served by the trigram index of the schema. When the threshold is lower than `PgSqlDialect.IndexSimilarityThreshold` (default 0.3, matching the `pg_trgm.similarity_threshold` setting), the `%` prefilter (and thus the index) is not used: `similarity(value, @p0) >= @p1`. Trigram similarities are lower than the old Levenshtein-based ones (e.g. `rome`/`roma` 0.43, `neumagen`/`neumagan` 0.5), hence the new default threshold 0.3: clients passing explicit thresholds (`text:threshold`) should revise them. The schema no longer creates the `fuzzystrmatch` extension (existing databases keep it).

## 4.0.1

- 2025-02-04: updated packages.

## 4.0.0

- 2024-11-30: upgraded to NET 9.

## 3.0.0

- 2024-06-13: upgraded to NET 8.

## 1.2.2

- 2023-01-10: added `CalculatePartitionCount` to `IndexBuilder` to allow clients setup their UI.

## 1.2.1

- 2023-01-10:
  - added nullability to projects.
  - updated packages.
- 2022-11-09: upgraded to NET 7.

## 1.2.0

2022-09-18: updated packages.

## 1.1.0

2021-11-09: upgraded to NET 6.
