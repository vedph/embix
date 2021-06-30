using SqlKata.Compilers;
using SqlKata.Execution;
using System;
using System.Data;

namespace Embix.Search
{
    public abstract class QueryBuilder
    {
        protected readonly string _connString;
        private QueryFactory _qf;

        protected QueryFactory QueryFactory
        {
            get
            {
                if (_qf != null) return _qf;
                _qf = new QueryFactory(
                    GetConnection(_connString),
                    GetSqlCompiler());
                return _qf;
            }
        }

        protected QueryBuilder(string connString)
        {
            _connString = connString
                ?? throw new ArgumentNullException(nameof(connString));
        }

        protected abstract IDbConnection GetConnection(string connString);
        protected abstract Compiler GetSqlCompiler();
    }
}
