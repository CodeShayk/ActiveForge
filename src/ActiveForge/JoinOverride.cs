using System;

namespace ActiveForge
{
    /// <summary>
    /// A query-time override that changes the join type for one embedded
    /// <see cref="Record"/> type within a LINQ query.
    ///
    /// <para>
    /// Created by <see cref="ActiveForge.Linq.OrmQueryable{T}.InnerJoin{TJoined}"/> and
    /// <see cref="ActiveForge.Linq.OrmQueryable{T}.LeftOuterJoin{TJoined}"/> and applied
    /// at execution time to override the join type that would otherwise be inferred from
    /// <c>[JoinSpec]</c> attributes or naming conventions on the entity class.
    /// </para>
    /// </summary>
    public readonly struct JoinOverride
    {
        /// <summary>The <see cref="Record"/> subclass whose join type is overridden.</summary>
        public readonly Type TargetType;

        /// <summary>The join type to apply at query time.</summary>
        public readonly JoinSpecification.JoinTypeEnum JoinType;

        public JoinOverride(Type targetType, JoinSpecification.JoinTypeEnum joinType)
        {
            TargetType = targetType ?? throw new ArgumentNullException(nameof(targetType));
            JoinType   = joinType;
        }
    }
}
