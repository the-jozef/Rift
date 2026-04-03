using Microsoft.Data.Sqlite;
using System;
using System.IO;
namespace Rift_App.Database
{
    public class DatabaseService
    {
        private static readonly string _dbFolder =
        Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "RiftApp");
        private static readonly string _dbPath =
        Path.Combine(_dbFolder, "auth.db");


        public string ConnectionString => $"Data Source={_dbPath};";
        public DatabaseService()
        {
            Directory.CreateDirectory(_dbFolder);
        }
        public void Initialize()
        {
            using var connection = new SqliteConnection(ConnectionString);
            connection.Open();
            string createTable = @"
CREATE TABLE IF NOT EXISTS Users (
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

