namespace Turquoise.ORM.Query
{
    public class NotTerm : QueryTerm
    {
        protected QueryTerm Term1;

        public NotTerm(QueryTerm term1) : base()
        {
            Term1 = term1;
        }

        public override QueryFragment GetSQL(ObjectBinding binding, ref int termNumber)
            => new QueryFragment("NOT (" + Term1.GetSQL(binding, ref termNumber) + ")");

        public override string GetDeleteSQL(ObjectBinding binding, ref int termNumber)
            => "NOT (" + Term1.GetDeleteSQL(binding, ref termNumber) + ")";

        public override void BindParameters(DataObject obj, ObjectBinding binding, CommandBase command, ref int termNumber)
            => Term1.BindParameters(obj, binding, command, ref termNumber);

        public override string GetQueryValues(DataObject obj, ObjectBinding binding)
            => Term1.GetQueryValues(obj, binding);

        public override bool IncludesLookupDataObject(DataObject rootObject)
            => Term1.IncludesLookupDataObject(rootObject);

        public override FieldSubset IncludeInFieldSubset(DataObject rootObject, ObjectBinding binding, FieldSubset fieldSubset)
            => Term1.IncludeInFieldSubset(rootObject, binding, fieldSubset);

        public override void GetParameterDebugInfo(DataObject obj, ObjectBinding binding, ref int n, ref string result)
            => Term1.GetParameterDebugInfo(obj, binding, ref n, ref result);
    }
}
