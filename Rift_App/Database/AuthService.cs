using Microsoft.Data.Sqlite;
using BCrypt.Net;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Rift_App.Database
{
    public class AuthService
    {
        private readonly DatabaseService _db = new();   // tvoja trieda s ConnectionString
        public AuthService()
        {
            _db = new DatabaseService();   // uprav podľa seba
        }
        public bool RegisterWithSteam(string username, string plainPassword, string steamId64, out string errorMessage)
        {
            errorMessage = string.Empty;

            try
            {
                using var connection = new SqliteConnection(_db.ConnectionString);
                connection.Open();

                // Kontrola, či už existuje
                string checkSql = "SELECT COUNT(*) FROM Users WHERE Username = @u OR SteamId64 = @s";
                using var checkCmd = new SqliteCommand(checkSql, connection);
                checkCmd.Parameters.AddWithValue("@u", username);
                checkCmd.Parameters.AddWithValue("@s", steamId64);
                long count = (long)checkCmd.ExecuteScalar();

                if (count > 0)
                {
                    errorMessage = "Toto meno alebo Steam účet už existuje!";
                    return false;
                }

                string hash = BCrypt.Net.BCrypt.HashPassword(plainPassword, 12);

                string sql = "INSERT INTO Users (Username, PasswordHash, SteamId64) VALUES (@u, @h, @s)";
                using var cmd = new SqliteCommand(sql, connection);
                cmd.Parameters.AddWithValue("@u", username);
                cmd.Parameters.AddWithValue("@h", hash);
                cmd.Parameters.AddWithValue("@s", steamId64);
                cmd.ExecuteNonQuery();

                return true;
            }
            catch (Exception ex)
            {
                errorMessage = "Chyba pri registrácii: " + ex.Message;
                return false;
            }
        }

        public bool Login(string username, string plainPassword, out string steamId64, out string errorMessage)
        {
            steamId64 = string.Empty;
            errorMessage = string.Empty;

            try
            {
                using var connection = new SqliteConnection(_db.ConnectionString);
                connection.Open();

                string sql = "SELECT PasswordHash, SteamId64 FROM Users WHERE Username = @u";
                using var cmd = new SqliteCommand(sql, connection);
                cmd.Parameters.AddWithValue("@u", username);

                using var reader = cmd.ExecuteReader();
                if (reader.Read())
                {
                    string hash = reader.GetString(0);
                    if (BCrypt.Net.BCrypt.Verify(plainPassword, hash))
                    {
                        steamId64 = reader.GetString(1);
                        return true;
                    }
                    else
                    {
                        errorMessage = "Zlé heslo!";
                        return false;
                    }
                }
                errorMessage = "Používateľ neexistuje!";
                return false;
            }
            catch (Exception ex)
            {
                errorMessage = ex.Message;
                return false;
            }
        }
    }
}
