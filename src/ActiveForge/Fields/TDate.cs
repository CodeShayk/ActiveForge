using System;
using System.Diagnostics;

namespace ActiveForge
{
    /// <summary>
    /// Database field that wraps the date-only portion of a CLR <see cref="DateTime"/>
    /// value, mapping to a SQL DATE (or equivalent date-only) column.
    /// <para>
    /// The time component is always stripped on assignment: any <see cref="DateTime"/>
    /// passed to <see cref="SetValue"/> or the implicit cast operator is truncated to
    /// midnight (<c>DateTime.Date</c>) before storage.  This ensures round-trip
    /// consistency with database engines that store DATE without a time portion.
    /// </para>
    /// <para>
    /// Use <c>SetValue</c> / <c>GetValue()</c> to assign and retrieve the
    /// value programmatically, or use the implicit conversion operators to work with
    /// plain <see cref="DateTime"/> values directly.
    /// </para>
    /// <example>
    /// <code>
    /// record.BirthDate.SetValue(new DateTime(1990, 6, 15));
    /// DateTime d = record.BirthDate;          // implicit cast to DateTime (time = 00:00:00)
    /// record.BirthDate = DateTime.Today;      // implicit cast from DateTime
    /// </code>
    /// </example>
    /// </summary>
    [Serializable]
    [DebuggerDisplay("Null={Null} Value={InnerValue}")]
    public class TDate : TBaseDateTime
    {
        /// <summary>
        /// Initialises a new <see cref="TDate"/> in the null state with
        /// <c>InnerValue</c> set to <see cref="DateTime.MinValue"/>.
        /// </summary>
        public TDate()           { }

        /// <summary>
        /// Initialises a new <see cref="TDate"/> from a <see cref="DateTime"/>.
        /// Only the date portion (<c>v.Date</c>) is stored; the time component is discarded.
        /// The field is immediately non-null.
        /// </summary>
        /// <param name="v">The date value; the time component is ignored.</param>
        public TDate(DateTime v) { SetValue(v.Date); }

        /// <summary>
        /// Initialises a new <see cref="TDate"/> from an untyped object via
        /// <see cref="TField.SetValue(object)"/>.  Supports any value that the base
        /// <see cref="TBaseDateTime.SetDerivedValue"/> can convert; the time component
        /// is stripped after conversion.
        /// </summary>
        /// <param name="v">The value to convert and store.</param>
        public TDate(object v)   { SetValue(v); }

        /// <summary>
        /// Implicitly converts a <see cref="TDate"/> field to a plain <see cref="DateTime"/>.
        /// The returned value always has a time component of <c>00:00:00</c>.
        /// Returns <c>InnerValue</c> directly without a null-guard; callers should check
        /// <see cref="TField.IsNull()"/> first if the field may be null.
        /// </summary>
        /// <param name="t">The source <see cref="TDate"/> field.</param>
        /// <returns>The underlying date value (time component = midnight).</returns>
        public static implicit operator DateTime(TDate t) => t.InnerValue;

        /// <summary>
        /// Implicitly wraps a plain <see cref="DateTime"/> in a new non-null
        /// <see cref="TDate"/> field.  The time component of <paramref name="v"/>
        /// is discarded.
        /// </summary>
        /// <param name="v">The date/time value to wrap; the time portion is ignored.</param>
        /// <returns>A new <see cref="TDate"/> containing the date portion of <paramref name="v"/>.</returns>
        public static implicit operator TDate(DateTime v) => new TDate(v);

        /// <summary>Determines whether two <see cref="TDate"/> fields are equal.</summary>
        public static bool operator ==(TDate o1, TDate o2) => EqualityOperatorHelper<TDate>(o1, o2);

        /// <summary>Determines whether two <see cref="TDate"/> fields are not equal.</summary>
        public static bool operator !=(TDate o1, TDate o2) => !(o1 == o2);

        /// <summary>Determines whether the left <see cref="TDate"/> is later than the right.</summary>
        public static bool operator >(TDate  o1, TDate o2) => GTHelper<TDate>(o1, o2);

        /// <summary>Determines whether the left <see cref="TDate"/> is earlier than the right.</summary>
        public static bool operator <(TDate  o1, TDate o2) => LTHelper<TDate>(o1, o2);

        /// <summary>Determines whether the left <see cref="TDate"/> is later than or equal to the right.</summary>
        public static bool operator >=(TDate o1, TDate o2) => o1 > o2 || o1 == o2;

        /// <summary>Determines whether the left <see cref="TDate"/> is earlier than or equal to the right.</summary>
        public static bool operator <=(TDate o1, TDate o2) => o1 < o2 || o1 == o2;

        /// <summary>Returns <c>typeof(DateTime)</c>.</summary>
        public override Type   GetUnderlyingType()  => typeof(DateTime);

        /// <summary>Returns <c>"date"</c>, the short database type identifier for this field.</summary>
        public override string GetTypeDescription()  => "date";

        /// <summary>
        /// Sets <c>InnerValue</c> from <paramref name="value"/> via the base
        /// <see cref="TBaseDateTime.SetDerivedValue"/> conversion, then strips the time
        /// component so only the date portion (<c>InnerValue.Date</c>) is retained.
        /// </summary>
        /// <param name="value">The non-null source value.</param>
        public override void SetDerivedValue(object value)
        {
            base.SetDerivedValue(value);
            InnerValue = InnerValue.Date;  // strip time
        }

        /// <summary>
        /// Determines whether this field equals <paramref name="obj"/>, using consistent
        /// null semantics and comparing only the date component.
        /// </summary>
        /// <param name="obj">The object to compare against.</param>
        /// <returns><c>true</c> if equal; otherwise <c>false</c>.</returns>
        public override bool Equals(object obj) => EqualsHelper<TDate, DateTime>(obj);

        /// <summary>
        /// Returns a hash code for this field based on the date portion only.
        /// Null-state fields return <c>0</c>.
        /// </summary>
        public override int  GetHashCode()      => IsNull() ? 0 : InnerValue.Date.GetHashCode();
    }
}
