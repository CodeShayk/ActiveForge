namespace ActiveForge.Query
{
    /// <summary>field LIKE @value  (caller supplies the % wildcards)</summary>
    public class LikeTerm : QueryTerm
    {
        public LikeTerm(DataObject target, TField field, object value)   : base(target, field, value) { }
        public LikeTerm(DataObject target, TString field, object value)  : base(target, field, value) { }

        public override QueryFragment GetSQL(ObjectBinding binding, ref int termNumber)
        {
            FieldBinding check    = GetTermFieldInfo(binding);
            string       aliasSQL = check.MapNode.Alias.Length > 0 ? check.MapNode.Alias + "." : "";
            return new QueryFragment(aliasSQL + Target.GetConnection().QuoteName(check.Info.TargetName)
                + " LIKE " + ParameterMark + "IN_" + check.Info.TargetName + (termNumber++));
        }

        public override string GetDeleteSQL(ObjectBinding binding, ref int termNumber)
        {
            FieldBinding check = GetTermFieldInfo(binding);
            return Target.GetConnection().QuoteName(check.Info.SourceName) + "."
                + Target.GetConnection().QuoteName(check.Info.TargetName)
                + " LIKE " + ParameterMark + "IN_" + check.Info.TargetName + (termNumber++);
        }
    }
}
