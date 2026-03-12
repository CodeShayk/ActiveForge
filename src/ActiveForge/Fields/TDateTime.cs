using System;
using System.Diagnostics;

namespace ActiveForge
{
    /// <summary>
    /// Database field that wraps a CLR <see cref="DateTime"/> value (including both
    /// date and time components), mapping to a SQL DATETIME (or equivalent) column.
    /// <para>
    /// The full date-and-time value is stored as-is; no truncation or timezone
    /// conversion is applied.  For UTC-normalised storage use <c>TUtcDateTime</c>;
    /// for date-only storage use <see cref="TDate"/>.
    /// </para>
    /// <para>
    /// Use <c>SetValue(DateTime)</c> / <c>GetValue()</c> to assign and retrieve the
    /// value programmatically, or use the implicit conversion operators to work with
    /// plain <see cref="DateTime"/> values directly.
    /// </para>
    /// <example>
    /// <code>
    /// record.CreatedAt.SetValue(DateTime.Now);
    /// DateTime dt = record.CreatedAt;         // implicit cast to DateTime
    /// record.CreatedAt = DateTime.UtcNow;     // implicit cast from DateTime
    /// </code>
    /// </example>
    /// </summary>
    [Serializable]
    [DebuggerDisplay("Null={Null} Value={InnerValue}")]
    public class TDateTime : TBaseDateTime
    {
        /// <summary>
        /// Initialises a new <see cref="TDateTime"/> in the null state with
        /// <c>InnerValue</c> set to <see cref="DateTime.MinValue"/>.
        /// </summary>
        public TDateTime()           { }

        /// <summary>
        /// Initialises a new <see cref="TDateTime"/> with the specified date-and-time
        /// value.  The full value (date and time components) is stored.
        /// The field is immediately non-null.
        /// </summary>
        /// <param name="v">The initial date-and-time value.</param>
        public TDateTime(DateTime v) { SetValue(v); }

        /// <summary>
        /// Initialises a new <see cref="TDateTime"/> from an untyped object via
        /// <see cref="TField.SetValue(object)"/>.  Supports any value that
        /// <see cref="TBaseDateTime.SetDerivedValue"/> can convert (another
        /// <see cref="TBaseDateTime"/>, a plain <see cref="DateTime"/>, or any object
        /// that <see cref="Convert.ToDateTime(object)"/> can handle).
        /// </summary>
        /// <param name="v">The value to convert and store.</param>
        public TDateTime(object v)   { SetValue(v); }

        /// <summary>
        /// Implicitly converts a <see cref="TDateTime"/> field to a plain
        /// <see cref="DateTime"/>.  Returns <c>InnerValue</c> directly without a
        /// null-guard; callers should check <see cref="TField.IsNull()"/> first if
        /// the field may be null.
        /// </summary>
        /// <param name="t">The source <see cref="TDateTime"/> field.</param>
        /// <returns>The underlying date-and-time value.</returns>
        public static implicit operator DateTime(TDateTime t) => t.InnerValue;

        /// <summary>
        /// Implicitly wraps a plain <see cref="DateTime"/> in a new non-null
        /// <see cref="TDateTime"/> field.
        /// </summary>
        /// <param name="v">The date-and-time value to wrap.</param>
        /// <returns>A new <see cref="TDateTime"/> containing <paramref name="v"/>.</returns>
        public static implicit operator TDateTime(DateTime v) => new TDateTime(v);

        /// <summary>Determines whether two <see cref="TDateTime"/> fields are equal.</summary>
        public static bool operator ==(TDateTime o1, TDateTime o2) => EqualityOperatorHelper<TDateTime>(o1, o2);

        /// <summary>Determines whether two <see cref="TDateTime"/> fields are not equal.</summary>
        public static bool operator !=(TDateTime o1, TDateTime o2) => !(o1 == o2);

        /// <summary>Determines whether the left <see cref="TDateTime"/> is later than the right.</summary>
        public static bool operator >(TDateTime  o1, TDateTime o2) => GTHelper<TDateTime>(o1, o2);

        /// <summary>Determines whether the left <see cref="TDateTime"/> is earlier than the right.</summary>
        public static bool operator <(TDateTime  o1, TDateTime o2) => LTHelper<TDateTime>(o1, o2);

        /// <summary>Determines whether the left <see cref="TDateTime"/> is later than or equal to the right.</summary>
        public static bool operator >=(TDateTime o1, TDateTime o2) => o1 > o2 || o1 == o2;

        /// <summary>Determines whether the left <see cref="TDateTime"/> is earlier than or equal to the right.</summary>
        public static bool operator <=(TDateTime o1, TDateTime o2) => o1 < o2 || o1 == o2;

        /// <summary>Returns <c>typeof(DateTime)</c>.</summary>
        public override Type   GetUnderlyingType()  => typeof(DateTime);

        /// <summary>Returns <c>"datetime"</c>, the short database type identifier for this field.</summary>
        public override string GetTypeDescription()  => "datetime";

        /// <summary>
        /// Determines whether this field equals <paramref name="obj"/>, using consistent
        /// null semantics and comparing both date and time components.
        /// </summary>
        /// <param name="obj">The object to compare against.</param>
        /// <returns><c>true</c> if equal; otherwise <c>false</c>.</returns>
        public override bool Equals(object obj) => EqualsHelper<TDateTime, DateTime>(obj);

        /// <summary>
        /// Returns a hash code for this field.  Null-state fields return <c>0</c>;
        /// non-null fields return the hash code of the full <see cref="DateTime"/> value.
        /// </summary>
        public override int  GetHashCode()      => IsNull() ? 0 : InnerValue.GetHashCode();
    }
}
