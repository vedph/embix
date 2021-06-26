using Embix.Core.Config;
using Fusi.Tools;
using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Linq;
using System.Globalization;

namespace Embix.Core
{
    /// <summary>
    /// Character codes inspector. This is used to inspect the BMP Unicode
    /// characters found in a set of <see cref="DocumentDefinition"/>'s from
    /// a database. Each character code is collected with its frequency in
    /// <see cref="Counts"/>.
    /// </summary>
    public sealed class CharInspector
    {
        private readonly IDbConnectionFactory _connFactory;
        private readonly Dictionary<int, long> _counts;
        private CombinedProgressCalculator _calculator;
        private readonly string[] _uniCatNames = new[]
        {
            "Lu", "Ll", "Lt", "Lm", "Lo", "Mn", "Mc", "Me", "Nd", "Nl", "No",
            "Zs", "Zl", "Zp", "Cc", "Cf", "Cs", "Co", "Pc", "Pd", "Ps", "Pe",
            "Pi", "Pf", "Po", "Sm", "Sc", "Sk", "So", "Cn"
        };

        /// <summary>
        /// Gets the counts collected by this inspector. Each key
        /// is a character code, having a value equal to its frequency.
        /// </summary>
        public IReadOnlyDictionary<int, long> Counts => _counts;

        /// <summary>
        /// Initializes a new instance of the <see cref="CharInspector"/> class.
        /// </summary>
        public CharInspector(IDbConnectionFactory connFactory)
        {
            _connFactory = connFactory
                ?? throw new ArgumentNullException(nameof(connFactory));

            _counts = new Dictionary<int, long>();
        }

        /// <summary>
        /// Resets all the counts.
        /// </summary>
        public void Reset() => _counts.Clear();

        private void Inspect(string text)
        {
            if (string.IsNullOrEmpty(text)) return;

            foreach (char c in text)
            {
                if (!_counts.ContainsKey(c)) _counts[c] = 1;
                else _counts[c]++;
            }
        }

        private void InspectDocument(
            IDbConnection connection,
            DocumentDefinition doc,
            CancellationToken cancel,
            IProgress<ProgressReport> progress)
        {
            ProgressReport report = progress != null ?
                new ProgressReport() : null;

            // get count
            IDbCommand cmd = connection.CreateCommand();
            cmd.CommandText = doc.CountSql;
            int result = (int)(long)cmd.ExecuteScalar();

            if (result == 0) return;

            // inspect
            cmd.CommandText = doc.DataSql;
            using IDataReader reader = cmd.ExecuteReader();
            int count = 0;
            while (reader.Read())
            {
                for (int i = 0; i < reader.FieldCount; i++)
                {
                    if (reader.IsDBNull(i)) continue;
                    string text = reader.GetString(i);
                    Inspect(text);
                }
                if (progress != null && ++count % 10 == 0)
                {
                    report.Count = count;
                    report.Percent = _calculator.Calculate(
                        _calculator.BigChunkCount,
                        count);
                    progress.Report(report);
                }
            }
        }

        /// <summary>
        /// Inspects the specified documents.
        /// </summary>
        /// <param name="documents">The documents.</param>
        /// <param name="cancel">The cancel token.</param>
        /// <param name="progress">The optional progress reporter.</param>
        /// <exception cref="ArgumentNullException">documents</exception>
        public void Inspect(
            IList<DocumentDefinition> documents,
            CancellationToken cancel,
            IProgress<ProgressReport> progress = null)
        {
            if (documents == null)
                throw new ArgumentNullException(nameof(documents));

            if (documents.Count == 0) return;

            _calculator = progress != null ?
                new CombinedProgressCalculator(documents.Count) : null;

            using IDbConnection connection = _connFactory.GetConnection();
            connection.Open();
            int docCount = 0;

            foreach (DocumentDefinition doc in documents)
            {
                _calculator.Calculate(docCount, 0);
                InspectDocument(connection, doc, cancel, progress);
                docCount++;
                if (cancel.IsCancellationRequested) break;
            }
        }

        /// <summary>
        /// Inspects the documents specified in the <paramref name="json"/> profile.
        /// This is an array of JSON document definitions.
        /// </summary>
        /// <param name="json">The json.</param>
        /// <param name="cancel">The cancel.</param>
        /// <param name="progress">The progress.</param>
        /// <exception cref="ArgumentNullException">json</exception>
        public void Inspect(
            string json,
            CancellationToken cancel,
            IProgress<ProgressReport> progress = null)
        {
            if (json == null) throw new ArgumentNullException(nameof(json));

            JsonDocument doc = JsonDocument.Parse(json, new JsonDocumentOptions
            {
                AllowTrailingCommas = true
            });
            List<DocumentDefinition> documents = new List<DocumentDefinition>();

            foreach (JsonElement child in doc.RootElement.EnumerateArray())
            {
                documents.Add(JsonSerializer.Deserialize<DocumentDefinition>(
                    child.GetRawText()));
            }
            Inspect(documents, cancel, progress);
        }

        /// <summary>
        /// Saves the counts in CSV format.
        /// </summary>
        /// <param name="writer">The writer to write to.</param>
        /// <exception cref="ArgumentNullException">writer</exception>
        public void SaveCsv(TextWriter writer)
        {
            if (writer is null) throw new ArgumentNullException(nameof(writer));

            writer.WriteLine("hex,dec,cat,glyph,freq");
            foreach (int code in _counts.Keys.OrderBy(n => n))
            {
                // hex dec
                writer.Write($"\"{code:X4}\",{code},");

                // cat
                char c = code < 32 || code == 127 ? '.' : (char)code;
                var cat = char.GetUnicodeCategory(c);
                writer.Write($"{_uniCatNames[(int)cat]},");

                // glyph
                if (c == ',') writer.Write("\",\",");
                else
                {
                    if (cat == UnicodeCategory.NonSpacingMark) writer.Write(' ');
                    writer.Write($"{c},");
                }

                // frequency
                writer.WriteLine(_counts[code]);
            }
            writer.Flush();
        }
    }
}
