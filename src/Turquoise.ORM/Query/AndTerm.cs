namespace Turquoise.ORM.Query
{
    public class AndTerm : QueryTerm
    {
        protected QueryTerm Term1;
        protected QueryTerm Term2;

        public AndTerm(QueryTerm term1, QueryTerm term2) : base()
        {
            Term1 = term1;
            Term2 = term2;
        }

        public override bool IncludesLookupDataObject(DataObject rootObject)
            => Term1.IncludesLookupDataObject(rootObject) || Term2.IncludesLookupDataObject(rootObject);

        public override QueryFragment GetSQL(ObjectBinding binding, ref int termNumber)
            => new QueryFragment("(" + Term1.GetSQL(binding, ref termNumber) + " AND " + Term2.GetSQL(binding, ref termNumber) + ")");

        public override string GetDeleteSQL(ObjectBinding binding, ref int termNumber)
            => "(" + Term1.GetDeleteSQL(binding, ref termNumber) + " AND " + Term2.GetDeleteSQL(binding, ref termNumber) + ")";

        public override void BindParameters(DataObject obj, ObjectBinding binding, CommandBase command, ref int termNumber)
        {
            Term1.BindParameters(obj, binding, command, ref termNumber);
            Term2.BindParameters(obj, binding, command, ref termNumber);
        }

        public override string GetQueryValues(DataObject obj, ObjectBinding binding)
            => "[" + Term1.GetQueryValues(obj, binding) + "]and[" + Term2.GetQueryValues(obj, binding) + "]";

        public override FieldSubset IncludeInFieldSubset(DataObject rootObject, ObjectBinding binding, FieldSubset fieldSubset)
        {
            fieldSubset = Term1.IncludeInFieldSubset(rootObject, binding, fieldSubset);
            fieldSubset = Term2.IncludeInFieldSubset(rootObject, binding, fieldSubset);
            return fieldSubset;
        }

        public override void GetParameterDebugInfo(DataObject obj, ObjectBinding binding, ref int n, ref string result)
        {
            Term1.GetParameterDebugInfo(obj, binding, ref n, ref result);
            Term2.GetParameterDebugInfo(obj, binding, ref n, ref result);
        }
    }
}
