using System;
using System.Diagnostics;

namespace ActiveForge
{
    /// <summary>
    /// ORM field that wraps the CLR <see cref="decimal"/> type, mapping to a
    /// fixed-precision numeric database column (e.g. DECIMAL / NUMERIC in SQL).
    /// <para>
    /// Use <see cref="TField.SetValue(object)"/> or the typed overload
    /// <see cref="SetValue(decimal)"/> to assign a value, <see cref="TField.GetValue()"/>
    /// to retrieve it as <see cref="object"/>, or let the implicit conversions between
    /// <c>TDecimal</c> and <c>decimal</c> handle the cast transparently.
    /// </para>
    /// </summary>
    [Serializable]
    [DebuggerDisplay("Null={Null} Value={InnerValue}")]
    public class TDecimal : TField, IComparable, IFormattable
    {
        /// <summary>Backing store for the field value.</summary>
        protected decimal InnerValue;

        /// <summary>
        /// Gets or sets the field value, enforcing null/validity checks on read.
        /// </summary>
        protected decimal Value { get { CheckValidity(); return InnerValue; } set { InnerValue = value; } }

        /// <summary>Initialises a new <see cref="TDecimal"/> with a value of zero and null state unset.</summary>
        public TDecimal()             { InnerValue = 0; }

        /// <summary>Initialises a new <see cref="TDecimal"/> with the specified <paramref name="v"/> value.</summary>
        /// <param name="v">The initial decimal value.</param>
        public TDecimal(decimal v)    { SetValue(v); }

        /// <summary>
        /// Initialises a new <see cref="TDecimal"/> by converting <paramref name="v"/> using
        /// <see cref="TField.SetValue(object)"/>.
        /// </summary>
        /// <param name="v">Any value that can be converted to <see cref="decimal"/>.</param>
        public TDecimal(object v)     { SetValue(v); }

        /// <summary>Implicitly converts a <see cref="TDecimal"/> to its underlying <see cref="decimal"/> value.</summary>
        /// <param name="t">The field instance to convert.</param>
        public static implicit operator decimal(TDecimal t) => t.InnerValue;

        /// <summary>Implicitly wraps a <see cref="decimal"/> literal or variable in a new <see cref="TDecimal"/>.</summary>
        /// <param name="v">The decimal value to wrap.</param>
        public static implicit operator TDecimal(decimal v) => new TDecimal(v);

        /// <summary>Returns <see langword="true"/> when both operands are equal (null-aware).</summary>
        public static bool operator ==(TDecimal o1, TDecimal o2) => EqualityOperatorHelper<TDecimal>(o1, o2);

        /// <summary>Returns <see langword="true"/> when the operands are not equal.</summary>
        public static bool operator !=(TDecimal o1, TDecimal o2) => !(o1 == o2);

        /// <summary>Returns <see langword="true"/> when <paramref name="o1"/> is greater than <paramref name="o2"/>.</summary>
        public static bool operator >(TDecimal  o1, TDecimal o2) => GTHelper<TDecimal>(o1, o2);

        /// <summary>Returns <see langword="true"/> when <paramref name="o1"/> is less than <paramref name="o2"/>.</summary>
        public static bool operator <(TDecimal  o1, TDecimal o2) => LTHelper<TDecimal>(o1, o2);

        /// <summary>Returns <see langword="true"/> when <paramref name="o1"/> is greater than or equal to <paramref name="o2"/>.</summary>
        public static bool operator >=(TDecimal o1, TDecimal o2) => o1 > o2 || o1 == o2;

        /// <summary>Returns <see langword="true"/> when <paramref name="o1"/> is less than or equal to <paramref name="o2"/>.</summary>
        public static bool operator <=(TDecimal o1, TDecimal o2) => o1 < o2 || o1 == o2;

        /// <summary>Returns <see cref="decimal"/> as the underlying CLR type of this field.</summary>
        public override Type   GetUnderlyingType()  => typeof(decimal);

        /// <summary>Returns the string token <c>"decimal"</c> used by the ORM schema system.</summary>
        public override string GetTypeDescription()  => "decimal";

        /// <summary>Returns the current value as a boxed <see cref="decimal"/>.</summary>
        public override object GetValue()            => InnerValue;

        /// <summary>
        /// Sets the backing value from an arbitrary <see cref="object"/>.
        /// Accepts a <see cref="TDecimal"/> (copies its inner value) or anything
        /// convertible via <see cref="Convert.ToDecimal(object)"/>.
        /// </summary>
        /// <param name="value">The value to assign.</param>
        public override void SetDerivedValue(object value)
        {
            if (value is TDecimal td) InnerValue = td.InnerValue;
            else                      InnerValue = Convert.ToDecimal(value);
        }

        /// <summary>
        /// Sets the field to the given <paramref name="value"/> and clears any conversion-error flag.
        /// </summary>
        /// <param name="value">The decimal value to store.</param>
        public void SetValue(decimal value) { base.SetValue(value); ConversionError = false; }

        /// <summary>
        /// Marks the field as null or non-null.  When set to null the backing value is reset to zero.
        /// </summary>
        /// <param name="isNull"><see langword="true"/> to null the field; <see langword="false"/> to un-null it.</param>
        public override void SetNull(bool isNull) { base.SetNull(isNull); if (isNull) InnerValue = 0; }

        /// <summary>Returns the default string representation of the current decimal value.</summary>
        public override string ToString()                              => InnerValue.ToString();

        /// <summary>Formats the current value using <paramref name="fmt"/> and <paramref name="p"/>.</summary>
        /// <param name="fmt">A standard or custom numeric format string.</param>
        /// <param name="p">Culture-specific formatting information.</param>
        public string ToString(string fmt, IFormatProvider p)          => InnerValue.ToString(fmt, p);

        /// <summary>Formats the current value using the given <paramref name="fmt"/> format string.</summary>
        /// <param name="fmt">A standard or custom numeric format string.</param>
        public string ToString(string fmt)                             => InnerValue.ToString(fmt);

        /// <summary>
        /// Compares this instance to <paramref name="obj"/>.
        /// Accepts a <see cref="TDecimal"/> or any value convertible to <see cref="decimal"/>.
        /// </summary>
        /// <param name="obj">The object to compare against.</param>
        /// <returns>A negative number, zero, or a positive number.</returns>
        public int CompareTo(object obj)
        {
            if (obj is TDecimal other) return InnerValue.CompareTo(other.InnerValue);
            return InnerValue.CompareTo(Convert.ToDecimal(obj));
        }

        /// <summary>Determines value equality using the shared ORM equality helper.</summary>
        public override bool Equals(object obj) => EqualsHelper<TDecimal, decimal>(obj);

        /// <summary>Returns zero when null; otherwise delegates to <see cref="decimal.GetHashCode()"/>.</summary>
        public override int  GetHashCode()      => IsNull() ? 0 : InnerValue.GetHashCode();
    }
}
