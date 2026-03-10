namespace ActiveForge.Query
{
    /// <summary>FREETEXT(field, @value) — SQL Server full-text search</summary>
    public class FullTextTerm : QueryTerm
    {
        public FullTextTerm(Record target, TField field, object value)   : base(target, field, value) { }
        public FullTextTerm(Record target, TString field, object value)  : base(target, field, value) { }

        public override QueryFragment GetSQL(RecordBinding binding, ref int termNumber)
        {
            FieldBinding check    = GetTermFieldInfo(binding);
            string       aliasSQL = check.MapNode.Alias.Length > 0 ? check.MapNode.Alias + "." : "";
            return new QueryFragment(" FREETEXT(" + aliasSQL
                + Target.GetConnection().QuoteName(check.Info.TargetName)
                + "," + ParameterMark + "IN_" + check.Info.TargetName + (termNumber++) + ")");
        }

        public override string GetDeleteSQL(RecordBinding binding, ref int termNumber)
        {
            FieldBinding check = GetTermFieldInfo(binding);
            return " FREETEXT("
                + Target.GetConnection().QuoteName(check.Info.SourceName) + "."
                + Target.GetConnection().QuoteName(check.Info.TargetName)
                + "," + ParameterMark + "IN_" + check.Info.TargetName + (termNumber++) + ")";
        }
    }
}
