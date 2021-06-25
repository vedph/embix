using Embix.Core;
using Embix.MySql;
using Embix.PgSql;
using Fusi.Tools;
using Microsoft.Extensions.CommandLineUtils;
using ShellProgressBar;
using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Embix.Commands
{
    public sealed class InspectCharsCommand : ICommand
    {
        private readonly AppOptions _options;
        private readonly string _profilePath;
        private readonly string _outputPath;
        private readonly string _dbName;
        private readonly string _dbType;

        public InspectCharsCommand(AppOptions options,
            string profilePath,
            string outputPath,
            string dbName,
            string dbType)
        {
            _options = options ?? throw new ArgumentNullException(nameof(options));
            _profilePath = profilePath
                ?? throw new ArgumentNullException(nameof(profilePath));
            _outputPath = outputPath
                ?? throw new ArgumentNullException(nameof(outputPath));
            _dbName = dbName ?? throw new ArgumentNullException(nameof(dbName));
            _dbType = dbType ?? throw new ArgumentNullException(nameof(dbType));
        }

        public static void Configure(CommandLineApplication command,
            AppOptions options)
        {
            command.Description = "Inspect characters from selected parts " +
                "of the database.";
            command.HelpOption("-?|-h|--help");

            CommandArgument profilePathArgument = command.Argument("[profilePath]",
                "The JSON profile file path");
            CommandArgument dbNameArgument = command.Argument("[dbName]",
                "The database name");
            CommandArgument outputPathArgument = command.Argument("[output]",
                "The output path");

            CommandOption dbTypeOption = command.Option("-t|--type",
                "The type of database: mysql (default), mssql, pgsql",
                CommandOptionType.SingleValue);

            command.OnExecute(() =>
            {
                options.Command = new InspectCharsCommand(
                    options,
                    profilePathArgument.Value,
                    outputPathArgument.Value,
                    dbNameArgument.Value,
                    dbTypeOption.Value() ?? "mysql");
                return 0;
            });
        }

        public Task Run()
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("INSPECT CHARACTERS\n");
            Console.ResetColor();
            Console.WriteLine(
                $"Profile path: {_profilePath}\n" +
                $"Database name: {_dbName}\n" +
                $"Database type: {_dbType}\n" +
                $"Output path: {_outputPath}\n");

            // connection
            string connString = string.Format(
                _options.Configuration[$"ConnectionStrings:{_dbType}"], _dbName);
            IDbConnectionFactory factory;

            switch (_dbType.ToLowerInvariant())
            {
                case "mysql":
                    factory = new MySqlDbConnectionFactory(connString);
                    break;
                case "pgsql":
                    factory = new PgSqlDbConnectionFactory(connString);
                    break;
                default:
                    throw new ArgumentException("Invalid db type: " + _dbType);
            }

            ProgressBar bar = new ProgressBar(100, null, new ProgressBarOptions
            {
                DisplayTimeInRealTime = true,
                EnableTaskBarProgress = true
            });

            CharInspector inspector = new CharInspector(factory);
            inspector.Inspect(BuildIndexCommand.LoadText(_profilePath),
                CancellationToken.None,
                new Progress<ProgressReport>(report =>
                {
                    bar.Tick(report.Percent);
                }));

            // save
            using (StreamWriter writer = new StreamWriter(_outputPath, false,
                Encoding.UTF8))
            {
                inspector.SaveCsv(writer);
            }

            return Task.CompletedTask;
        }
    }
}
