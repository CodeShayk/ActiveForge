using System;
using System.Diagnostics;

namespace ActiveForge
{
    /// <summary>
    /// ORM field that stores a UTC calendar date without a time-of-day component, using the
    /// CLR <see cref="DateTime"/> type internally and mapping to a DATE database column.
    /// <para>
    /// Values are expected to be in UTC; only the date portion of any supplied
    /// <see cref="DateTime"/> is meaningful — the time-of-day component is stored but
    /// ignored by database DATE columns.  Use <see cref="TLocalDate"/> when local-timezone
    /// semantics are required, or <see cref="TUtcDateTime"/> when a full UTC timestamp is needed.
    /// </para>
    /// <para>
    /// Use <see cref="TField.SetValue(object)"/> or the typed overload inherited from
    /// <c>TBaseDateTime</c> to assign a value, <see cref="TField.GetValue()"/> to retrieve
    /// it as <see cref="object"/>, or rely on the implicit conversions between
    /// <c>TUtcDate</c> and <c>DateTime</c> for transparent usage.
    /// </para>
    /// </summary>
    [Serializable]
    [DebuggerDisplay("Null={Null} Value={InnerValue}")]
    public class TUtcDate : TBaseDateTime
    {
        /// <summary>Initialises a new null <see cref="TUtcDate"/>.</summary>
        public TUtcDate()           { }

        /// <summary>Initialises a new <see cref="TUtcDate"/> with the specified <paramref name="v"/> value.</summary>
        /// <param name="v">The initial UTC date value.</param>
        public TUtcDate(DateTime v) { SetValue(v); }

        /// <summary>
        /// Initialises a new <see cref="TUtcDate"/> by converting <paramref name="v"/> using
        /// <see cref="TField.SetValue(object)"/>.
        /// </summary>
        /// <param name="v">Any value that can be converted to <see cref="DateTime"/>.</param>
        public TUtcDate(object v)   { SetValue(v); }

        /// <summary>Implicitly converts a <see cref="TUtcDate"/> to its underlying <see cref="DateTime"/> value.</summary>
        /// <param name="t">The field instance to convert.</param>
        public static implicit operator DateTime(TUtcDate t) => t.InnerValue;

        /// <summary>Implicitly wraps a <see cref="DateTime"/> in a new <see cref="TUtcDate"/>.</summary>
        /// <param name="v">The date value to wrap.</param>
        public static implicit operator TUtcDate(DateTime v) => new TUtcDate(v);

        /// <summary>Returns <see langword="true"/> when both operands are equal (null-aware).</summary>
        public static bool operator ==(TUtcDate o1, TUtcDate o2) => EqualityOperatorHelper<TUtcDate>(o1, o2);

        /// <summary>Returns <see langword="true"/> when the operands are not equal.</summary>
        public static bool operator !=(TUtcDate o1, TUtcDate o2) => !(o1 == o2);

        /// <summary>Returns <see cref="DateTime"/> as the underlying CLR type of this field.</summary>
        public override Type   GetUnderlyingType()  => typeof(DateTime);

        /// <summary>Returns the string token <c>"utcdate"</c> used by the ORM schema system.</summary>
        public override string GetTypeDescription()  => "utcdate";

        /// <summary>Determines value equality using the shared ORM equality helper.</summary>
        public override bool Equals(object obj) => EqualsHelper<TUtcDate, DateTime>(obj);

        /// <summary>Returns zero when null; otherwise delegates to <see cref="DateTime.GetHashCode()"/>.</summary>
        public override int  GetHashCode()      => IsNull() ? 0 : InnerValue.GetHashCode();
    }
}
