﻿using System;
using System.Reflection;
using Embix.Commands;
using Microsoft.Extensions.CommandLineUtils;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Serilog.Extensions.Logging;
using IConfiguration = Microsoft.Extensions.Configuration.IConfiguration;

namespace Embix
{
    public sealed class AppOptions
    {
        public ICommand Command { get; set; }
        public IConfiguration Configuration { get; private set; }
        public ILogger Logger { get; private set; }

        public AppOptions()
        {
            BuildConfiguration();
        }

        private void BuildConfiguration()
        {
            ConfigurationBuilder cb = new();
            Configuration = cb
                .AddJsonFile("appsettings.json")
                .AddEnvironmentVariables()
                .Build();

            Logger = new SerilogLoggerProvider(Serilog.Log.Logger)
                .CreateLogger(nameof(Program));
        }

        public static AppOptions Parse(string[] args)
        {
            if (args == null) throw new ArgumentNullException(nameof(args));

            AppOptions options = new();
            CommandLineApplication app = new()
            {
                Name = "embix",
                FullName = "Embedded indexing tool - "
                    + Assembly.GetEntryAssembly().GetName().Version
            };
            app.HelpOption("-?|-h|--help");

            // app-level options
            RootCommand.Configure(app, options);

            int result = app.Execute(args);
            return result != 0 ? null : options;
        }
    }
}
