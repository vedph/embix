using Embix.Core.Config;
using Fusi.Tools;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Embix.Core.Inspection
{
    /// <summary>
    /// Character codes inspector.
    /// </summary>
    public sealed class CharInspector
    {
        private readonly IDbConnectionFactory _connFactory;
        private readonly Dictionary<int, long> _counts;
        private CombinedProgressCalculator _calculator;

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
            // TODO
        }

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
            int docCount = 0;

            foreach (DocumentDefinition doc in documents)
            {
                _calculator.Calculate(docCount, 0);
                InspectDocument(connection, doc, cancel, progress);
                docCount++;
                if (cancel.IsCancellationRequested) break;
            }
        }
    }
}
