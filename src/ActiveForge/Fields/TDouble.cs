using System;
using System.Diagnostics;

namespace ActiveForge
{
    /// <summary>
    /// ORM field that wraps the CLR <see cref="double"/> type, mapping to a
    /// double-precision floating-point database column (e.g. FLOAT / DOUBLE in SQL).
    /// <para>
    /// Use <see cref="TField.SetValue(object)"/> or the typed overload
    /// <see cref="SetValue(double)"/> to assign a value, <see cref="TField.GetValue()"/>
    /// to retrieve it as <see cref="object"/>, or rely on the implicit conversions
    /// between <c>TDouble</c> and <c>double</c> for transparent usage.
    /// </para>
    /// </summary>
    [Serializable]
    [DebuggerDisplay("Null={Null} Value={InnerValue}")]
    public class TDouble : TField, IComparable, IFormattable
    {
        /// <summary>Backing store for the field value.</summary>
        protected double InnerValue;

        /// <summary>
        /// Gets or sets the field value, enforcing null/validity checks on read.
        /// </summary>
        protected double Value { get { CheckValidity(); return InnerValue; } set { InnerValue = value; } }

        /// <summary>Initialises a new <see cref="TDouble"/> with a value of zero and null state unset.</summary>
        public TDouble()           { InnerValue = 0; }

        /// <summary>Initialises a new <see cref="TDouble"/> with the specified <paramref name="v"/> value.</summary>
        /// <param name="v">The initial double value.</param>
        public TDouble(double v)   { SetValue(v); }

        /// <summary>
        /// Initialises a new <see cref="TDouble"/> by converting <paramref name="v"/> using
        /// <see cref="TField.SetValue(object)"/>.
        /// </summary>
        /// <param name="v">Any value that can be converted to <see cref="double"/>.</param>
        public TDouble(object v)   { SetValue(v); }

        /// <summary>Implicitly converts a <see cref="TDouble"/> to its underlying <see cref="double"/> value.</summary>
        /// <param name="t">The field instance to convert.</param>
        public static implicit operator double(TDouble t) => t.InnerValue;

        /// <summary>Implicitly wraps a <see cref="double"/> literal or variable in a new <see cref="TDouble"/>.</summary>
        /// <param name="v">The double value to wrap.</param>
        public static implicit operator TDouble(double v) => new TDouble(v);

        /// <summary>Returns <see langword="true"/> when both operands are equal (null-aware).</summary>
        public static bool operator ==(TDouble o1, TDouble o2) => EqualityOperatorHelper<TDouble>(o1, o2);

        /// <summary>Returns <see langword="true"/> when the operands are not equal.</summary>
        public static bool operator !=(TDouble o1, TDouble o2) => !(o1 == o2);

        /// <summary>Returns <see langword="true"/> when <paramref name="o1"/> is greater than <paramref name="o2"/>.</summary>
        public static bool operator >(TDouble  o1, TDouble o2) => GTHelper<TDouble>(o1, o2);

        /// <summary>Returns <see langword="true"/> when <paramref name="o1"/> is less than <paramref name="o2"/>.</summary>
        public static bool operator <(TDouble  o1, TDouble o2) => LTHelper<TDouble>(o1, o2);

        /// <summary>Returns <see langword="true"/> when <paramref name="o1"/> is greater than or equal to <paramref name="o2"/>.</summary>
        public static bool operator >=(TDouble o1, TDouble o2) => o1 > o2 || o1 == o2;

        /// <summary>Returns <see langword="true"/> when <paramref name="o1"/> is less than or equal to <paramref name="o2"/>.</summary>
        public static bool operator <=(TDouble o1, TDouble o2) => o1 < o2 || o1 == o2;

        /// <summary>Returns <see cref="double"/> as the underlying CLR type of this field.</summary>
        public override Type   GetUnderlyingType()  => typeof(double);

        /// <summary>Returns the string token <c>"double"</c> used by the ORM schema system.</summary>
        public override string GetTypeDescription()  => "double";

        /// <summary>Returns the current value as a boxed <see cref="double"/>.</summary>
        public override object GetValue()            => InnerValue;

        /// <summary>
        /// Sets the backing value from an arbitrary <see cref="object"/>.
        /// Accepts a <see cref="TDouble"/> (copies its inner value) or anything
        /// convertible via <see cref="Convert.ToDouble(object)"/>.
        /// </summary>
        /// <param name="value">The value to assign.</param>
        public override void SetDerivedValue(object value)
        {
            if (value is TDouble td) InnerValue = td.InnerValue;
            else                     InnerValue = Convert.ToDouble(value);
        }

        /// <summary>
        /// Sets the field to the given <paramref name="value"/> and clears any conversion-error flag.
        /// </summary>
        /// <param name="value">The double value to store.</param>
        public void SetValue(double value) { base.SetValue(value); ConversionError = false; }

        /// <summary>
        /// Marks the field as null or non-null.  When set to null the backing value is reset to zero.
        /// </summary>
        /// <param name="isNull"><see langword="true"/> to null the field; <see langword="false"/> to un-null it.</param>
        public override void SetNull(bool isNull) { base.SetNull(isNull); if (isNull) InnerValue = 0; }

        /// <summary>Returns the default string representation of the current double value.</summary>
        public override string ToString()             => InnerValue.ToString();

        /// <summary>Formats the current value using <paramref name="fmt"/> and <paramref name="p"/>.</summary>
        /// <param name="fmt">A standard or custom numeric format string.</param>
        /// <param name="p">Culture-specific formatting information.</param>
        public string ToString(string fmt, IFormatProvider p) => InnerValue.ToString(fmt, p);

        /// <summary>
        /// Compares this instance to <paramref name="obj"/>.
        /// Accepts a <see cref="TDouble"/> or any value convertible to <see cref="double"/>.
        /// </summary>
        /// <param name="obj">The object to compare against.</param>
        /// <returns>A negative number, zero, or a positive number.</returns>
        public int CompareTo(object obj)
        {
            if (obj is TDouble other) return InnerValue.CompareTo(other.InnerValue);
            return InnerValue.CompareTo(Convert.ToDouble(obj));
        }

        /// <summary>Determines value equality using the shared ORM equality helper.</summary>
        public override bool Equals(object obj) => EqualsHelper<TDouble, double>(obj);

        /// <summary>Returns zero when null; otherwise delegates to <see cref="double.GetHashCode()"/>.</summary>
        public override int  GetHashCode()      => IsNull() ? 0 : InnerValue.GetHashCode();
    }
}
