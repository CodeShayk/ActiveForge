using System;
using System.Diagnostics;

namespace ActiveForge
{
    /// <summary>
    /// ORM field that wraps the CLR <see cref="uint"/> (32-bit unsigned integer) type,
    /// mapping to an unsigned integer database column (e.g. INT UNSIGNED in MySQL / BIGINT in SQL Server).
    /// <para>
    /// Use <see cref="TField.SetValue(object)"/> or the typed overload
    /// <see cref="SetValue(uint)"/> to assign a value, <see cref="TField.GetValue()"/>
    /// to retrieve it as <see cref="object"/>, or rely on the implicit conversions
    /// between <c>TUInt</c> and <c>uint</c> for transparent usage.
    /// </para>
    /// </summary>
    [Serializable]
    [DebuggerDisplay("Null={Null} Value={InnerValue}")]
    public class TUInt : TField, IComparable
    {
        /// <summary>Backing store for the field value.</summary>
        protected uint InnerValue;

        /// <summary>
        /// Gets or sets the field value, enforcing null/validity checks on read.
        /// </summary>
        protected uint Value { get { CheckValidity(); return InnerValue; } set { InnerValue = value; } }

        /// <summary>Initialises a new <see cref="TUInt"/> with a value of zero and null state unset.</summary>
        public TUInt()           { InnerValue = 0; }

        /// <summary>Initialises a new <see cref="TUInt"/> with the specified <paramref name="v"/> value.</summary>
        /// <param name="v">The initial unsigned integer value.</param>
        public TUInt(uint v)     { SetValue(v); }

        /// <summary>
        /// Initialises a new <see cref="TUInt"/> by converting <paramref name="v"/> using
        /// <see cref="TField.SetValue(object)"/>.
        /// </summary>
        /// <param name="v">Any value that can be converted to <see cref="uint"/>.</param>
        public TUInt(object v)   { SetValue(v); }

        /// <summary>Implicitly converts a <see cref="TUInt"/> to its underlying <see cref="uint"/> value.</summary>
        /// <param name="t">The field instance to convert.</param>
        public static implicit operator uint(TUInt t) => t.InnerValue;

        /// <summary>Implicitly wraps a <see cref="uint"/> literal or variable in a new <see cref="TUInt"/>.</summary>
        /// <param name="v">The unsigned integer value to wrap.</param>
        public static implicit operator TUInt(uint v) => new TUInt(v);

        /// <summary>Returns <see langword="true"/> when both operands are equal (null-aware).</summary>
        public static bool operator ==(TUInt o1, TUInt o2) => EqualityOperatorHelper<TUInt>(o1, o2);

        /// <summary>Returns <see langword="true"/> when the operands are not equal.</summary>
        public static bool operator !=(TUInt o1, TUInt o2) => !(o1 == o2);

        /// <summary>Returns <see cref="uint"/> as the underlying CLR type of this field.</summary>
        public override Type   GetUnderlyingType()  => typeof(uint);

        /// <summary>Returns the string token <c>"uint"</c> used by the ORM schema system.</summary>
        public override string GetTypeDescription()  => "uint";

        /// <summary>Returns the current value as a boxed <see cref="uint"/>.</summary>
        public override object GetValue()            => InnerValue;

        /// <summary>
        /// Sets the backing value from an arbitrary <see cref="object"/>.
        /// Accepts a <see cref="TUInt"/> (copies its inner value) or anything
        /// convertible via <see cref="Convert.ToUInt32(object)"/>.
        /// </summary>
        /// <param name="value">The value to assign.</param>
        public override void SetDerivedValue(object value)
        {
            if (value is TUInt tu) InnerValue = tu.InnerValue;
            else                   InnerValue = Convert.ToUInt32(value);
        }

        /// <summary>
        /// Sets the field to the given <paramref name="value"/> and clears any conversion-error flag.
        /// </summary>
        /// <param name="value">The unsigned integer value to store.</param>
        public void SetValue(uint value) { base.SetValue(value); ConversionError = false; }

        /// <summary>
        /// Marks the field as null or non-null.  When set to null the backing value is reset to zero.
        /// </summary>
        /// <param name="isNull"><see langword="true"/> to null the field; <see langword="false"/> to un-null it.</param>
        public override void SetNull(bool isNull) { base.SetNull(isNull); if (isNull) InnerValue = 0; }

        /// <summary>Returns the default string representation of the current unsigned integer value.</summary>
        public override string ToString() => InnerValue.ToString();

        /// <summary>
        /// Compares this instance to <paramref name="obj"/>.
        /// Accepts a <see cref="TUInt"/> or any value convertible to <see cref="uint"/>.
        /// </summary>
        /// <param name="obj">The object to compare against.</param>
        /// <returns>A negative number, zero, or a positive number.</returns>
        public int CompareTo(object obj)
        {
            if (obj is TUInt other) return InnerValue.CompareTo(other.InnerValue);
            return InnerValue.CompareTo(Convert.ToUInt32(obj));
        }

        /// <summary>Determines value equality using the shared ORM equality helper.</summary>
        public override bool Equals(object obj) => EqualsHelper<TUInt, uint>(obj);

        /// <summary>Returns zero when null; otherwise delegates to <see cref="uint.GetHashCode()"/>.</summary>
        public override int  GetHashCode()      => IsNull() ? 0 : InnerValue.GetHashCode();
    }
}
