using System;
using System.Diagnostics;

namespace ActiveForge
{
    /// <summary>
    /// ORM field that wraps the CLR <see cref="ulong"/> (64-bit unsigned integer) type,
    /// mapping to a BIGINT UNSIGNED (or equivalent) database column.
    /// <para>
    /// Use <see cref="TField.SetValue(object)"/> or the typed overload
    /// <see cref="SetValue(ulong)"/> to assign a value, <see cref="TField.GetValue()"/>
    /// to retrieve it as <see cref="object"/>, or rely on the implicit conversions
    /// between <c>TUInt64</c> and <c>ulong</c> for transparent usage.
    /// </para>
    /// </summary>
    [Serializable]
    [DebuggerDisplay("Null={Null} Value={InnerValue}")]
    public class TUInt64 : TField, IComparable
    {
        /// <summary>Backing store for the field value.</summary>
        protected ulong InnerValue;

        /// <summary>
        /// Gets or sets the field value, enforcing null/validity checks on read.
        /// </summary>
        protected ulong Value { get { CheckValidity(); return InnerValue; } set { InnerValue = value; } }

        /// <summary>Initialises a new <see cref="TUInt64"/> with a value of zero and null state unset.</summary>
        public TUInt64()           { InnerValue = 0; }

        /// <summary>Initialises a new <see cref="TUInt64"/> with the specified <paramref name="v"/> value.</summary>
        /// <param name="v">The initial unsigned long value.</param>
        public TUInt64(ulong v)    { SetValue(v); }

        /// <summary>
        /// Initialises a new <see cref="TUInt64"/> by converting <paramref name="v"/> using
        /// <see cref="TField.SetValue(object)"/>.
        /// </summary>
        /// <param name="v">Any value that can be converted to <see cref="ulong"/>.</param>
        public TUInt64(object v)   { SetValue(v); }

        /// <summary>Implicitly converts a <see cref="TUInt64"/> to its underlying <see cref="ulong"/> value.</summary>
        /// <param name="t">The field instance to convert.</param>
        public static implicit operator ulong(TUInt64 t) => t.InnerValue;

        /// <summary>Implicitly wraps a <see cref="ulong"/> literal or variable in a new <see cref="TUInt64"/>.</summary>
        /// <param name="v">The unsigned long value to wrap.</param>
        public static implicit operator TUInt64(ulong v) => new TUInt64(v);

        /// <summary>Returns <see langword="true"/> when both operands are equal (null-aware).</summary>
        public static bool operator ==(TUInt64 o1, TUInt64 o2) => EqualityOperatorHelper<TUInt64>(o1, o2);

        /// <summary>Returns <see langword="true"/> when the operands are not equal.</summary>
        public static bool operator !=(TUInt64 o1, TUInt64 o2) => !(o1 == o2);

        /// <summary>Returns <see cref="ulong"/> as the underlying CLR type of this field.</summary>
        public override Type   GetUnderlyingType()  => typeof(ulong);

        /// <summary>Returns the string token <c>"uint64"</c> used by the ORM schema system.</summary>
        public override string GetTypeDescription()  => "uint64";

        /// <summary>Returns the current value as a boxed <see cref="ulong"/>.</summary>
        public override object GetValue()            => InnerValue;

        /// <summary>
        /// Sets the backing value from an arbitrary <see cref="object"/>.
        /// Accepts a <see cref="TUInt64"/> (copies its inner value) or anything
        /// convertible via <see cref="Convert.ToUInt64(object)"/>.
        /// </summary>
        /// <param name="value">The value to assign.</param>
        public override void SetDerivedValue(object value)
        {
            if (value is TUInt64 tu) InnerValue = tu.InnerValue;
            else                     InnerValue = Convert.ToUInt64(value);
        }

        /// <summary>
        /// Sets the field to the given <paramref name="value"/> and clears any conversion-error flag.
        /// </summary>
        /// <param name="value">The unsigned long value to store.</param>
        public void SetValue(ulong value) { base.SetValue(value); ConversionError = false; }

        /// <summary>
        /// Marks the field as null or non-null.  When set to null the backing value is reset to zero.
        /// </summary>
        /// <param name="isNull"><see langword="true"/> to null the field; <see langword="false"/> to un-null it.</param>
        public override void SetNull(bool isNull) { base.SetNull(isNull); if (isNull) InnerValue = 0; }

        /// <summary>Returns the default string representation of the current unsigned long value.</summary>
        public override string ToString() => InnerValue.ToString();

        /// <summary>
        /// Compares this instance to <paramref name="obj"/>.
        /// Accepts a <see cref="TUInt64"/> or any value convertible to <see cref="ulong"/>.
        /// </summary>
        /// <param name="obj">The object to compare against.</param>
        /// <returns>A negative number, zero, or a positive number.</returns>
        public int CompareTo(object obj)
        {
            if (obj is TUInt64 other) return InnerValue.CompareTo(other.InnerValue);
            return InnerValue.CompareTo(Convert.ToUInt64(obj));
        }

        /// <summary>Determines value equality using the shared ORM equality helper.</summary>
        public override bool Equals(object obj) => EqualsHelper<TUInt64, ulong>(obj);

        /// <summary>Returns zero when null; otherwise delegates to <see cref="ulong.GetHashCode()"/>.</summary>
        public override int  GetHashCode()      => IsNull() ? 0 : InnerValue.GetHashCode();
    }
}
