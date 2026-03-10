using System.Collections.Generic;

namespace ActiveForge.Query
{
    /// <summary>field IN (@p1, @p2, ...)</summary>
    public class InTerm : QueryTerm
    {
        public InTerm(DataObject target, TField field, IList<object> values)
            : base(target, field, values) { }

        public override QueryFragment GetSQL(ObjectBinding binding, ref int termNumber)
        {
            FieldBinding check    = GetTermFieldInfo(binding);
            string       aliasSQL = check.MapNode.Alias.Length > 0 ? check.MapNode.Alias + "." : "";
            string       inList   = BuildList(check, ref termNumber);
            return new QueryFragment(aliasSQL + Target.GetConnection().QuoteName(check.Info.TargetName)
                + " IN(" + inList + ")");
        }

        public override string GetDeleteSQL(ObjectBinding binding, ref int termNumber)
        {
            FieldBinding check  = GetTermFieldInfo(binding);
            string       inList = BuildList(check, ref termNumber);
            return Target.GetConnection().QuoteName(check.Info.SourceName) + "."
                + Target.GetConnection().QuoteName(check.Info.TargetName)
                + " IN(" + inList + ")";
        }

        public override void BindParameters(DataObject obj, ObjectBinding binding, CommandBase command, ref int termNumber)
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

        public override void GetParameterDebugInfo(DataObject obj, ObjectBinding binding, ref int n, ref string result)
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
