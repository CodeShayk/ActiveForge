using System.Collections.Generic;

namespace ActiveForge
{
    public class JoinSpecification
    {
        public enum JoinTypeEnum
        {
            InnerJoin      = 1,
            LeftOuterJoin  = 2,
            RightOuterJoin = 3,
        }

        public string        SourceAlias;
        public string        JoinSource;
        public string        JoinSourceField;
        public string        TargetAlias;
        public string        JoinTarget;
        public string        JoinTargetField;
        public JoinTypeEnum  JoinType;
        public bool          Function    = false;
        public System.Type   JoinTargetClass = null;
        public string        TempTableName   = "";

        public bool InList(List<JoinSpecification> specs)
        {
            foreach (var s in specs)
                if (ValueCompare(s)) return true;
            return false;
        }

        public bool ValueCompare(JoinSpecification other)
            => string.Compare(SourceAlias,      other.SourceAlias,      true) == 0
            && string.Compare(JoinSource,        other.JoinSource,        true) == 0
            && string.Compare(JoinSourceField,   other.JoinSourceField,   true) == 0
            && string.Compare(TargetAlias,       other.TargetAlias,       true) == 0
            && string.Compare(JoinTargetField,   other.JoinTargetField,   true) == 0;

        // ── Legacy DBJoinAttribute mapping ───────────────────────────────────────────

        public static JoinTypeEnum MapJoinType(Attributes.JoinAttribute.JoinTypeEnum value) =>
            value switch
            {
                Attributes.JoinAttribute.JoinTypeEnum.LeftOuterJoin  => JoinTypeEnum.LeftOuterJoin,
                Attributes.JoinAttribute.JoinTypeEnum.RightOuterJoin => JoinTypeEnum.RightOuterJoin,
                _                                                    => JoinTypeEnum.InnerJoin,
            };

        public static JoinTypeEnum MapJoinType(Attributes.JoinSpecAttribute.JoinTypeEnum value) =>
            value switch
            {
                Attributes.JoinSpecAttribute.JoinTypeEnum.LeftOuterJoin  => JoinTypeEnum.LeftOuterJoin,
                Attributes.JoinSpecAttribute.JoinTypeEnum.RightOuterJoin => JoinTypeEnum.RightOuterJoin,
                _                                                        => JoinTypeEnum.InnerJoin,
            };
    }

    public class TranslationJoinSpecification : JoinSpecification { }
}
