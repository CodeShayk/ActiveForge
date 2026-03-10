using System.Collections.Generic;

namespace ActiveForge
{
    /// <summary>
    /// Describes a single SQL JOIN between two tables as resolved by the ORM binding layer.
    /// Instances are built from <see cref="Attributes.JoinAttribute"/> / <see cref="Attributes.JoinSpecAttribute"/>
    /// declarations or from the embedded-<c>DataObject</c> naming convention and are consumed by
    /// <c>DBDataConnection</c> when generating SELECT SQL.
    /// </summary>
    public class JoinSpecification
    {
        /// <summary>
        /// SQL JOIN type that controls how non-matching rows are handled.
        /// </summary>
        public enum JoinTypeEnum
        {
            /// <summary>Only rows with a match in both tables are returned.</summary>
            InnerJoin      = 1,
            /// <summary>All rows from the source (left) table are returned; unmatched target columns are NULL.</summary>
            LeftOuterJoin  = 2,
            /// <summary>All rows from the target (right) table are returned; unmatched source columns are NULL.</summary>
            RightOuterJoin = 3,
        }

        /// <summary>The SQL alias of the source (outer) table in the JOIN clause.</summary>
        public string        SourceAlias;

        /// <summary>The table name (or view name) being joined from the source side.</summary>
        public string        JoinSource;

        /// <summary>The column name on the source table used as the JOIN key (e.g. the foreign-key column).</summary>
        public string        JoinSourceField;

        /// <summary>The SQL alias of the target (inner) table in the JOIN clause.</summary>
        public string        TargetAlias;

        /// <summary>The table name (or view name) of the target table being joined.</summary>
        public string        JoinTarget;

        /// <summary>The column name on the target table used as the JOIN key (e.g. the primary-key column).</summary>
        public string        JoinTargetField;

        /// <summary>The SQL JOIN type (<c>INNER JOIN</c>, <c>LEFT OUTER JOIN</c>, or <c>RIGHT OUTER JOIN</c>).</summary>
        public JoinTypeEnum  JoinType;

        /// <summary>
        /// When <c>true</c>, the join source is a SQL function rather than a plain table or view.
        /// </summary>
        public bool          Function    = false;

        /// <summary>
        /// The CLR type of the entity mapped to the join target table.
        /// Used by <c>ApplyJoinOverrides</c> to match a <see cref="JoinOverride"/> against this specification.
        /// </summary>
        public System.Type   JoinTargetClass = null;

        /// <summary>
        /// Name of a temporary table used when the join source is materialised as a temp table.
        /// Empty string when not applicable.
        /// </summary>
        public string        TempTableName   = "";

        /// <summary>
        /// Returns <c>true</c> if this specification is structurally equivalent to any entry in
        /// <paramref name="specs"/> (alias and column names compared case-insensitively).
        /// </summary>
        /// <param name="specs">The list of existing specifications to check against.</param>
        public bool InList(List<JoinSpecification> specs)
        {
            foreach (var s in specs)
                if (ValueCompare(s)) return true;
            return false;
        }

        /// <summary>
        /// Performs a case-insensitive structural comparison with <paramref name="other"/>.
        /// Two specifications are considered equal if their source alias, source table, source field,
        /// target alias, and target field all match — join type and CLR type are not compared.
        /// </summary>
        /// <param name="other">The specification to compare with.</param>
        /// <returns><c>true</c> when structurally identical; otherwise <c>false</c>.</returns>
        public bool ValueCompare(JoinSpecification other)
            => string.Compare(SourceAlias,      other.SourceAlias,      true) == 0
            && string.Compare(JoinSource,        other.JoinSource,        true) == 0
            && string.Compare(JoinSourceField,   other.JoinSourceField,   true) == 0
            && string.Compare(TargetAlias,       other.TargetAlias,       true) == 0
            && string.Compare(JoinTargetField,   other.JoinTargetField,   true) == 0;

        // ── Legacy DBJoinAttribute mapping ───────────────────────────────────────────

        /// <summary>
        /// Converts a <see cref="Attributes.JoinAttribute.JoinTypeEnum"/> value (from the legacy
        /// <c>[Join]</c> attribute) into the canonical <see cref="JoinTypeEnum"/> used by the query engine.
        /// </summary>
        /// <param name="value">The join type declared on a <c>[Join]</c> attribute.</param>
        /// <returns>The corresponding <see cref="JoinTypeEnum"/> value.</returns>
        public static JoinTypeEnum MapJoinType(Attributes.JoinAttribute.JoinTypeEnum value) =>
            value switch
            {
                Attributes.JoinAttribute.JoinTypeEnum.LeftOuterJoin  => JoinTypeEnum.LeftOuterJoin,
                Attributes.JoinAttribute.JoinTypeEnum.RightOuterJoin => JoinTypeEnum.RightOuterJoin,
                _                                                    => JoinTypeEnum.InnerJoin,
            };

        /// <summary>
        /// Converts a <see cref="Attributes.JoinSpecAttribute.JoinTypeEnum"/> value (from the
        /// <c>[JoinSpec]</c> attribute) into the canonical <see cref="JoinTypeEnum"/> used by the query engine.
        /// </summary>
        /// <param name="value">The join type declared on a <c>[JoinSpec]</c> attribute.</param>
        /// <returns>The corresponding <see cref="JoinTypeEnum"/> value.</returns>
        public static JoinTypeEnum MapJoinType(Attributes.JoinSpecAttribute.JoinTypeEnum value) =>
            value switch
            {
                Attributes.JoinSpecAttribute.JoinTypeEnum.LeftOuterJoin  => JoinTypeEnum.LeftOuterJoin,
                Attributes.JoinSpecAttribute.JoinTypeEnum.RightOuterJoin => JoinTypeEnum.RightOuterJoin,
                _                                                        => JoinTypeEnum.InnerJoin,
            };
    }

    /// <summary>
    /// A specialised <see cref="JoinSpecification"/> used for translation joins — i.e. joins that
    /// bring in a secondary table solely to resolve a translated/lookup value rather than to expose
    /// all of its columns in the result set.
    /// </summary>
    public class TranslationJoinSpecification : JoinSpecification { }
}
