using System;
using System.Diagnostics;

namespace ActiveForge
{
    /// <summary>
    /// ORM field that wraps the CLR <see cref="long"/> (64-bit signed integer) type,
    /// mapping to a BIGINT database column.
    /// <para>
    /// Use <see cref="TField.SetValue(object)"/> or the typed overload
    /// <see cref="SetValue(long)"/> to assign a value, <see cref="TField.GetValue()"/>
    /// to retrieve it as <see cref="object"/>, or rely on the implicit conversions
    /// between <c>TInt64</c> and <c>long</c> (or <c>int</c>) for transparent usage.
    /// </para>
    /// <para>
    /// <see cref="TLong"/> is a semantic alias for this class and can be used interchangeably.
    /// </para>
    /// </summary>
    [Serializable]
    [DebuggerDisplay("Null={Null} Value={InnerValue}")]
    public class TInt64 : TField, IComparable
    {
        /// <summary>Backing store for the field value.</summary>
        protected long InnerValue;

        /// <summary>
        /// Gets or sets the field value, enforcing null/validity checks on read.
        /// </summary>
        protected long Value { get { CheckValidity(); return InnerValue; } set { InnerValue = value; } }

        /// <summary>Initialises a new <see cref="TInt64"/> with a value of zero and null state unset.</summary>
        public TInt64()          { InnerValue = 0; }

        /// <summary>Initialises a new <see cref="TInt64"/> with the specified <paramref name="v"/> value.</summary>
        /// <param name="v">The initial long value.</param>
        public TInt64(long v)    { SetValue(v); }

        /// <summary>
        /// Initialises a new <see cref="TInt64"/> by converting <paramref name="v"/> using
        /// <see cref="TField.SetValue(object)"/>.
        /// </summary>
        /// <param name="v">Any value that can be converted to <see cref="long"/>.</param>
        public TInt64(object v)  { SetValue(v); }

        /// <summary>Implicitly converts a <see cref="TInt64"/> to its underlying <see cref="long"/> value.</summary>
        /// <param name="t">The field instance to convert.</param>
        public static implicit operator long(TInt64 t)  => t.InnerValue;

        /// <summary>Implicitly wraps a <see cref="long"/> literal or variable in a new <see cref="TInt64"/>.</summary>
        /// <param name="v">The long value to wrap.</param>
        public static implicit operator TInt64(long v)  => new TInt64(v);

        /// <summary>Implicitly wraps an <see cref="int"/> literal or variable in a new <see cref="TInt64"/>.</summary>
        /// <param name="v">The int value to wrap (widened to long).</param>
        public static implicit operator TInt64(int v)   => new TInt64(v);

        /// <summary>Returns <see langword="true"/> when both operands are equal (null-aware).</summary>
        public static bool operator ==(TInt64 o1, TInt64 o2) => EqualityOperatorHelper<TInt64>(o1, o2);

        /// <summary>Returns <see langword="true"/> when the operands are not equal.</summary>
        public static bool operator !=(TInt64 o1, TInt64 o2) => !(o1 == o2);

        /// <summary>Returns <see langword="true"/> when <paramref name="o1"/> is greater than <paramref name="o2"/>.</summary>
        public static bool operator >(TInt64  o1, TInt64 o2) => GTHelper<TInt64>(o1, o2);

        /// <summary>Returns <see langword="true"/> when <paramref name="o1"/> is less than <paramref name="o2"/>.</summary>
        public static bool operator <(TInt64  o1, TInt64 o2) => LTHelper<TInt64>(o1, o2);

        /// <summary>Returns <see langword="true"/> when <paramref name="o1"/> is greater than or equal to <paramref name="o2"/>.</summary>
        public static bool operator >=(TInt64 o1, TInt64 o2) => o1 > o2 || o1 == o2;

        /// <summary>Returns <see langword="true"/> when <paramref name="o1"/> is less than or equal to <paramref name="o2"/>.</summary>
        public static bool operator <=(TInt64 o1, TInt64 o2) => o1 < o2 || o1 == o2;

        /// <summary>Returns <see cref="long"/> as the underlying CLR type of this field.</summary>
        public override Type   GetUnderlyingType()  => typeof(long);

        /// <summary>Returns the string token <c>"int64"</c> used by the ORM schema system.</summary>
        public override string GetTypeDescription()  => "int64";

        /// <summary>Returns the current value as a boxed <see cref="long"/>.</summary>
        public override object GetValue()            => InnerValue;

        /// <summary>
        /// Sets the backing value from an arbitrary <see cref="object"/>.
        /// Accepts a <see cref="TInt64"/> (copies its inner value) or anything
        /// convertible via <see cref="Convert.ToInt64(object)"/>.
        /// </summary>
        /// <param name="value">The value to assign.</param>
        public override void SetDerivedValue(object value)
        {
            if (value is TInt64 ti) InnerValue = ti.InnerValue;
            else                    InnerValue = Convert.ToInt64(value);
        }

        /// <summary>
        /// Sets the field to the given <paramref name="value"/> and clears any conversion-error flag.
        /// </summary>
        /// <param name="value">The long value to store.</param>
        public void SetValue(long value) { base.SetValue(value); ConversionError = false; }

        /// <summary>
        /// Marks the field as null or non-null.  When set to null the backing value is reset to zero.
        /// </summary>
        /// <param name="isNull"><see langword="true"/> to null the field; <see langword="false"/> to un-null it.</param>
        public override void SetNull(bool isNull) { base.SetNull(isNull); if (isNull) InnerValue = 0; }

        /// <summary>Returns the default string representation of the current long value.</summary>
        public override string ToString() => InnerValue.ToString();

        /// <summary>
        /// Compares this instance to <paramref name="obj"/>.
        /// Accepts a <see cref="TInt64"/> or any value convertible to <see cref="long"/>.
        /// </summary>
        /// <param name="obj">The object to compare against.</param>
        /// <returns>A negative number, zero, or a positive number.</returns>
        public int CompareTo(object obj)
        {
            if (obj is TInt64 other) return InnerValue.CompareTo(other.InnerValue);
            return InnerValue.CompareTo(Convert.ToInt64(obj));
        }

        /// <summary>Determines value equality using the shared ORM equality helper.</summary>
        public override bool Equals(object obj) => EqualsHelper<TInt64, long>(obj);

        /// <summary>Returns zero when null; otherwise delegates to <see cref="long.GetHashCode()"/>.</summary>
        public override int  GetHashCode()      => IsNull() ? 0 : InnerValue.GetHashCode();
    }
}
