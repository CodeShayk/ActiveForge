using System.Collections.Generic;

namespace ActiveForge.Query
{
    /// <summary>
    /// A leaf <see cref="QueryTerm"/> that generates a SQL <c>IN</c> predicate of the form
    /// <c>alias.column IN(@IN_column&lt;n&gt;, @IN_column&lt;n+1&gt;, ...)</c>.
    /// One bound parameter is emitted for each element in the supplied value list; the
    /// <paramref name="termNumber"/> counter is incremented once per element.
    /// </summary>
    public class InTerm : QueryTerm
    {
        /// <summary>
        /// Initialises a new <see cref="InTerm"/> that matches <paramref name="field"/> against
        /// any value in <paramref name="values"/>.
        /// </summary>
        /// <param name="target">
        /// The <see cref="Record"/> that owns the field. For joined queries this may be
        /// an embedded child <see cref="Record"/>, not the root.
        /// </param>
        /// <param name="field">The field whose column will appear on the left-hand side of <c>IN</c>.</param>
        /// <param name="values">
        /// The list of candidate values. Each element produces a separate named parameter in the
        /// generated SQL. The list must not be <see langword="null"/> or empty; an empty IN list
        /// produces invalid SQL on most databases.
        /// </param>
        public InTerm(Record target, TField field, IList<object> values)
            : base(target, field, values) { }

        /// <summary>
        /// Generates the SQL fragment
        /// <c>alias.QuotedColumn IN(@IN_column&lt;n&gt;, @IN_column&lt;n+1&gt;, ...)</c>
        /// for use in a SELECT or UPDATE WHERE clause.
        /// <paramref name="termNumber"/> is incremented once for each value in the list.
        /// </summary>
        /// <param name="binding">The <see cref="RecordBinding"/> that resolves the field to its <see cref="FieldBinding"/>.</param>
        /// <param name="termNumber">
        /// Running counter used to generate unique parameter name suffixes; incremented once
        /// per list element.
        /// </param>
        /// <returns>A <see cref="QueryFragment"/> containing the IN predicate.</returns>
        public override QueryFragment GetSQL(RecordBinding binding, ref int termNumber)
        {
            FieldBinding check    = GetTermFieldInfo(binding);
            string       aliasSQL = check.MapNode.Alias.Length > 0 ? check.MapNode.Alias + "." : "";
            string       inList   = BuildList(check, ref termNumber);
            return new QueryFragment(aliasSQL + Target.GetConnection().QuoteName(check.Info.TargetName)
                + " IN(" + inList + ")");
        }

        /// <summary>
        /// Generates the SQL IN fragment for use inside a DELETE statement's WHERE clause.
        /// Uses the field's source (table) name rather than a query alias.
        /// <paramref name="termNumber"/> is incremented once per list element.
        /// </summary>
        /// <param name="binding">The <see cref="RecordBinding"/> that resolves the field to its <see cref="FieldBinding"/>.</param>
        /// <param name="termNumber">
        /// Running counter used to generate unique parameter name suffixes; incremented once
        /// per list element.
        /// </param>
        /// <returns>An IN predicate string suitable for DELETE SQL.</returns>
        public override string GetDeleteSQL(RecordBinding binding, ref int termNumber)
        {
            FieldBinding check  = GetTermFieldInfo(binding);
            string       inList = BuildList(check, ref termNumber);
            return Target.GetConnection().QuoteName(check.Info.SourceName) + "."
                + Target.GetConnection().QuoteName(check.Info.TargetName)
                + " IN(" + inList + ")";
        }

        /// <summary>
        /// Binds one named parameter for each element in the value list.
        /// Each value is passed through the field's <c>FieldMapper</c> and optional
        /// encryption layer before being added to <paramref name="command"/>.
        /// <paramref name="termNumber"/> is incremented once per element, matching the
        /// parameter names emitted by <see cref="GetSQL"/>.
        /// </summary>
        /// <param name="obj">The <see cref="Record"/> instance used as context by the field mapper.</param>
        /// <param name="binding">The <see cref="RecordBinding"/> that resolves the field to its <see cref="FieldBinding"/>.</param>
        /// <param name="command">The <see cref="CommandBase"/> to which parameters are added.</param>
        /// <param name="termNumber">
        /// Running counter that must match the value used when <see cref="GetSQL"/> was called.
        /// </param>
        public override void BindParameters(Record obj, RecordBinding binding, CommandBase command, ref int termNumber)
        {
            FieldBinding     fb     = GetTermFieldInfo(binding);
            IList<object>    values = (IList<object>)Value;
            foreach (object v in values)
            {
                object bindValue = GetBindValue(fb.Info, obj, v);
                string paramName = ParameterMark + "IN_" + fb.Info.TargetName + (termNumber++);
                command.AddParameter(paramName, bindValue, fb.Info);
            }
        }

        /// <summary>
        /// Appends a human-readable description of the IN list to <paramref name="result"/>,
        /// formatted as <c>fieldName IN [v1, v2, ...]</c>.
        /// Used for diagnostic and logging output only.
        /// </summary>
        /// <param name="obj">The <see cref="Record"/> instance (unused by this override; present for interface consistency).</param>
        /// <param name="binding">The <see cref="RecordBinding"/> that resolves the field to its <see cref="FieldBinding"/>.</param>
        /// <param name="n">Running count of parameters appended so far; incremented by the count of values.</param>
        /// <param name="result">Accumulator string to which the IN-list description is appended.</param>
        public override void GetParameterDebugInfo(Record obj, RecordBinding binding, ref int n, ref string result)
        {
            if (binding == null) return;
            try
            {
                FieldBinding     fb     = GetTermFieldInfo(binding);
                var              values = (System.Collections.Generic.IList<object>)Value;
                string           sep    = result.Length > 0 ? ", " : "";
                result += sep + fb.Info?.TargetName + " IN [" + string.Join(", ", values) + "]";
                n += values?.Count ?? 0;
            }
            catch { }
        }

        /// <summary>
        /// Builds a comma-separated list of numbered parameter placeholders
        /// (e.g. <c>@IN_Name0, @IN_Name1, @IN_Name2</c>) for the IN clause.
        /// <paramref name="termNumber"/> is incremented once per element.
        /// </summary>
        /// <param name="check">The resolved <see cref="FieldBinding"/> providing the column name.</param>
        /// <param name="termNumber">Running counter; incremented for each placeholder emitted.</param>
        /// <returns>A comma-separated string of parameter placeholders.</returns>
        private string BuildList(FieldBinding check, ref int termNumber)
        {
            var    values = (IList<object>)Value;
            var    list   = new System.Text.StringBuilder();
            foreach (object _ in values)
            {
                if (list.Length > 0) list.Append(',');
                list.Append(ParameterMark + "IN_" + check.Info.TargetName + (termNumber++));
            }
            return list.ToString();
        }
    }
}
