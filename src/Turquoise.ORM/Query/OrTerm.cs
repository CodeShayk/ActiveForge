namespace Turquoise.ORM.Query
{
    public class OrTerm : QueryTerm
    {
        private QueryTerm _term1;
        private QueryTerm _term2;

        public OrTerm(QueryTerm term1, QueryTerm term2) : base()
        {
            _term1 = term1;
            _term2 = term2;
        }

        public override bool IncludesLookupDataObject(DataObject rootObject)
            => _term1.IncludesLookupDataObject(rootObject) || _term2.IncludesLookupDataObject(rootObject);

        public override QueryFragment GetSQL(ObjectBinding binding, ref int termNumber)
            => new QueryFragment("(" + _term1.GetSQL(binding, ref termNumber) + " OR " + _term2.GetSQL(binding, ref termNumber) + ")");

        public override string GetDeleteSQL(ObjectBinding binding, ref int termNumber)
            => "(" + _term1.GetDeleteSQL(binding, ref termNumber) + " OR " + _term2.GetDeleteSQL(binding, ref termNumber) + ")";

        public override void BindParameters(DataObject obj, ObjectBinding binding, CommandBase command, ref int termNumber)
        {
            _term1.BindParameters(obj, binding, command, ref termNumber);
            _term2.BindParameters(obj, binding, command, ref termNumber);
        }

        public override string GetQueryValues(DataObject obj, ObjectBinding binding)
            => "[" + _term1.GetQueryValues(obj, binding) + "]or[" + _term2.GetQueryValues(obj, binding) + "]";

        public override FieldSubset IncludeInFieldSubset(DataObject rootObject, ObjectBinding binding, FieldSubset fieldSubset)
        {
            fieldSubset = _term1.IncludeInFieldSubset(rootObject, binding, fieldSubset);
            fieldSubset = _term2.IncludeInFieldSubset(rootObject, binding, fieldSubset);
            return fieldSubset;
        }

        public override void GetParameterDebugInfo(DataObject obj, ObjectBinding binding, ref int n, ref string result)
        {
            _term1.GetParameterDebugInfo(obj, binding, ref n, ref result);
            _term2.GetParameterDebugInfo(obj, binding, ref n, ref result);
        }
    }
}
