using Microsoft.Data.SqlClient;

namespace Csir.Spme.Infrastructure.Persistence;

public static class SqlServerRowVersionDetector
{
    public static bool IsStoreGeneratedType(string? sqlTypeName) =>
        sqlTypeName is "timestamp" or "rowversion";

    public static bool UsesStoreGeneratedRowVersion(string connectionString)
    {
        try
        {
            using var connection = new SqlConnection(connectionString);
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandTimeout = 5;
            command.CommandText =
                """
                SELECT t.name
                FROM sys.columns AS c
                INNER JOIN sys.tables AS tb ON tb.object_id = c.object_id
                INNER JOIN sys.schemas AS s ON s.schema_id = tb.schema_id
                INNER JOIN sys.types AS t ON t.user_type_id = c.user_type_id
                WHERE s.name = N'comms' AND tb.name = N'Memos' AND c.name = N'RowVersion';
                """;
            var typeName = command.ExecuteScalar() as string;
            return string.IsNullOrWhiteSpace(typeName) || IsStoreGeneratedType(typeName);
        }
        catch (SqlException)
        {
            return true;
        }
    }
}
