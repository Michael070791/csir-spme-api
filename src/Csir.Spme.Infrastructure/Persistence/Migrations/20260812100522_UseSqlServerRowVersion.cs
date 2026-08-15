using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Csir.Spme.Infrastructure.Persistence.Migrations;

/// <summary>
/// Recreates application-managed varbinary tokens as SQL Server rowversion columns.
/// SQLite tests retain application-generated opaque tokens and do not run this migration.
/// </summary>
public partial class UseSqlServerRowVersion : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            DECLARE @schema sysname, @table sysname, @constraint sysname, @sql nvarchar(max);
            DECLARE rowversions CURSOR LOCAL FAST_FORWARD FOR
                SELECT SCHEMA_NAME(t.schema_id), t.name
                FROM sys.tables t
                JOIN sys.columns c ON c.object_id = t.object_id
                JOIN sys.types ty ON ty.user_type_id = c.user_type_id
                WHERE c.name = N'RowVersion' AND ty.name = N'varbinary';
            OPEN rowversions;
            FETCH NEXT FROM rowversions INTO @schema, @table;
            WHILE @@FETCH_STATUS = 0
            BEGIN
                SELECT @constraint = dc.name
                FROM sys.default_constraints dc
                JOIN sys.columns c
                  ON c.object_id = dc.parent_object_id
                 AND c.column_id = dc.parent_column_id
                WHERE dc.parent_object_id = OBJECT_ID(QUOTENAME(@schema) + N'.' + QUOTENAME(@table))
                  AND c.name = N'RowVersion';
                SET @sql = CASE WHEN @constraint IS NULL THEN N'' ELSE
                    N'ALTER TABLE ' + QUOTENAME(@schema) + N'.' + QUOTENAME(@table) +
                    N' DROP CONSTRAINT ' + QUOTENAME(@constraint) + N';' END +
                    N'ALTER TABLE ' + QUOTENAME(@schema) + N'.' + QUOTENAME(@table) +
                    N' DROP COLUMN [RowVersion];' +
                    N'ALTER TABLE ' + QUOTENAME(@schema) + N'.' + QUOTENAME(@table) +
                    N' ADD [RowVersion] rowversion NOT NULL;';
                EXEC sp_executesql @sql;
                SET @constraint = NULL;
                FETCH NEXT FROM rowversions INTO @schema, @table;
            END
            CLOSE rowversions;
            DEALLOCATE rowversions;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            DECLARE @schema sysname, @table sysname, @sql nvarchar(max);
            DECLARE rowversions CURSOR LOCAL FAST_FORWARD FOR
                SELECT SCHEMA_NAME(t.schema_id), t.name
                FROM sys.tables t
                JOIN sys.columns c ON c.object_id = t.object_id
                JOIN sys.types ty ON ty.user_type_id = c.user_type_id
                WHERE c.name = N'RowVersion' AND ty.name IN (N'timestamp', N'rowversion');
            OPEN rowversions;
            FETCH NEXT FROM rowversions INTO @schema, @table;
            WHILE @@FETCH_STATUS = 0
            BEGIN
                SET @sql =
                    N'ALTER TABLE ' + QUOTENAME(@schema) + N'.' + QUOTENAME(@table) +
                    N' DROP COLUMN [RowVersion];' +
                    N'ALTER TABLE ' + QUOTENAME(@schema) + N'.' + QUOTENAME(@table) +
                    N' ADD [RowVersion] varbinary(16) NOT NULL CONSTRAINT [DF_' +
                    REPLACE(@schema, N']', N'') + N'_' + REPLACE(@table, N']', N'') +
                    N'_RowVersion] DEFAULT (CONVERT(varbinary(16), NEWID()));';
                EXEC sp_executesql @sql;
                FETCH NEXT FROM rowversions INTO @schema, @table;
            END
            CLOSE rowversions;
            DEALLOCATE rowversions;
            """);
    }
}
