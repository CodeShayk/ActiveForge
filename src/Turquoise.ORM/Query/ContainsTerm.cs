namespace Turquoise.ORM.Query
{
    /// <summary>field LIKE '%value%' — wraps the value in % automatically</summary>
    public class ContainsTerm : QueryTerm
    {
        public ContainsTerm(DataObject target, TField field, object value)   : base(target, field, value) { }
        public ContainsTerm(DataObject target, TString field, object value)  : base(target, field, value) { }

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

        protected override object GetBindValue(TargetFieldInfo info, DataObject obj, object valueToConvert)
        {
            object result = base.GetBindValue(info, obj, valueToConvert);
            return "%" + result + "%";
        }
    }
}
