using Embix.Core;
using Embix.MySql;
using Embix.PgSql;
using Microsoft.Extensions.CommandLineUtils;
using System;
using System.Threading.Tasks;

namespace Embix.Commands
{
    public sealed class InitIndexCommand : ICommand
    {
        private readonly AppOptions _options;
        private readonly string _dbName;
        private readonly string _dbType;
        private readonly bool _clear;

        public InitIndexCommand(
            AppOptions options,
            string dbName,
            string dbType,
            bool clear)
        {
            _options = options ?? throw new ArgumentNullException(nameof(options));
            _dbName = dbName ?? throw new ArgumentNullException(nameof(dbName));
            _dbType = dbType ?? throw new ArgumentNullException(nameof(dbType));
            _clear = clear;
        }

        public static void Configure(CommandLineApplication command,
            AppOptions options)
        {
            command.Description = "Build text index in database.";
            command.HelpOption("-?|-h|--help");

            CommandArgument dbNameArgument = command.Argument("[dbName]",
                "The database name");

            CommandOption dbTypeOption = command.Option("-t|--type",
                "The type of database: mysql (default), pgsql",
                CommandOptionType.SingleValue);

            CommandOption clearOption = command.Option("-c|--clear",
                "Clear the index tables in database if present",
                CommandOptionType.NoValue);

            command.OnExecute(() =>
            {
                options.Command = new InitIndexCommand(
                    options,
                    dbNameArgument.Value,
                    dbTypeOption.Value() ?? "mysql",
                    clearOption.HasValue());
                return 0;
            });
        }

        public Task Run()
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("INIT INDEX\n");
            Console.ResetColor();

            Console.WriteLine(
                $"Database name: {_dbName}\n" +
                $"Database type: {_dbType}\n");

            Serilog.Log.Information("INIT INDEX");
            string connString = _options.Configuration[$"ConnectionStrings:{_dbType}"];
            ITableInitializer initializer;
            switch (_dbType.ToLowerInvariant())
            {
                case "mysql":
                    initializer = new MySqlTableInitializer(
                        new MySqlDbConnectionFactory(connString));
                    break;
                case "pgsql":
                    initializer = new PgSqlTableInitializer(
                        new PgSqlDbConnectionFactory(connString));
                    break;
                default:
                    throw new ArgumentException("Invalid db type: " + _dbType);
            }

            Console.Write("Initializing...");
            initializer.Initialize(_clear);
            Console.WriteLine(" completed.");

            return Task.CompletedTask;
        }
    }
}
