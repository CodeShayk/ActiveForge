using System;

namespace ActiveForge.Attributes
{
    /// <summary>
    /// Explicit join specification attached to a class or field when the automatic
    /// naming convention cannot resolve the relationship.
    /// Corresponds to the legacy <c>DBJoinSpecificationAttribute</c>.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Field, AllowMultiple = true, Inherited = false)]
    public class JoinSpecAttribute : Attribute
    {
        public enum JoinTypeEnum
        {
            InnerJoin      = 1,
            LeftOuterJoin  = 2,
            RightOuterJoin = 3,
        }

        public JoinSpecAttribute(string foreignKeyField, string targetField)
        {
            ForeignKeyField      = foreignKeyField;
            TargetField          = targetField;
            TargetPrimaryKeyField = "ID";
            JoinType             = JoinTypeEnum.InnerJoin;
        }

        public JoinSpecAttribute(string foreignKeyField, string targetField, string targetPrimaryKeyField, JoinTypeEnum joinType = JoinTypeEnum.InnerJoin)
        {
            ForeignKeyField       = foreignKeyField;
            TargetField           = targetField;
            TargetPrimaryKeyField = targetPrimaryKeyField;
            JoinType              = joinType;
        }

        public string      ForeignKeyField       { get; }
        public string      TargetField           { get; }
        public string      TargetPrimaryKeyField { get; }
        public JoinTypeEnum JoinType             { get; }
    }
}
