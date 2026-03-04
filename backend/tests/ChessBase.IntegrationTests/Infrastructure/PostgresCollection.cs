namespace ChessBase.IntegrationTests.Infrastructure;

[CollectionDefinition(Name)]
public class PostgresCollection : ICollectionFixture<PostgresTestFixture>
{
    public const string Name = "postgres-collection";
}
