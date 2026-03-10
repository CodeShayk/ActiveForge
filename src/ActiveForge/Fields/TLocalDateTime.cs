using System;
using System.Diagnostics;

namespace ActiveForge
{
    /// <summary>
    /// ORM field that stores a local date and time (without UTC conversion), using the CLR
    /// <see cref="DateTime"/> type internally and mapping to a DATETIME / TIMESTAMP database column.
    /// <para>
    /// Values are interpreted in the local machine timezone; no UTC conversion is applied
    /// when reading from or writing to the database.  Use <see cref="TUtcDateTime"/> when
    /// UTC semantics are required.
    /// </para>
    /// <para>
    /// Use <see cref="TField.SetValue(object)"/> or the typed overload inherited from
    /// <c>TDateTimeBase</c> to assign a value, <see cref="TField.GetValue()"/> to retrieve
    /// it as <see cref="object"/>, or rely on the implicit conversions between
    /// <c>TLocalDateTime</c> and <c>DateTime</c> for transparent usage.
    /// </para>
    /// </summary>
    [Serializable]
    [DebuggerDisplay("Null={Null} Value={InnerValue}")]
    public class TLocalDateTime : TDateTimeBase
    {
        /// <summary>Initialises a new null <see cref="TLocalDateTime"/>.</summary>
        public TLocalDateTime()           { }

        /// <summary>Initialises a new <see cref="TLocalDateTime"/> with the specified <paramref name="v"/> value.</summary>
        /// <param name="v">The initial date-time value.</param>
        public TLocalDateTime(DateTime v) { SetValue(v); }

        /// <summary>
        /// Initialises a new <see cref="TLocalDateTime"/> by converting <paramref name="v"/> using
        /// <see cref="TField.SetValue(object)"/>.
        /// </summary>
        /// <param name="v">Any value that can be converted to <see cref="DateTime"/>.</param>
        public TLocalDateTime(object v)   { SetValue(v); }

        /// <summary>Implicitly converts a <see cref="TLocalDateTime"/> to its underlying <see cref="DateTime"/> value.</summary>
        /// <param name="t">The field instance to convert.</param>
        public static implicit operator DateTime(TLocalDateTime t) => t.InnerValue;

        /// <summary>Implicitly wraps a <see cref="DateTime"/> in a new <see cref="TLocalDateTime"/>.</summary>
        /// <param name="v">The date-time value to wrap.</param>
        public static implicit operator TLocalDateTime(DateTime v) => new TLocalDateTime(v);

        /// <summary>Returns <see langword="true"/> when both operands are equal (null-aware).</summary>
        public static bool operator ==(TLocalDateTime o1, TLocalDateTime o2) => EqualityOperatorHelper<TLocalDateTime>(o1, o2);

        /// <summary>Returns <see langword="true"/> when the operands are not equal.</summary>
        public static bool operator !=(TLocalDateTime o1, TLocalDateTime o2) => !(o1 == o2);

        /// <summary>Returns <see cref="DateTime"/> as the underlying CLR type of this field.</summary>
        public override Type   GetUnderlyingType()  => typeof(DateTime);

        /// <summary>Returns the string token <c>"localdatetime"</c> used by the ORM schema system.</summary>
        public override string GetTypeDescription()  => "localdatetime";

        /// <summary>Determines value equality using the shared ORM equality helper.</summary>
        public override bool Equals(object obj) => EqualsHelper<TLocalDateTime, DateTime>(obj);

        /// <summary>Returns zero when null; otherwise delegates to <see cref="DateTime.GetHashCode()"/>.</summary>
        public override int  GetHashCode()      => IsNull() ? 0 : InnerValue.GetHashCode();
    }
}
