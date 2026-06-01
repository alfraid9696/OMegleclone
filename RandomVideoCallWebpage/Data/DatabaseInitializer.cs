using Microsoft.EntityFrameworkCore;

namespace RandomVideoCallWebpage.Data;

public static class DatabaseInitializer
{
    public static void ApplyMigrations(ApplicationDbContext db, bool isSqlite)
    {
        try
        {
            db.Database.Migrate();
        }
        catch (Exception)
        {
            if (!isSqlite)
            {
                throw;
            }
        }

        EnsureBlockedColumnExists(db, isSqlite);
    }

    private static void EnsureBlockedColumnExists(ApplicationDbContext db, bool isSqlite)
    {
        if (!db.Database.CanConnect())
        {
            return;
        }

        if (isSqlite)
        {
            EnsureSqliteColumn(db);
        }
        else
        {
            EnsureSqlServerColumn(db);
        }
    }

    private static void EnsureSqliteColumn(ApplicationDbContext db)
    {
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

    private static void EnsureSqlServerColumn(ApplicationDbContext db)
    {
        db.Database.ExecuteSqlRaw("""
            IF COL_LENGTH('AspNetUsers', 'IsBlocked') IS NULL
            BEGIN
                ALTER TABLE AspNetUsers ADD IsBlocked bit NOT NULL CONSTRAINT DF_AspNetUsers_IsBlocked DEFAULT 0;
            END
            """);
    }
}
