using System;
using System.Diagnostics;

namespace ActiveForge
{
    /// <summary>
    /// ORM field that wraps the CLR <see cref="sbyte"/> (8-bit signed integer) type,
    /// mapping to a TINYINT (signed) database column.
    /// <para>
    /// Use <see cref="TField.SetValue(object)"/> or the typed overload
    /// <see cref="SetValue(sbyte)"/> to assign a value, <see cref="TField.GetValue()"/>
    /// to retrieve it as <see cref="object"/>, or rely on the implicit conversions
    /// between <c>TSByte</c> and <c>sbyte</c> for transparent usage.
    /// </para>
    /// </summary>
    [Serializable]
    [DebuggerDisplay("Null={Null} Value={InnerValue}")]
    public class TSByte : TField, IComparable
    {
        /// <summary>Backing store for the field value.</summary>
        protected sbyte InnerValue;

        /// <summary>
        /// Gets or sets the field value, enforcing null/validity checks on read.
        /// </summary>
        protected sbyte Value { get { CheckValidity(); return InnerValue; } set { InnerValue = value; } }

        /// <summary>Initialises a new <see cref="TSByte"/> with a value of zero and null state unset.</summary>
        public TSByte()           { InnerValue = 0; }

        /// <summary>Initialises a new <see cref="TSByte"/> with the specified <paramref name="v"/> value.</summary>
        /// <param name="v">The initial sbyte value.</param>
        public TSByte(sbyte v)    { SetValue(v); }

        /// <summary>
        /// Initialises a new <see cref="TSByte"/> by converting <paramref name="v"/> using
        /// <see cref="TField.SetValue(object)"/>.
        /// </summary>
        /// <param name="v">Any value that can be converted to <see cref="sbyte"/>.</param>
        public TSByte(object v)   { SetValue(v); }

        /// <summary>Implicitly converts a <see cref="TSByte"/> to its underlying <see cref="sbyte"/> value.</summary>
        /// <param name="t">The field instance to convert.</param>
        public static implicit operator sbyte(TSByte t) => t.InnerValue;

        /// <summary>Implicitly wraps a <see cref="sbyte"/> literal or variable in a new <see cref="TSByte"/>.</summary>
        /// <param name="v">The sbyte value to wrap.</param>
        public static implicit operator TSByte(sbyte v) => new TSByte(v);

        /// <summary>Returns <see langword="true"/> when both operands are equal (null-aware).</summary>
        public static bool operator ==(TSByte o1, TSByte o2) => EqualityOperatorHelper<TSByte>(o1, o2);

        /// <summary>Returns <see langword="true"/> when the operands are not equal.</summary>
        public static bool operator !=(TSByte o1, TSByte o2) => !(o1 == o2);

        /// <summary>Returns <see cref="sbyte"/> as the underlying CLR type of this field.</summary>
        public override Type   GetUnderlyingType()  => typeof(sbyte);

        /// <summary>Returns the string token <c>"sbyte"</c> used by the ORM schema system.</summary>
        public override string GetTypeDescription()  => "sbyte";

        /// <summary>Returns the current value as a boxed <see cref="sbyte"/>.</summary>
        public override object GetValue()            => InnerValue;

        /// <summary>
        /// Sets the backing value from an arbitrary <see cref="object"/>.
        /// Accepts a <see cref="TSByte"/> (copies its inner value) or anything
        /// convertible via <see cref="Convert.ToSByte(object)"/>.
        /// </summary>
        /// <param name="value">The value to assign.</param>
        public override void SetDerivedValue(object value)
        {
            if (value is TSByte ts) InnerValue = ts.InnerValue;
            else                    InnerValue = Convert.ToSByte(value);
        }

        /// <summary>
        /// Sets the field to the given <paramref name="value"/> and clears any conversion-error flag.
        /// </summary>
        /// <param name="value">The sbyte value to store.</param>
        public void SetValue(sbyte value) { base.SetValue(value); ConversionError = false; }

        /// <summary>
        /// Marks the field as null or non-null.  When set to null the backing value is reset to zero.
        /// </summary>
        /// <param name="isNull"><see langword="true"/> to null the field; <see langword="false"/> to un-null it.</param>
        public override void SetNull(bool isNull) { base.SetNull(isNull); if (isNull) InnerValue = 0; }

        /// <summary>Returns the default string representation of the current sbyte value.</summary>
        public override string ToString() => InnerValue.ToString();

        /// <summary>
        /// Compares this instance to <paramref name="obj"/>.
        /// Accepts a <see cref="TSByte"/> or any value convertible to <see cref="sbyte"/>.
        /// </summary>
        /// <param name="obj">The object to compare against.</param>
        /// <returns>A negative number, zero, or a positive number.</returns>
        public int CompareTo(object obj)
        {
            if (obj is TSByte other) return InnerValue.CompareTo(other.InnerValue);
            return InnerValue.CompareTo(Convert.ToSByte(obj));
        }

        /// <summary>Determines value equality using the shared ORM equality helper.</summary>
        public override bool Equals(object obj) => EqualsHelper<TSByte, sbyte>(obj);

        /// <summary>Returns zero when null; otherwise delegates to <see cref="sbyte.GetHashCode()"/>.</summary>
        public override int  GetHashCode()      => IsNull() ? 0 : InnerValue.GetHashCode();
    }
}
