using Microsoft.EntityFrameworkCore;

namespace RandomVideoCallWebpage.Data;

public static class DatabaseInitializer
{
    public static void ApplyMigrations(ApplicationDbContext db, bool isSqlite)
    {
        if (isSqlite)
        {
            ApplySqlite(db);
            return;
        }

        db.Database.Migrate();
    }

    private static void ApplySqlite(ApplicationDbContext db)
    {
        if (!TableExists(db, "AspNetUsers"))
        {
            // SQL Server migrations use nvarchar and cannot run on SQLite.
            // Reset any partial schema from a failed Migrate() attempt, then create from the model.
            if (TableExists(db, "__EFMigrationsHistory") || TableExists(db, "AspNetRoles"))
            {
                db.Database.EnsureDeleted();
            }

            db.Database.EnsureCreated();
            return;
        }

        EnsureSqliteBlockedColumn(db);
        EnsureFriendTables(db);
    }

    private static void EnsureFriendTables(ApplicationDbContext db)
    {
        if (TableExists(db, "Friendships"))
        {
            return;
        }

        var provider = db.Database.ProviderName ?? "";
        if (provider.Contains("Sqlite", StringComparison.OrdinalIgnoreCase))
        {
            db.Database.ExecuteSqlRaw("""
                CREATE TABLE IF NOT EXISTS FriendRequests (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    FromUserId TEXT NOT NULL,
                    ToUserId TEXT NOT NULL,
                    Status INTEGER NOT NULL,
                    CreatedAtUtc TEXT NOT NULL
                );
                CREATE TABLE IF NOT EXISTS Friendships (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    UserAId TEXT NOT NULL,
                    UserBId TEXT NOT NULL,
                    CreatedAtUtc TEXT NOT NULL
                );
                CREATE UNIQUE INDEX IF NOT EXISTS IX_Friendships_UserAId_UserBId ON Friendships (UserAId, UserBId);
                CREATE TABLE IF NOT EXISTS FriendMessages (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    SenderId TEXT NOT NULL,
                    ReceiverId TEXT NOT NULL,
                    Body TEXT NOT NULL,
                    SentAtUtc TEXT NOT NULL,
                    ReadAtUtc TEXT NULL
                );
                CREATE TABLE IF NOT EXISTS FriendCalls (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    CallerId TEXT NOT NULL,
                    ReceiverId TEXT NOT NULL,
                    Status INTEGER NOT NULL,
                    CreatedAtUtc TEXT NOT NULL
                );
                """);
            return;
        }

        try
        {
            db.Database.Migrate();
        }
        catch
        {
            db.Database.EnsureCreated();
        }
    }

    private static bool TableExists(ApplicationDbContext db, string tableName)
    {
        if (!db.Database.CanConnect())
        {
            return false;
        }

        var connection = db.Database.GetDbConnection();
        var shouldClose = connection.State != System.Data.ConnectionState.Open;

        if (shouldClose)
        {
            connection.Open();
        }

        try
        {
            using var command = connection.CreateCommand();
            command.CommandText =
                "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = $name";
            var parameter = command.CreateParameter();
            parameter.ParameterName = "$name";
            parameter.Value = tableName;
            command.Parameters.Add(parameter);

            return Convert.ToInt32(command.ExecuteScalar()) > 0;
        }
        finally
        {
            if (shouldClose)
            {
                connection.Close();
            }
        }
    }

    private static void EnsureSqliteBlockedColumn(ApplicationDbContext db)
    {
        if (!TableExists(db, "AspNetUsers"))
        {
            return;
        }

        var connection = db.Database.GetDbConnection();
        var shouldClose = connection.State != System.Data.ConnectionState.Open;

        if (shouldClose)
        {
            connection.Open();
        }

        try
        {
            using var check = connection.CreateCommand();
            check.CommandText =
                "SELECT COUNT(*) FROM pragma_table_info('AspNetUsers') WHERE name = 'IsBlocked'";
            var exists = Convert.ToInt32(check.ExecuteScalar()) > 0;

            if (!exists)
            {
                using var alter = connection.CreateCommand();
                alter.CommandText =
                    "ALTER TABLE AspNetUsers ADD COLUMN IsBlocked INTEGER NOT NULL DEFAULT 0";
                alter.ExecuteNonQuery();
            }
        }
        finally
        {
            if (shouldClose)
            {
                connection.Close();
            }
        }
    }
}
