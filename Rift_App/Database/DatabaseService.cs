using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Rift_App.Database
{
    public class DatabaseService
    {
        private static readonly string _dbFolder =
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MySteamApp");

        private static readonly string _dbPath = Path.Combine(_dbFolder, "auth.db");

        public string ConnectionString => $"Data Source={_dbPath};";

        public DatabaseService()
        {
            Directory.CreateDirectory(_dbFolder);

            // Ak databáza neexistuje, vytvoríme tabuľku
            using var connection = new SqliteConnection(ConnectionString);
            connection.Open();

            string createTable = @"
            REATE TABLE Users (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    Username TEXT UNIQUE NOT NULL,
    PasswordHash TEXT NOT NULL,
    SteamId64 TEXT UNIQUE NOT NULL
            );";

            using var command = new SqliteCommand(createTable, connection);
            command.ExecuteNonQuery();
        }
    }
}
