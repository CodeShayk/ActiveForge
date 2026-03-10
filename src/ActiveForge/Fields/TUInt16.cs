using System;
using System.Diagnostics;

namespace ActiveForge
{
    /// <summary>
    /// ORM field that wraps the CLR <see cref="ushort"/> (16-bit unsigned integer) type,
    /// mapping to a SMALLINT UNSIGNED (or equivalent) database column.
    /// <para>
    /// Use <see cref="TField.SetValue(object)"/> or the typed overload
    /// <see cref="SetValue(ushort)"/> to assign a value, <see cref="TField.GetValue()"/>
    /// to retrieve it as <see cref="object"/>, or rely on the implicit conversions
    /// between <c>TUInt16</c> and <c>ushort</c> for transparent usage.
    /// </para>
    /// </summary>
    [Serializable]
    [DebuggerDisplay("Null={Null} Value={InnerValue}")]
    public class TUInt16 : TField, IComparable
    {
        /// <summary>Backing store for the field value.</summary>
        protected ushort InnerValue;

        /// <summary>
        /// Gets or sets the field value, enforcing null/validity checks on read.
        /// </summary>
        protected ushort Value { get { CheckValidity(); return InnerValue; } set { InnerValue = value; } }

        /// <summary>Initialises a new <see cref="TUInt16"/> with a value of zero and null state unset.</summary>
        public TUInt16()            { InnerValue = 0; }

        /// <summary>Initialises a new <see cref="TUInt16"/> with the specified <paramref name="v"/> value.</summary>
        /// <param name="v">The initial unsigned short value.</param>
        public TUInt16(ushort v)    { SetValue(v); }

        /// <summary>
        /// Initialises a new <see cref="TUInt16"/> by converting <paramref name="v"/> using
        /// <see cref="TField.SetValue(object)"/>.
        /// </summary>
        /// <param name="v">Any value that can be converted to <see cref="ushort"/>.</param>
        public TUInt16(object v)    { SetValue(v); }

        /// <summary>Implicitly converts a <see cref="TUInt16"/> to its underlying <see cref="ushort"/> value.</summary>
        /// <param name="t">The field instance to convert.</param>
        public static implicit operator ushort(TUInt16 t) => t.InnerValue;

        /// <summary>Implicitly wraps a <see cref="ushort"/> literal or variable in a new <see cref="TUInt16"/>.</summary>
        /// <param name="v">The unsigned short value to wrap.</param>
        public static implicit operator TUInt16(ushort v) => new TUInt16(v);

        /// <summary>Returns <see langword="true"/> when both operands are equal (null-aware).</summary>
        public static bool operator ==(TUInt16 o1, TUInt16 o2) => EqualityOperatorHelper<TUInt16>(o1, o2);

        /// <summary>Returns <see langword="true"/> when the operands are not equal.</summary>
        public static bool operator !=(TUInt16 o1, TUInt16 o2) => !(o1 == o2);

        /// <summary>Returns <see cref="ushort"/> as the underlying CLR type of this field.</summary>
        public override Type   GetUnderlyingType()  => typeof(ushort);

        /// <summary>Returns the string token <c>"uint16"</c> used by the ORM schema system.</summary>
        public override string GetTypeDescription()  => "uint16";

        /// <summary>Returns the current value as a boxed <see cref="ushort"/>.</summary>
        public override object GetValue()            => InnerValue;

        /// <summary>
        /// Sets the backing value from an arbitrary <see cref="object"/>.
        /// Accepts a <see cref="TUInt16"/> (copies its inner value) or anything
        /// convertible via <see cref="Convert.ToUInt16(object)"/>.
        /// </summary>
        /// <param name="value">The value to assign.</param>
        public override void SetDerivedValue(object value)
        {
            if (value is TUInt16 tu) InnerValue = tu.InnerValue;
            else                     InnerValue = Convert.ToUInt16(value);
        }

        /// <summary>
        /// Sets the field to the given <paramref name="value"/> and clears any conversion-error flag.
        /// </summary>
        /// <param name="value">The unsigned short value to store.</param>
        public void SetValue(ushort value) { base.SetValue(value); ConversionError = false; }

        /// <summary>
        /// Marks the field as null or non-null.  When set to null the backing value is reset to zero.
        /// </summary>
        /// <param name="isNull"><see langword="true"/> to null the field; <see langword="false"/> to un-null it.</param>
        public override void SetNull(bool isNull) { base.SetNull(isNull); if (isNull) InnerValue = 0; }

        /// <summary>Returns the default string representation of the current unsigned short value.</summary>
        public override string ToString() => InnerValue.ToString();

        /// <summary>
        /// Compares this instance to <paramref name="obj"/>.
        /// Accepts a <see cref="TUInt16"/> or any value convertible to <see cref="ushort"/>.
        /// </summary>
        /// <param name="obj">The object to compare against.</param>
        /// <returns>A negative number, zero, or a positive number.</returns>
        public int CompareTo(object obj)
        {
            if (obj is TUInt16 other) return InnerValue.CompareTo(other.InnerValue);
            return InnerValue.CompareTo(Convert.ToUInt16(obj));
        }

        /// <summary>Determines value equality using the shared ORM equality helper.</summary>
        public override bool Equals(object obj) => EqualsHelper<TUInt16, ushort>(obj);

        /// <summary>Returns zero when null; otherwise delegates to <see cref="ushort.GetHashCode()"/>.</summary>
        public override int  GetHashCode()      => IsNull() ? 0 : InnerValue.GetHashCode();
    }
}
