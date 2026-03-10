using System;
using System.Diagnostics;

namespace ActiveForge
{
    /// <summary>
    /// ORM field that stores a time-of-day value using the CLR <see cref="TimeSpan"/> type,
    /// mapping to a TIME database column.
    /// <para>
    /// Values represent durations up to 24 hours and are typically used to record the
    /// time portion of a date-time value.  When a <see cref="DateTime"/> is supplied via
    /// <see cref="SetDerivedValue"/>, only its <c>TimeOfDay</c> component is stored.
    /// String values are parsed with <see cref="TimeSpan.Parse(string)"/>.
    /// </para>
    /// <para>
    /// Use <see cref="TField.SetValue(object)"/> or the typed overload
    /// <see cref="SetValue(TimeSpan)"/> to assign a value, <see cref="TField.GetValue()"/>
    /// to retrieve it as <see cref="object"/>, or rely on the implicit conversions between
    /// <c>TTime</c> and <c>TimeSpan</c> for transparent usage.
    /// </para>
    /// </summary>
    [Serializable]
    [DebuggerDisplay("Null={Null} Value={InnerValue}")]
    public class TTime : TField, IComparable
    {
        /// <summary>Backing store for the field value.</summary>
        protected TimeSpan InnerValue;

        /// <summary>
        /// Gets or sets the field value, enforcing null/validity checks on read.
        /// </summary>
        protected TimeSpan Value { get { CheckValidity(); return InnerValue; } set { InnerValue = value; } }

        /// <summary>Initialises a new <see cref="TTime"/> set to <see cref="TimeSpan.Zero"/> and null state unset.</summary>
        public TTime()              { InnerValue = TimeSpan.Zero; }

        /// <summary>Initialises a new <see cref="TTime"/> with the specified <paramref name="v"/> value.</summary>
        /// <param name="v">The initial time value.</param>
        public TTime(TimeSpan v)    { SetValue(v); }

        /// <summary>
        /// Initialises a new <see cref="TTime"/> by converting <paramref name="v"/> using
        /// <see cref="TField.SetValue(object)"/>.
        /// </summary>
        /// <param name="v">A <see cref="TimeSpan"/>, a <see cref="DateTime"/> (only <c>TimeOfDay</c> is used),
        /// or a parseable string.</param>
        public TTime(object v)      { SetValue(v); }

        /// <summary>Implicitly converts a <see cref="TTime"/> to its underlying <see cref="TimeSpan"/> value.</summary>
        /// <param name="t">The field instance to convert.</param>
        public static implicit operator TimeSpan(TTime t) => t.InnerValue;

        /// <summary>Implicitly wraps a <see cref="TimeSpan"/> in a new <see cref="TTime"/>.</summary>
        /// <param name="v">The time value to wrap.</param>
        public static implicit operator TTime(TimeSpan v) => new TTime(v);

        /// <summary>Returns <see langword="true"/> when both operands are equal (null-aware).</summary>
        public static bool operator ==(TTime o1, TTime o2) => EqualityOperatorHelper<TTime>(o1, o2);

        /// <summary>Returns <see langword="true"/> when the operands are not equal.</summary>
        public static bool operator !=(TTime o1, TTime o2) => !(o1 == o2);

        /// <summary>Returns <see cref="TimeSpan"/> as the underlying CLR type of this field.</summary>
        public override Type   GetUnderlyingType()  => typeof(TimeSpan);

        /// <summary>Returns the string token <c>"time"</c> used by the ORM schema system.</summary>
        public override string GetTypeDescription()  => "time";

        /// <summary>Returns the current value as a boxed <see cref="TimeSpan"/>.</summary>
        public override object GetValue()            => InnerValue;

        /// <summary>
        /// Sets the backing value from an arbitrary <see cref="object"/>.
        /// Accepts a <see cref="TTime"/> (copies its inner value), a <see cref="TimeSpan"/>,
        /// a <see cref="DateTime"/> (extracts <c>TimeOfDay</c>), or a string parseable by
        /// <see cref="TimeSpan.Parse(string)"/>.
        /// </summary>
        /// <param name="value">The value to assign.</param>
        public override void SetDerivedValue(object value)
        {
            if (value is TTime tt)        InnerValue = tt.InnerValue;
            else if (value is TimeSpan ts) InnerValue = ts;
            else if (value is DateTime dt) InnerValue = dt.TimeOfDay;
            else                           InnerValue = TimeSpan.Parse(value.ToString());
        }

        /// <summary>
        /// Sets the field to the given <paramref name="value"/> and clears any conversion-error flag.
        /// </summary>
        /// <param name="value">The time value to store.</param>
        public void SetValue(TimeSpan value) { base.SetValue(value); ConversionError = false; }

        /// <summary>
        /// Marks the field as null or non-null.  When set to null the backing value is reset to <see cref="TimeSpan.Zero"/>.
        /// </summary>
        /// <param name="isNull"><see langword="true"/> to null the field; <see langword="false"/> to un-null it.</param>
        public override void SetNull(bool isNull) { base.SetNull(isNull); if (isNull) InnerValue = TimeSpan.Zero; }

        /// <summary>Returns the default string representation of the current <see cref="TimeSpan"/> value.</summary>
        public override string ToString() => InnerValue.ToString();

        /// <summary>
        /// Compares this instance to <paramref name="obj"/>.
        /// Accepts a <see cref="TTime"/> or a string parseable by <see cref="TimeSpan.Parse(string)"/>.
        /// </summary>
        /// <param name="obj">The object to compare against.</param>
        /// <returns>A negative number, zero, or a positive number.</returns>
        public int CompareTo(object obj)
        {
            if (obj is TTime other) return InnerValue.CompareTo(other.InnerValue);
            return InnerValue.CompareTo(TimeSpan.Parse(obj.ToString()));
        }

        /// <summary>Determines value equality using the shared ORM equality helper.</summary>
        public override bool Equals(object obj) => EqualsHelper<TTime, TimeSpan>(obj);

        /// <summary>Returns zero when null; otherwise delegates to <see cref="TimeSpan.GetHashCode()"/>.</summary>
        public override int  GetHashCode()      => IsNull() ? 0 : InnerValue.GetHashCode();
    }
}
