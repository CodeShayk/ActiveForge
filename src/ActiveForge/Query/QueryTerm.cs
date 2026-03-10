using System.Reflection;
using ActiveForge.Attributes;

namespace ActiveForge.Query
{
    /// <summary>
    /// Abstract base for all composable WHERE-clause predicates.
    /// Subclasses implement <see cref="GetSQL"/> and <see cref="GetDeleteSQL"/> to emit
    /// the parameterised SQL fragment, and <see cref="BindParameters"/> to add the
    /// corresponding <see cref="CommandBase"/> parameters.
    /// <para>
    /// Leaf predicate classes (e.g. <see cref="EqualTerm"/>, <see cref="GreaterThanTerm"/>)
    /// each test one field against one value. Composite classes (<see cref="AndTerm"/>,
    /// <see cref="OrTerm"/>, <see cref="NotTerm"/>) combine or negate other terms.
    /// The <c>&amp;</c>, <c>|</c>, and <c>!</c> operators provide a concise fluent syntax
    /// for building these combinations.
    /// </para>
    /// </summary>
    public abstract class QueryTerm
    {
        /// <summary>
        /// Reflected <see cref="System.Reflection.FieldInfo"/> for <see cref="Field"/> on the
        /// owning <see cref="Record"/> type. Populated during <see cref="Initialize(Record,TField,object)"/>
        /// by calling <c>TField.GetFieldInfo</c>.
        /// </summary>
        protected FieldInfo   FieldInfo       = null;

        /// <summary>
        /// The <see cref="Record"/> that owns the field being tested. For joined queries
        /// this is the embedded child object that directly contains the field, not the root.
        /// Set during <see cref="Initialize(Record,TField,object)"/>.
        /// </summary>
        protected Record  Target          = null;

        /// <summary>
        /// The <see cref="TField"/> whose mapped column is tested by this predicate.
        /// Set during <see cref="Initialize(Record,TField,object)"/>.
        /// </summary>
        protected TField      Field           = null;

        /// <summary>
        /// The comparison value supplied at construction time. For leaf terms this is the
        /// right-hand side of the SQL operator. For <see cref="InTerm"/> it is an
        /// <see cref="System.Collections.Generic.IList{T}"/> of candidate values.
        /// <see cref="IsNullTerm"/> sets this to <see langword="null"/>.
        /// </summary>
        public    object      Value           = null;

        /// <summary>
        /// The database-specific parameter prefix character(s) retrieved from the connection
        /// (e.g. <c>@</c> for SQL Server / SQLite, <c>:</c> for PostgreSQL).
        /// Used when building parameter placeholder strings in <see cref="GetSQL"/>.
        /// </summary>
        protected string      ParameterMark;

        /// <summary>
        /// The database-specific string concatenation operator retrieved from the connection
        /// (e.g. <c>+</c> for SQL Server, <c>||</c> for SQLite / PostgreSQL).
        /// Available to subclasses that need to build concatenation expressions in SQL.
        /// </summary>
        protected string      StringConnectionOperator;

        /// <summary>
        /// Default constructor used by composite terms (<see cref="AndTerm"/>,
        /// <see cref="OrTerm"/>, <see cref="NotTerm"/>) that do not directly own a field.
        /// </summary>
        protected QueryTerm() { }

        /// <summary>
        /// Constructor for leaf predicate terms that test a <see cref="TField"/> field.
        /// Delegates to <see cref="Initialize(Record,TField,object)"/>.
        /// </summary>
        /// <param name="target">The <see cref="Record"/> that owns <paramref name="field"/>.</param>
        /// <param name="field">The field to test.</param>
        /// <param name="value">The comparison value (right-hand side of the operator).</param>
        protected QueryTerm(Record target, TField field, object value)
            => Initialize(target, field, value);

        /// <summary>
        /// Constructor for leaf predicate terms that test a <see cref="TString"/> field.
        /// Delegates to <see cref="Initialize(Record,TString,object)"/>.
        /// </summary>
        /// <param name="target">The <see cref="Record"/> that owns <paramref name="stringField"/>.</param>
        /// <param name="stringField">The string field to test.</param>
        /// <param name="value">The comparison value.</param>
        protected QueryTerm(Record target, TString stringField, object value)
            => Initialize(target, stringField, value);

        // ── Abstract contract ────────────────────────────────────────────────────────

        /// <summary>
        /// Returns the SQL WHERE fragment for this predicate, suitable for use in SELECT
        /// and UPDATE statements.
        /// </summary>
        /// <param name="binding">The <see cref="RecordBinding"/> that resolves fields to column metadata.</param>
        /// <param name="termNumber">
        /// Running counter used to produce unique parameter name suffixes; must be the same
        /// value that will be passed to <see cref="BindParameters"/> so that parameter names align.
        /// Incremented once per parameter placeholder emitted.
        /// </param>
        /// <returns>A <see cref="QueryFragment"/> containing the SQL predicate text.</returns>
        public abstract QueryFragment GetSQL(RecordBinding binding, ref int termNumber);

        /// <summary>
        /// Returns the SQL WHERE fragment for this predicate, suitable for use in DELETE
        /// statements. May differ from <see cref="GetSQL"/> in the way table names are
        /// qualified (source name vs. alias).
        /// </summary>
        /// <param name="binding">The <see cref="RecordBinding"/> that resolves fields to column metadata.</param>
        /// <param name="termNumber">
        /// Running counter; incremented once per parameter placeholder emitted.
        /// </param>
        /// <returns>A SQL predicate string suitable for DELETE SQL.</returns>
        public abstract string        GetDeleteSQL(RecordBinding binding, ref int termNumber);

        // ── Initialisation ───────────────────────────────────────────────────────────

        /// <summary>
        /// Shared initialisation for leaf terms that accept a <see cref="TField"/>.
        /// Resolves the field's <see cref="System.Reflection.FieldInfo"/> on <paramref name="target"/>,
        /// caches the connection's parameter mark and string operator, and validates that
        /// the field exists on the target type.
        /// </summary>
        /// <param name="target">The <see cref="Record"/> that owns <paramref name="field"/>.</param>
        /// <param name="field">The field to test.</param>
        /// <param name="value">The comparison value.</param>
        /// <exception cref="PersistenceException">
        /// Thrown when <paramref name="field"/> cannot be found as a member of <paramref name="target"/>.
        /// </exception>
        protected void Initialize(Record target, TField field, object value)
        {
            Field         = field;
            Target        = target;
            FieldInfo     = field.GetFieldInfo(target);
            Value         = value;
            ParameterMark = target.GetConnection().GetParameterMark();
            StringConnectionOperator = target.GetConnection().GetStringConnectionOperator();
            if (FieldInfo == null)
                throw new PersistenceException($"Query term field not found in {target.GetType().Name}");
        }

        /// <summary>
        /// Shared initialisation for leaf terms that accept a <see cref="TString"/>.
        /// Resolves the field's <see cref="System.Reflection.FieldInfo"/> on <paramref name="target"/>,
        /// caches the connection's parameter mark and string operator, and validates that
        /// the field exists on the target type.
        /// </summary>
        /// <param name="target">The <see cref="Record"/> that owns <paramref name="stringField"/>.</param>
        /// <param name="stringField">The string field to test.</param>
        /// <param name="value">The comparison value.</param>
        /// <exception cref="PersistenceException">
        /// Thrown when <paramref name="stringField"/> cannot be found as a member of <paramref name="target"/>.
        /// </exception>
        protected void Initialize(Record target, TString stringField, object value)
        {
            Field         = stringField;
            Target        = target;
            FieldInfo     = stringField.GetFieldInfo(target);
            Value         = value;
            ParameterMark = target.GetConnection().GetParameterMark();
            StringConnectionOperator = target.GetConnection().GetStringConnectionOperator();
            if (FieldInfo == null)
                throw new PersistenceException($"Query term field not found in {target.GetType().Name}");
        }

        // ── Parameter binding ────────────────────────────────────────────────────────

        /// <summary>
        /// Adds the parameter(s) required by this term to <paramref name="command"/>.
        /// The default implementation handles single-value terms by calling
        /// <see cref="GetBindValue(TargetFieldInfo, Record, RecordBinding)"/> and
        /// <see cref="CommandBase.AddParameter"/>.
        /// Multi-value terms (e.g. <see cref="InTerm"/>) override this method.
        /// </summary>
        /// <param name="obj">
        /// The <see cref="Record"/> instance used as context when the field mapper
        /// converts the value.
        /// </param>
        /// <param name="binding">The <see cref="RecordBinding"/> that resolves the field to its <see cref="FieldBinding"/>.</param>
        /// <param name="command">The <see cref="CommandBase"/> to which the parameter is added.</param>
        /// <param name="termNumber">
        /// Running counter that must match the value used when <see cref="GetSQL"/> was called;
        /// incremented once per parameter added.
        /// </param>
        public virtual void BindParameters(Record obj, RecordBinding binding, CommandBase command, ref int termNumber)
        {
            FieldBinding fb        = GetTermFieldInfo(binding);
            object       bindValue = GetBindValue(fb.Info, obj, Binding: binding);
            string       paramName = ParameterMark + "IN_" + fb.Info.TargetName + (termNumber++);
            command.AddParameter(paramName, bindValue, fb.Info);
        }

        /// <summary>
        /// Returns a human-readable representation of this term's current value,
        /// wrapped in square brackets (e.g. <c>[42]</c>). Used for diagnostics only.
        /// </summary>
        /// <param name="obj">The <see cref="Record"/> instance used as context for value conversion.</param>
        /// <param name="binding">The <see cref="RecordBinding"/> that resolves the field to its <see cref="FieldBinding"/>.</param>
        /// <returns>A string of the form <c>[value]</c>.</returns>
        public virtual string GetQueryValues(Record obj, RecordBinding binding)
        {
            FieldBinding fb        = GetTermFieldInfo(binding);
            object       bindValue = GetBindValue(fb.Info, obj, Binding: binding);
            return "[" + bindValue + "]";
        }

        // ── Helpers ──────────────────────────────────────────────────────────────────

        /// <summary>
        /// Resolves the <see cref="FieldBinding"/> for this term's field from
        /// <paramref name="binding"/>. The result provides the target column name, alias,
        /// field mapper, and encryption metadata needed to emit SQL and bind parameters.
        /// </summary>
        /// <param name="binding">The <see cref="RecordBinding"/> to query.</param>
        /// <returns>The <see cref="FieldBinding"/> for this term's field and target object.</returns>
        public FieldBinding GetTermFieldInfo(RecordBinding binding)
            => binding.GetFieldBinding(Target, Field);

        /// <summary>
        /// Converts <see cref="Value"/> using the field's <c>FieldMapper</c> and optional
        /// encryption layer. Delegates to <see cref="GetBindValue(TargetFieldInfo, Record, object)"/>
        /// passing <see cref="Value"/> as the raw value.
        /// </summary>
        /// <param name="info">Metadata describing the target column.</param>
        /// <param name="obj">The <see cref="Record"/> used as context by the field mapper.</param>
        /// <param name="Binding">Unused in the default implementation; available for subclasses.</param>
        /// <returns>The converted, possibly encrypted, value ready for parameter binding.</returns>
        protected virtual object GetBindValue(TargetFieldInfo info, Record obj, RecordBinding Binding)
            => GetBindValue(info, obj, Value);

        /// <summary>
        /// Converts <paramref name="valueToConvert"/> using the field's <c>FieldMapper</c>
        /// and optional encryption layer.
        /// <para>
        /// If the field has an <c>AllDataEncrypted</c> encryption method, the converted value
        /// is encrypted before being returned. Any other encryption method causes a
        /// <see cref="PersistenceException"/> because partial encryption cannot be safely
        /// used in query predicates.
        /// </para>
        /// </summary>
        /// <param name="info">Metadata describing the target column, including any mapper or encryption.</param>
        /// <param name="obj">The <see cref="Record"/> used as context by the field mapper.</param>
        /// <param name="valueToConvert">The raw value to convert.</param>
        /// <returns>The converted, possibly encrypted, value ready for parameter binding.</returns>
        /// <exception cref="PersistenceException">
        /// Thrown when the field uses an encryption method other than <c>AllDataEncrypted</c>.
        /// </exception>
        protected virtual object GetBindValue(TargetFieldInfo info, Record obj, object valueToConvert)
        {
            object result;
            if (info.FieldMapper != null)
            {
                info.FieldMapper.SetContainingDataObject(obj);
                result = info.FieldMapper.ConvertToDBValue(valueToConvert);
            }
            else
            {
                result = valueToConvert;
            }

            if (info.Encryption != null)
            {
                if (info.Encryption.GetEncryptionMethodType() == EncryptionMethodType.AllDataEncrypted)
                    result = info.Encryption.Encrypt(result);
                else
                    throw new PersistenceException(
                        $"Cannot use encrypted field '{info.TargetName}' in QueryTerm: " +
                        $"encryption method is '{info.Encryption.GetEncryptionMethodType()}'");
            }

            return result;
        }

        /// <summary>
        /// Returns <see langword="true"/> if this term's <see cref="Target"/> is (or is
        /// transitively contained by) a <see cref="LookupRecord"/> rooted under
        /// <paramref name="rootObject"/>. Used to determine whether lookup joins must be
        /// included when building the query's <see cref="RecordBinding"/>.
        /// </summary>
        /// <param name="rootObject">The root <see cref="Record"/> of the query.</param>
        public virtual bool IncludesLookupDataObject(Record rootObject)
        {
            if (Target == null) return false;
            if (Target is LookupRecord) return true;

            Record containing = rootObject.GetContainingDataObject(Target);
            while (containing != null && containing != rootObject)
            {
                if (containing is LookupRecord) return true;
                containing = rootObject.GetContainingDataObject(containing);
            }
            return false;
        }

        /// <summary>
        /// Ensures that the field referenced by this term is included in
        /// <paramref name="fieldSubset"/> so that the column is present in the query's
        /// SELECT list. Called during query preparation to build the minimal column set.
        /// </summary>
        /// <param name="rootObject">The root <see cref="Record"/> of the query.</param>
        /// <param name="binding">The <see cref="RecordBinding"/> that resolves the field to its <see cref="FieldBinding"/>.</param>
        /// <param name="fieldSubset">
        /// The current <see cref="FieldSubset"/>; returned with this term's field appended.
        /// </param>
        /// <returns>The updated <see cref="FieldSubset"/>.</returns>
        public virtual FieldSubset IncludeInFieldSubset(Record rootObject, RecordBinding binding, FieldSubset fieldSubset)
        {
            FieldBinding  fb              = GetTermFieldInfo(binding);
            Record    containingObject = binding.GetContainingObjectForField(fb, rootObject);
            TField        containedField  = (TField)fb.Info.FieldInfo.GetValue(containingObject);
            fieldSubset += rootObject.FieldSubset(containingObject, containedField);
            return fieldSubset;
        }

        // ── Debug helpers ─────────────────────────────────────────────────────────────

        /// <summary>
        /// Appends a human-readable description of this term's parameters to <paramref name="result"/>.
        /// Used only for diagnostic/logging output.
        /// </summary>
        /// <param name="obj">The <see cref="Record"/> instance to read the current field value from.</param>
        /// <param name="binding">The <see cref="RecordBinding"/> that resolves the field to its <see cref="FieldBinding"/>.</param>
        /// <param name="n">Running count of parameters appended; incremented once by this method.</param>
        /// <param name="result">
        /// Accumulator string to which a description of the form <c>fieldName=[value]</c>
        /// is appended. Failures are silently swallowed because this is best-effort output.
        /// </param>
        public virtual void GetParameterDebugInfo(Record obj, RecordBinding binding, ref int n, ref string result)
        {
            // Single-value leaf terms: emit "fieldName=value"
            if (Field == null || binding == null) return;
            try
            {
                FieldBinding fb = GetTermFieldInfo(binding);
                if (fb == null) return;
                object val = fb.Info?.GetValue(obj);
                string sep = result.Length > 0 ? ", " : "";
                result += sep + fb.Info?.TargetName + "=[" + (val?.ToString() ?? "null") + "]";
                n++;
            }
            catch { /* best-effort debug output */ }
        }

        // ── Boolean composition operators ────────────────────────────────────────────

        /// <summary>
        /// Combines two terms with SQL <c>AND</c>, returning a new <see cref="AndTerm"/>.
        /// If either operand is <see langword="null"/> the non-null operand is returned unchanged,
        /// making it safe to build up terms incrementally starting from <see langword="null"/>.
        /// </summary>
        /// <param name="t1">The left-hand predicate, or <see langword="null"/>.</param>
        /// <param name="t2">The right-hand predicate, or <see langword="null"/>.</param>
        public static QueryTerm operator &(QueryTerm t1, QueryTerm t2)
        {
            if (t1 == null) return t2;
            if (t2 == null) return t1;
            return new AndTerm(t1, t2);
        }

        /// <summary>
        /// Combines two terms with SQL <c>OR</c>, returning a new <see cref="OrTerm"/>.
        /// If either operand is <see langword="null"/> the non-null operand is returned unchanged.
        /// </summary>
        /// <param name="t1">The left-hand predicate, or <see langword="null"/>.</param>
        /// <param name="t2">The right-hand predicate, or <see langword="null"/>.</param>
        public static QueryTerm operator |(QueryTerm t1, QueryTerm t2)
        {
            if (t1 == null) return t2;
            if (t2 == null) return t1;
            return new OrTerm(t1, t2);
        }

        /// <summary>
        /// Negates a term with SQL <c>NOT</c>, returning a new <see cref="NotTerm"/>.
        /// </summary>
        /// <param name="t1">The predicate to negate.</param>
        public static QueryTerm operator !(QueryTerm t1)
            => new NotTerm(t1);
    }
}
