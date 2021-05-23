﻿using Embix.Core;
using Npgsql;
using System;
using System.Data;
using System.Data.Common;
using System.IO;
using System.Reflection;
using System.Text;

namespace Embix.PgSql
{
    /// <summary>
    /// PostgreSql table initializer.
    /// </summary>
    /// <seealso cref="ITableInitializer" />
    public class PgSqlTableInitializer : ITableInitializer
    {
        private readonly IDbConnectionFactory _connFactory;

        public PgSqlTableInitializer(IDbConnectionFactory factory)
        {
            _connFactory = factory
                ?? throw new ArgumentNullException(nameof(factory));
        }

        /// <summary>
        /// Gets the SQL code for tables definition.
        /// </summary>
        /// <returns>SQL code.</returns>
        protected virtual string GetSql()
        {
            using StreamReader reader = new StreamReader(
                Assembly.GetExecutingAssembly()
                .GetManifestResourceStream("Embix.PgSql.Assets.Schema.sql"),
                Encoding.UTF8);
            return reader.ReadToEnd();
        }

        private static bool TableExists(string table, IDbConnection connection)
        {
            DbConnectionStringBuilder builder = new DbConnectionStringBuilder();
            builder.ConnectionString = connection.ConnectionString;
            string databaseName = builder["Database"] as string;

            IDbCommand cmd = connection.CreateCommand();
            // https://stackoverflow.com/questions/464474/check-if-a-sql-table-exists
            cmd.CommandText = "SELECT CASE WHEN EXISTS(" +
                "(SELECT * FROM information_schema.tables " +
                $"WHERE table_name = '{table}' AND table_schema='{databaseName}')" +
                ") THEN 1 ELSE 0 END;";
            long n = (long)cmd.ExecuteScalar();
            return n == 1;
        }

        /// <summary>
        /// Initializes the database index tables by creating them if not
        /// present; if the tables are already present and <paramref name="clear" />
        /// is true, they are truncated.
        /// </summary>
        /// <param name="clear">if set to <c>true</c>, truncate the index
        /// tables when present.</param>
        public void Initialize(bool clear)
        {
            using NpgsqlConnection connection =
                (NpgsqlConnection)_connFactory.GetConnection();
            connection.Open();

            if (!TableExists("token", connection))
            {
                NpgsqlCommand cmd = new NpgsqlCommand(GetSql(), connection);
                cmd.ExecuteNonQuery();
            }
            else if (clear)
            {
                NpgsqlCommand cmd = new NpgsqlCommand(
                    "SET FOREIGN_KEY_CHECKS=0;\n" +
                    "TRUNCATE TABLE token;\n" +
                    "TRUNCATE TABLE occurrence;\n" +
                    "SET FOREIGN_KEY_CHECKS=1;\n", connection);
                cmd.ExecuteNonQuery();
            }
            connection.Close();
        }
    }
}