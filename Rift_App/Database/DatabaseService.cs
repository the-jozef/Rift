using Microsoft.Data.Sqlite;
using System;
using System.IO;

namespace Rift_App.Database
{
    public class DatabaseService
    {
        private static readonly string _dbFolder =
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "RiftApp");

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

            // Tabuľka Users (pôvodná)
            string createUsers = @"
                CREATE TABLE IF NOT EXISTS Users (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    Username TEXT UNIQUE NOT NULL,
                    PasswordHash TEXT NOT NULL,
                    SteamId64 TEXT UNIQUE NOT NULL
                );";
            using var cmdUsers = new SqliteCommand(createUsers, connection);
            cmdUsers.ExecuteNonQuery();

            // NOVÁ tabuľka pre automatické prihlásenie
            string createRemembered = @"
                CREATE TABLE IF NOT EXISTS RememberedLogin (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    Username TEXT NOT NULL UNIQUE,
                    PasswordPlain TEXT NOT NULL
                );";
            using var cmdRem = new SqliteCommand(createRemembered, connection);
            cmdRem.ExecuteNonQuery();
        }

        // === METÓDY PRE AUTOMATICKÉ PRIHLÁSENIE ===
        public void SaveRememberedLogin(string username, string plainPassword)
        {
            using var connection = new SqliteConnection(ConnectionString);
            connection.Open();
            string sql = "INSERT OR REPLACE INTO RememberedLogin (Username, PasswordPlain) VALUES (@u, @p)";
            using var cmd = new SqliteCommand(sql, connection);
            cmd.Parameters.AddWithValue("@u", username);
            cmd.Parameters.AddWithValue("@p", plainPassword);
            cmd.ExecuteNonQuery();
        }

        public (string Username, string Password)? GetRememberedLogin()
        {
            using var connection = new SqliteConnection(ConnectionString);
            connection.Open();
            string sql = "SELECT Username, PasswordPlain FROM RememberedLogin LIMIT 1";
            using var cmd = new SqliteCommand(sql, connection);
            using var reader = cmd.ExecuteReader();
            if (reader.Read())
                return (reader.GetString(0), reader.GetString(1));
            return null;
        }

        public void ClearRememberedLogin()
        {
            using var connection = new SqliteConnection(ConnectionString);
            connection.Open();
            string sql = "DELETE FROM RememberedLogin";
            using var cmd = new SqliteCommand(sql, connection);
            cmd.ExecuteNonQuery();
        }
    }
}