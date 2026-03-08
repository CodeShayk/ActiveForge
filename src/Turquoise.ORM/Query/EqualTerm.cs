namespace Turquoise.ORM.Query
{
    /// <summary>field = @value</summary>
    public class EqualTerm : QueryTerm
    {
        public EqualTerm(DataObject target, TField field, object value) : base(target, field, value) { }
        public EqualTerm(DataObject target, TString field, object value) : base(target, field, value) { }

        public override QueryFragment GetSQL(ObjectBinding binding, ref int termNumber)
        {
            FieldBinding check    = GetTermFieldInfo(binding);
            string       aliasSQL = check.MapNode.Alias.Length > 0 ? check.MapNode.Alias + "." : "";
            return new QueryFragment(aliasSQL + Target.GetConnection().QuoteName(check.Info.TargetName)
                + "=" + ParameterMark + "IN_" + check.Info.TargetName + (termNumber++));
        }

        public override string GetDeleteSQL(ObjectBinding binding, ref int termNumber)
        {
            FieldBinding check = GetTermFieldInfo(binding);
            return check.Info.SourceName + "." + Target.GetConnection().QuoteName(check.Info.TargetName)
                + "=" + ParameterMark + "IN_" + check.Info.TargetName + (termNumber++);
        }
    }
}
