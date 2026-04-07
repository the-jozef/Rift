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

            // ←←← Pôvodná tabuľka Users (nezmenená)
            string createUsers = @"
        CREATE TABLE IF NOT EXISTS Users (
            Id INTEGER PRIMARY KEY AUTOINCREMENT,
            Username TEXT UNIQUE NOT NULL,
            PasswordHash TEXT NOT NULL,
            SteamId64 TEXT UNIQUE NOT NULL
        );";

            using var cmdUsers = new SqliteCommand(createUsers, connection);
            cmdUsers.ExecuteNonQuery();

            // ←←← NOVÁ tabuľka pre aktuálne prihláseného používateľa
            string createSettings = @"
        CREATE TABLE IF NOT EXISTS Settings (
            Key TEXT PRIMARY KEY,
            Value TEXT NOT NULL
        );";

            using var cmdSettings = new SqliteCommand(createSettings, connection);
            cmdSettings.ExecuteNonQuery();
        }
        public void SetCurrentSteamId(string steamId64)
        {
            using var connection = new SqliteConnection(ConnectionString);
            connection.Open();

            string sql = @"
                INSERT OR REPLACE INTO Settings (Key, Value) 
                VALUES ('CurrentSteamId', @steamId);";

            using var command = new SqliteCommand(sql, connection);
            command.Parameters.AddWithValue("@steamId", steamId64 ?? "");
            command.ExecuteNonQuery();
        }

        // Načíta aktuálne prihláseného používateľa (vráti null ak nikto nie je prihlásený)
        public string? GetCurrentSteamId()
        {
            using var connection = new SqliteConnection(ConnectionString);
            connection.Open();

            string sql = "SELECT Value FROM Settings WHERE Key = 'CurrentSteamId';";
            using var command = new SqliteCommand(sql, connection);
            return command.ExecuteScalar()?.ToString();
        }

        // Odhlási (vymaže aktuálneho používateľa)
        public void ClearCurrentSteamId()
        {
            using var connection = new SqliteConnection(ConnectionString);
            connection.Open();

            string sql = "DELETE FROM Settings WHERE Key = 'CurrentSteamId';";
            using var command = new SqliteCommand(sql, connection);
            command.ExecuteNonQuery();
        }
    }
}

