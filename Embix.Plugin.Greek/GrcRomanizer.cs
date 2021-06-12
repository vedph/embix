using Fusi.Tools.Config;
using Proteus.Core;
using System;
using System.IO;
using System.Reflection;
using System.Text;

namespace Embix.Plugin.Greek
{
    /// <summary>
    /// Base class for ancient Greek romanizers.
    /// </summary>
    public abstract class GrcRomanizer : IConfigurable<GrcRomanizerOptions>
    {
        protected GreekTransliterator Transliterator { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="GrcRomanizer"/> class.
        /// </summary>
        protected GrcRomanizer()
        {
            Transliterator = new GreekTransliterator();
            Transliterator.LoadDefaultTables();
        }

        /// <summary>
        /// Configures the transliterator with the specified options.
        /// </summary>
        /// <param name="options">The options.</param>
        /// <exception cref="ArgumentNullException">options</exception>
        public void Configure(GrcRomanizerOptions options)
        {
            if (options == null) throw new ArgumentNullException(nameof(options));

            Transliterator.KsiAsX = options.KsiAsX;
            Transliterator.IncludeIpogegrammeni = options.IncludeIpogegrammeni;
            Transliterator.GammaPlusVelarAsN = options.GammaPlusVelarAsN;
            Transliterator.HAfterRR = options.HAfterRR;
            Transliterator.KhiAsCh = options.KhiAsCh;
            Transliterator.ConvertPunctuation = options.ConvertPunctuation;

            if (string.IsNullOrEmpty(options.TargetTable))
                Transliterator.LoadDefaultTables(options.IsIsoLatinOnly);
            else
            {
                if (options.TargetTable.StartsWith("$"))
                {
                    // load from resource
                    using (var reader = new StreamReader(
                        Assembly.GetExecutingAssembly()
                        .GetManifestResourceStream("Embix.Plugin.Greek.Assets." +
                        options.TargetTable.Substring(1) + ".txt"), Encoding.UTF8))
                    {
                        Transliterator.LoadTables(null, reader);
                    }
                }
                else if (File.Exists(options.TargetTable))
                {
                    // load from file
                    using (var reader = new StreamReader(
                        new FileStream(options.TargetTable, FileMode.Open,
                        FileAccess.Read, FileShare.Read), Encoding.UTF8))
                    {
                        Transliterator.LoadTables(null, reader);
                    }
                }
            }
        }
    }
}
