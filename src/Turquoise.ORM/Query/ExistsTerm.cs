namespace Turquoise.ORM.Query
{
    /// <summary>
    /// EXISTS (sub-query) predicate.
    /// The sub-query is represented as a <see cref="Query{T}"/> that knows how to emit
    /// a correlated EXISTS SELECT.
    /// </summary>
    public class ExistsTerm<T> : QueryTerm where T : DataObject
    {
        private readonly T      _existsObject;
        private readonly TField _existsObjectLinkField;

        /// <summary>
        /// Full constructor — specify both the outer link field and the inner link field explicitly.
        /// </summary>
        public ExistsTerm(DataObject target, TField targetLinkField, T existsObject, TField existsLinkField, Query<T> subQuery)
            : base(target, targetLinkField, subQuery)
        {
            _existsObject          = existsObject;
            _existsObjectLinkField = existsLinkField;
        }

        /// <summary>
        /// Convenience constructor for the common case where the outer link is IdentDataObject.ID.
        /// </summary>
        public ExistsTerm(IdentDataObject target, T existsObject, TField existsLinkField, Query<T> subQuery)
            : base(target, target.ID, subQuery)
        {
            _existsObject          = existsObject;
            _existsObjectLinkField = existsLinkField;
        }

        public override QueryFragment GetSQL(ObjectBinding binding, ref int termNumber)
        {
            FieldBinding   check = GetTermFieldInfo(binding);
            QueryFragment  term  = ((Query<T>)Value).GenerateExistsSQLQuery(
                check.MapNode.Alias, check.Info.TargetName, _existsObjectLinkField, ref termNumber);
            term.SQL = "EXISTS (" + term.SQL + ")";
            return term;
        }

        public override string GetDeleteSQL(ObjectBinding binding, ref int termNumber)
            => throw new System.NotImplementedException("ExistsTerm does not support delete SQL.");

        public override void BindParameters(DataObject obj, ObjectBinding binding, CommandBase command, ref int termNumber)
            => ((Query<T>)Value).BindParameters(command, ref termNumber);
    }
}
