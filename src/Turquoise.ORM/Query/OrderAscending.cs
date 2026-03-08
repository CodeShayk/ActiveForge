namespace Turquoise.ORM.Query
{
    public class OrderAscending : SortOrder
    {
        public OrderAscending(DataObject target, TField field) : base(target, field) { }

        public override string GetSQL(ObjectBinding binding)
        {
            FieldBinding check    = binding.GetFieldBinding(Target, Field);
            string       aliasSQL = check.MapNode.Alias.Length > 0 ? check.MapNode.Alias + "." : "";
            return aliasSQL + Target.GetConnection().QuoteName(check.Info.TargetName) + " ASC";
        }
    }
}
