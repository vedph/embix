using Embix.Core;
using Embix.Core.Config;
using Embix.MySql;
using Embix.PgSql;
using Fusi.Tools;
using Microsoft.Extensions.CommandLineUtils;
using ShellProgressBar;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace Embix.Commands
{
    public sealed class BuildIndexCommand : ICommand
    {
        private readonly AppOptions _options;
        private readonly string _profilePath;
        private readonly string _dbName;
        private readonly string _dbType;
        private readonly bool _clear;
        private readonly int _partitionCount;
        private readonly int _minPartitionSize;
        private readonly int _bufferSize;
        private readonly int _recordLimit;

        public BuildIndexCommand(
            AppOptions options,
            string profilePath,
            string dbName,
            string dbType,
            bool clear,
            int partitionCount,
            int minPartitionSize,
            int bufferSize,
            int recordLimit)
        {
            _options = options ?? throw new ArgumentNullException(nameof(options));
            _profilePath = profilePath
                ?? throw new ArgumentNullException(nameof(profilePath));
            _dbName = dbName ?? throw new ArgumentNullException(nameof(dbName));
            _dbType = dbType ?? throw new ArgumentNullException(nameof(dbType));
            _clear = clear;
            _partitionCount = partitionCount;
            _minPartitionSize = minPartitionSize;
            _bufferSize = bufferSize;
            _recordLimit = recordLimit;
        }

        public static void Configure(CommandLineApplication command,
            AppOptions options)
        {
            command.Description = "Build text index in database.";
            command.HelpOption("-?|-h|--help");

            CommandArgument profilePathArgument = command.Argument("[profilePath]",
                "The JSON profile file path");
            CommandArgument dbNameArgument = command.Argument("[dbName]",
                "The database name");

            CommandOption dbTypeOption = command.Option("-t|--type",
                "The type of database: mysql (default), mssql, pgsql",
                CommandOptionType.SingleValue);

            CommandOption clearOption = command.Option("-c|--clear",
                "Clear the index tables in database if present",
                CommandOptionType.NoValue);

            CommandOption partitionCountOption = command.Option("-p|--partitions",
                "The number of records partitions used to parallelize indexing " +
                "(default=2, use 1 for single-threaded indexing)",
                CommandOptionType.SingleValue);

            CommandOption partitionMinSizeOption = command.Option("-s|--partMinSize",
                "The minimum partition size when using parallelized indexing " +
                "(default=100). When the total number of records to be indexed " +
                "for each document is less than this size, no parallelization " +
                "will occur.",
                CommandOptionType.SingleValue);

            CommandOption bufferSizeOption = command.Option("-b|--bufferSize",
                "Set the write buffer size for tokens (default=100)",
                CommandOptionType.SingleValue);

            CommandOption limitOption = command.Option("-l|--limit",
                "Set an artificial limit to the records to be indexed (0=none)",
                CommandOptionType.SingleValue);

            command.OnExecute(() =>
            {
                int partitionCount = 2;
                if (partitionCountOption.HasValue())
                    int.TryParse(partitionCountOption.Value(), out partitionCount);

                int partitionMinSize = 100;
                if (partitionMinSizeOption.HasValue())
                    int.TryParse(partitionMinSizeOption.Value(), out partitionMinSize);

                int bufferSize = 100;
                if (bufferSizeOption.HasValue())
                    int.TryParse(bufferSizeOption.Value(), out bufferSize);

                int limit = 0;
                if (limitOption.HasValue())
                    int.TryParse(limitOption.Value(), out limit);

                options.Command = new BuildIndexCommand(
                    options,
                    profilePathArgument.Value,
                    dbNameArgument.Value,
                    dbTypeOption.Value() ?? "mysql",
                    clearOption.HasValue(),
                    partitionCount, partitionMinSize, bufferSize,
                    limit);
                return 0;
            });
        }

        internal static string LoadText(string path)
        {
            using (StreamReader reader = new StreamReader(path, Encoding.UTF8))
            {
                return reader.ReadToEnd();
            }
        }

        public async Task Run()
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("BUILD INDEX\n");
            Console.ResetColor();

            Console.WriteLine(
                $"Profile path: {_profilePath}\n" +
                $"Database name: {_dbName}\n" +
                $"Database type: {_dbType}\n");

            Serilog.Log.Information("BUILD INDEX");
            IIndexBuilderFactory factory = null;
            string connString = string.Format(
                _options.Configuration[$"ConnectionStrings:{_dbType}"], _dbName);
            ITableInitializer initializer = null;

            switch (_dbType.ToLowerInvariant())
            {
                case "mysql":
                    factory = new MySqlIndexBuilderFactory(
                        LoadText(_profilePath),
                        connString);
                    initializer = new MySqlTableInitializer(
                        new MySqlDbConnectionFactory(connString));
                    break;
                case "pgsql":
                    factory = new PgSqlIndexBuilderFactory(
                        LoadText(_profilePath),
                        connString);
                    initializer = new PgSqlTableInitializer(
                        new PgSqlDbConnectionFactory(connString));
                    break;
                default:
                    throw new ArgumentException("Invalid db type: " + _dbType);
            }
            factory.BufferSize = _bufferSize;

            initializer.Initialize(_clear);

            ProgressBar mainBar = new ProgressBar(100, null, new ProgressBarOptions
            {
                DisplayTimeInRealTime = true,
                EnableTaskBarProgress = true
            });
            ProgressBarOptions childBarOptionns = new ProgressBarOptions
            {
                DisplayTimeInRealTime = false,
                CollapseWhenFinished = true
            };
            Dictionary<string, ChildProgressBar> childBars =
                new Dictionary<string, ChildProgressBar>();
            Regex r = new Regex(@"^\[([^]]+)\]", RegexOptions.Compiled);

            foreach (DocumentDefinition document in factory.Profile.Documents)
            {
                using (IndexBuilder builder = factory.GetBuilder(null, _options.Logger))
                {
                    builder.PartitionCount = _partitionCount;
                    builder.MinPartitionSize = _minPartitionSize;
                    builder.RecordLimit = _recordLimit;

                    await builder.BuildAsync(document.Id, CancellationToken.None,
                        new Progress<ProgressReport>(report =>
                        {
                            IProgressBar bar = mainBar;
                            Match m = r.Match(report.Message ?? "");
                            if (m.Success)
                            {
                                if (!childBars.ContainsKey(m.Groups[1].Value))
                                {
                                    childBars[m.Groups[1].Value] =
                                        mainBar.Spawn(100, m.Groups[1].Value,
                                            childBarOptionns);
                                }
                                bar = childBars[m.Groups[1].Value];
                            }
                            bar.Tick(report.Percent, report.Message);
                        }));
                }
            }
        }
    }
}
