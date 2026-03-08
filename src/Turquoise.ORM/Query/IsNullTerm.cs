namespace Turquoise.ORM.Query
{
    /// <summary>field IS NULL</summary>
    public class IsNullTerm : QueryTerm
    {
        public IsNullTerm(DataObject target, TField field) : base(target, field, null) { }

        public override QueryFragment GetSQL(ObjectBinding binding, ref int termNumber)
        {
            FieldBinding check    = GetTermFieldInfo(binding);
            string       aliasSQL = check.MapNode.Alias.Length > 0 ? check.MapNode.Alias + "." : "";
            return new QueryFragment(aliasSQL + Target.GetConnection().QuoteName(check.Info.TargetName) + " IS NULL");
        }

        public override string GetDeleteSQL(ObjectBinding binding, ref int termNumber)
        {
            FieldBinding check = GetTermFieldInfo(binding);
            return Target.GetConnection().QuoteName(check.Info.SourceName) + "."
                + Target.GetConnection().QuoteName(check.Info.TargetName) + " IS NULL";
        }

        public override void BindParameters(DataObject obj, ObjectBinding binding, CommandBase command, ref int termNumber) { }
        public override void GetParameterDebugInfo(DataObject obj, ObjectBinding binding, ref int n, ref string result) { }

        public override string GetQueryValues(DataObject obj, ObjectBinding binding)
        {
            FieldBinding check    = GetTermFieldInfo(binding);
            string       aliasSQL = check.MapNode.Alias.Length > 0 ? check.MapNode.Alias + "." : "";
            return aliasSQL + Target.GetConnection().QuoteName(check.Info.TargetName) + " IS NULL";
        }
    }
}
