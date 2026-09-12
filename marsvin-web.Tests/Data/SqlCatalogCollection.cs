namespace MarsvinWebExample.Tests.Data;

/// <summary>
/// Shares one SqlCatalogFixture (one MarsvinDb_Test database) across every
/// test class in this collection, so xUnit's default parallel-by-class
/// execution doesn't race two fixtures to create/drop the same database.
/// </summary>
[CollectionDefinition("SqlCatalog collection")]
public class SqlCatalogCollection : ICollectionFixture<SqlCatalogFixture>;
