using System;

namespace ActiveForge.Attributes
{
    /// <summary>
    /// Overrides the automatic FK→join convention for an embedded Record field.
    /// Without this attribute, the ORM looks for a field named <c>XID</c> where X is
    /// the embedded object's field name.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field, AllowMultiple = false, Inherited = false)]
    public class JoinAttribute : Attribute
    {
        public enum JoinTypeEnum
        {
            InnerJoin      = 1,
            LeftOuterJoin  = 2,
            RightOuterJoin = 3,
        }

        public JoinAttribute() { }

        public JoinAttribute(string foreignKey, string targetField, JoinTypeEnum joinType = JoinTypeEnum.InnerJoin)
        {
            ForeignKey  = foreignKey;
            TargetField = targetField;
            JoinType    = joinType;
        }

        public string      ForeignKey  { get; set; } = "";
        public string      TargetField { get; set; } = "";
        public JoinTypeEnum JoinType   { get; set; } = JoinTypeEnum.InnerJoin;
    }
}
