namespace Turquoise.ORM
{
    /// <summary>
    /// Abstract base for collections of named parameters that can be bound into
    /// a SQL command for table-valued function calls or parameterized queries.
    /// </summary>
    public abstract class ObjectParameterCollectionBase
    {
        /// <summary>
        /// Adds each parameter in this collection to <paramref name="cmd"/>,
        /// naming them sequentially from <paramref name="index"/> using <paramref name="parameterMark"/>.
        /// </summary>
        public abstract void BindFunctionParameters(CommandBase cmd, ref int index, string parameterMark);

        /// <summary>
        /// Returns the SQL fragment listing the function parameters (e.g. "@p1,@p2")
        /// for inclusion in a table-valued function call.
        /// </summary>
        public abstract string FormatFunctionParameters(ref int index, string parameterMark, bool includeParentheses);
    }
}
