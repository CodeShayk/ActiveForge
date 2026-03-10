namespace ActiveForge.Query
{
    /// <summary>
    /// Allows embedding a raw SQL predicate string directly into a WHERE clause.
    /// No parameter binding is performed; the caller is responsible for SQL injection safety.
    /// </summary>
    public class RawSqlTerm : QueryTerm
    {
        private readonly string _sql;

        public RawSqlTerm(string sql) : base()
        {
            _sql = sql;
        }

        public override QueryFragment GetSQL(ObjectBinding binding, ref int termNumber)
            => new QueryFragment(_sql);

        public override string GetDeleteSQL(ObjectBinding binding, ref int termNumber)
            => _sql;

        public override void BindParameters(DataObject obj, ObjectBinding binding, CommandBase command, ref int termNumber) { }
    }
}
