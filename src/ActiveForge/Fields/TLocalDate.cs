using System;
using System.Diagnostics;

namespace ActiveForge
{
    /// <summary>
    /// ORM field that stores a calendar date without a time-of-day component, using the CLR
    /// <see cref="DateTime"/> type internally and mapping to a DATE database column.
    /// <para>
    /// Values are treated as local (machine-timezone) calendar dates; no UTC conversion is
    /// applied.  Only the date portion of any supplied <see cref="DateTime"/> is meaningful;
    /// the time-of-day component is stored but ignored by database DATE columns.
    /// </para>
    /// <para>
    /// Use <see cref="TField.SetValue(object)"/> or the typed overload inherited from
    /// <c>TBaseDateTime</c> to assign a value, <see cref="TField.GetValue()"/> to retrieve
    /// it as <see cref="object"/>, or rely on the implicit conversions between
    /// <c>TLocalDate</c> and <c>DateTime</c> for transparent usage.
    /// </para>
    /// </summary>
    [Serializable]
    [DebuggerDisplay("Null={Null} Value={InnerValue}")]
    public class TLocalDate : TBaseDateTime
    {
        /// <summary>Initialises a new null <see cref="TLocalDate"/>.</summary>
        public TLocalDate()           { }

        /// <summary>Initialises a new <see cref="TLocalDate"/> with the specified <paramref name="v"/> value.</summary>
        /// <param name="v">The initial date value.</param>
        public TLocalDate(DateTime v) { SetValue(v); }

        /// <summary>
        /// Initialises a new <see cref="TLocalDate"/> by converting <paramref name="v"/> using
        /// <see cref="TField.SetValue(object)"/>.
        /// </summary>
        /// <param name="v">Any value that can be converted to <see cref="DateTime"/>.</param>
        public TLocalDate(object v)   { SetValue(v); }

        /// <summary>Implicitly converts a <see cref="TLocalDate"/> to its underlying <see cref="DateTime"/> value.</summary>
        /// <param name="t">The field instance to convert.</param>
        public static implicit operator DateTime(TLocalDate t) => t.InnerValue;

        /// <summary>Implicitly wraps a <see cref="DateTime"/> in a new <see cref="TLocalDate"/>.</summary>
        /// <param name="v">The date value to wrap.</param>
        public static implicit operator TLocalDate(DateTime v) => new TLocalDate(v);

        /// <summary>Returns <see langword="true"/> when both operands are equal (null-aware).</summary>
        public static bool operator ==(TLocalDate o1, TLocalDate o2) => EqualityOperatorHelper<TLocalDate>(o1, o2);

        /// <summary>Returns <see langword="true"/> when the operands are not equal.</summary>
        public static bool operator !=(TLocalDate o1, TLocalDate o2) => !(o1 == o2);

        /// <summary>Returns <see cref="DateTime"/> as the underlying CLR type of this field.</summary>
        public override Type   GetUnderlyingType()  => typeof(DateTime);

        /// <summary>Returns the string token <c>"localdate"</c> used by the ORM schema system.</summary>
        public override string GetTypeDescription()  => "localdate";

        /// <summary>Determines value equality using the shared ORM equality helper.</summary>
        public override bool Equals(object obj) => EqualsHelper<TLocalDate, DateTime>(obj);

        /// <summary>Returns zero when null; otherwise delegates to <see cref="DateTime.GetHashCode()"/>.</summary>
        public override int  GetHashCode()      => IsNull() ? 0 : InnerValue.GetHashCode();
    }
}
