using System.Reflection;
using ActiveForge.Attributes;

namespace ActiveForge.Query
{
    /// <summary>
    /// Abstract base for all composable WHERE-clause predicates.
    /// Subclasses implement <see cref="GetSQL"/> and <see cref="GetDeleteSQL"/> to emit
    /// the parameterised SQL fragment, and <see cref="BindParameters"/> to add the
    /// corresponding <see cref="CommandBase"/> parameters.
    /// </summary>
    public abstract class QueryTerm
    {
        protected FieldInfo   FieldInfo       = null;
        protected DataObject  Target          = null;
        protected TField      Field           = null;
        public    object      Value           = null;
        protected string      ParameterMark;
        protected string      StringConnectionOperator;

        protected QueryTerm() { }

        protected QueryTerm(DataObject target, TField field, object value)
            => Initialize(target, field, value);

        protected QueryTerm(DataObject target, TString stringField, object value)
            => Initialize(target, stringField, value);

        // ── Abstract contract ────────────────────────────────────────────────────────

        public abstract QueryFragment GetSQL(ObjectBinding binding, ref int termNumber);
        public abstract string        GetDeleteSQL(ObjectBinding binding, ref int termNumber);

        // ── Initialisation ───────────────────────────────────────────────────────────

        protected void Initialize(DataObject target, TField field, object value)
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

        protected void Initialize(DataObject target, TString stringField, object value)
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

        public virtual void BindParameters(DataObject obj, ObjectBinding binding, CommandBase command, ref int termNumber)
        {
            FieldBinding fb        = GetTermFieldInfo(binding);
            object       bindValue = GetBindValue(fb.Info, obj, Binding: binding);
            string       paramName = ParameterMark + "IN_" + fb.Info.TargetName + (termNumber++);
            command.AddParameter(paramName, bindValue, fb.Info);
        }

        public virtual string GetQueryValues(DataObject obj, ObjectBinding binding)
        {
            FieldBinding fb        = GetTermFieldInfo(binding);
            object       bindValue = GetBindValue(fb.Info, obj, Binding: binding);
            return "[" + bindValue + "]";
        }

        // ── Helpers ──────────────────────────────────────────────────────────────────

        public FieldBinding GetTermFieldInfo(ObjectBinding binding)
            => binding.GetFieldBinding(Target, Field);

        protected virtual object GetBindValue(TargetFieldInfo info, DataObject obj, ObjectBinding Binding)
            => GetBindValue(info, obj, Value);

        protected virtual object GetBindValue(TargetFieldInfo info, DataObject obj, object valueToConvert)
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

        public virtual bool IncludesLookupDataObject(DataObject rootObject)
        {
            if (Target == null) return false;
            if (Target is LookupDataObject) return true;

            DataObject containing = rootObject.GetContainingDataObject(Target);
            while (containing != null && containing != rootObject)
            {
                if (containing is LookupDataObject) return true;
                containing = rootObject.GetContainingDataObject(containing);
            }
            return false;
        }

        public virtual FieldSubset IncludeInFieldSubset(DataObject rootObject, ObjectBinding binding, FieldSubset fieldSubset)
        {
            FieldBinding  fb              = GetTermFieldInfo(binding);
            DataObject    containingObject = binding.GetContainingObjectForField(fb, rootObject);
            TField        containedField  = (TField)fb.Info.FieldInfo.GetValue(containingObject);
            fieldSubset += rootObject.FieldSubset(containingObject, containedField);
            return fieldSubset;
        }

        // ── Debug helpers ─────────────────────────────────────────────────────────────

        /// <summary>
        /// Appends a human-readable description of this term's parameters to <paramref name="result"/>.
        /// Used only for diagnostic/logging output.
        /// </summary>
        public virtual void GetParameterDebugInfo(DataObject obj, ObjectBinding binding, ref int n, ref string result)
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

        public static QueryTerm operator &(QueryTerm t1, QueryTerm t2)
        {
            if (t1 == null) return t2;
            if (t2 == null) return t1;
            return new AndTerm(t1, t2);
        }

        public static QueryTerm operator |(QueryTerm t1, QueryTerm t2)
        {
            if (t1 == null) return t2;
            if (t2 == null) return t1;
            return new OrTerm(t1, t2);
        }

        public static QueryTerm operator !(QueryTerm t1)
            => new NotTerm(t1);
    }
}
