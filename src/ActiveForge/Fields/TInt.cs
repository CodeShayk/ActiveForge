using System;
using System.Diagnostics;
using System.Globalization;

namespace ActiveForge
{
    /// <summary>
    /// ORM field that wraps the CLR <see cref="int"/> (32-bit signed integer) type,
    /// mapping to an INTEGER / INT database column.
    /// <para>
    /// Use <see cref="TField.SetValue(object)"/> or the typed overload <see cref="SetValue(int)"/>
    /// to assign a value, <see cref="TField.GetValue()"/> to retrieve it as <see cref="object"/>,
    /// or rely on the implicit conversions between <c>TInt</c> and <c>int</c> for transparent usage.
    /// </para>
    /// <para>
    /// Mixed <c>TInt</c>/<c>int</c> comparison operators are provided so that predicates such as
    /// <c>myField &gt; 0</c> compile without an explicit cast.  The <c>++</c> and <c>--</c>
    /// operators return new <see cref="TInt"/> instances (the field is immutable in operator context).
    /// </para>
    /// <para>
    /// Static <see cref="Parse(string)"/> overloads mirror <see cref="int.Parse(string)"/>,
    /// returning a <see cref="TInt"/> directly.
    /// </para>
    /// </summary>
    [Serializable]
    [DebuggerDisplay("Null={Null} Value={InnerValue}")]
    public class TInt : TField, IComparable, IFormattable
    {
        /// <summary>Backing store for the field value.</summary>
        protected int InnerValue;

        /// <summary>
        /// Gets or sets the field value, enforcing null/validity checks on read.
        /// </summary>
        protected int Value
        {
            get { CheckValidity(); return InnerValue; }
            set { InnerValue = value; }
        }

        /// <summary>Initialises a new <see cref="TInt"/> with a value of zero and null state unset.</summary>
        public TInt()           { InnerValue = 0; }

        /// <summary>Initialises a new <see cref="TInt"/> with the specified integer <paramref name="value"/>.</summary>
        /// <param name="value">The initial integer value.</param>
        public TInt(int value)  { SetValue(value); }

        /// <summary>
        /// Initialises a new <see cref="TInt"/> by converting <paramref name="v"/> using
        /// <see cref="TField.SetValue(object)"/>.
        /// </summary>
        /// <param name="v">Any value that can be converted to <see cref="int"/>.</param>
        public TInt(object v)   { SetValue(v); }

        /// <summary>Implicitly converts a <see cref="TInt"/> to its underlying <see cref="int"/> value.</summary>
        /// <param name="t">The field instance to convert.</param>
        public static implicit operator int(TInt t)   => t.InnerValue;

        /// <summary>Implicitly wraps an <see cref="int"/> literal or variable in a new <see cref="TInt"/>.</summary>
        /// <param name="v">The integer value to wrap.</param>
        public static implicit operator TInt(int v)   => new TInt(v);

        /// <summary>Returns <see langword="true"/> when both <see cref="TInt"/> operands are equal (null-aware).</summary>
        public static bool operator ==(TInt o1, TInt o2)  => EqualityOperatorHelper<TInt>(o1, o2);

        /// <summary>Returns <see langword="true"/> when the <see cref="TInt"/> operands are not equal.</summary>
        public static bool operator !=(TInt o1, TInt o2)  => !(o1 == o2);

        /// <summary>Returns <see langword="true"/> when <paramref name="o1"/> equals the integer <paramref name="o2"/>.</summary>
        public static bool operator ==(TInt o1, int  o2)  => o1 == (TInt)o2;

        /// <summary>Returns <see langword="true"/> when <paramref name="o1"/> does not equal the integer <paramref name="o2"/>.</summary>
        public static bool operator !=(TInt o1, int  o2)  => o1 != (TInt)o2;

        /// <summary>Returns <see langword="true"/> when integer <paramref name="o1"/> equals <paramref name="o2"/>.</summary>
        public static bool operator ==(int  o1, TInt o2)  => (TInt)o1 == o2;

        /// <summary>Returns <see langword="true"/> when integer <paramref name="o1"/> does not equal <paramref name="o2"/>.</summary>
        public static bool operator !=(int  o1, TInt o2)  => (TInt)o1 != o2;

        /// <summary>Returns <see langword="true"/> when <paramref name="o1"/> is greater than <paramref name="o2"/>.</summary>
        public static bool operator >(TInt  o1, TInt o2)  => GTHelper<TInt>(o1, o2);

        /// <summary>Returns <see langword="true"/> when <paramref name="o1"/> is less than <paramref name="o2"/>.</summary>
        public static bool operator <(TInt  o1, TInt o2)  => LTHelper<TInt>(o1, o2);

        /// <summary>Returns <see langword="true"/> when <paramref name="o1"/> is greater than or equal to <paramref name="o2"/>.</summary>
        public static bool operator >=(TInt o1, TInt o2)  => o1 > o2 || o1 == o2;

        /// <summary>Returns <see langword="true"/> when <paramref name="o1"/> is less than or equal to <paramref name="o2"/>.</summary>
        public static bool operator <=(TInt o1, TInt o2)  => o1 < o2 || o1 == o2;

        /// <summary>Returns <see langword="true"/> when <paramref name="o1"/> is greater than the integer <paramref name="o2"/>.</summary>
        public static bool operator >(TInt  o1, int  o2)  => o1 > (TInt)o2;

        /// <summary>Returns <see langword="true"/> when <paramref name="o1"/> is less than the integer <paramref name="o2"/>.</summary>
        public static bool operator <(TInt  o1, int  o2)  => o1 < (TInt)o2;

        /// <summary>Returns <see langword="true"/> when <paramref name="o1"/> is greater than or equal to the integer <paramref name="o2"/>.</summary>
        public static bool operator >=(TInt o1, int  o2)  => o1 >= (TInt)o2;

        /// <summary>Returns <see langword="true"/> when <paramref name="o1"/> is less than or equal to the integer <paramref name="o2"/>.</summary>
        public static bool operator <=(TInt o1, int  o2)  => o1 <= (TInt)o2;

        /// <summary>Returns a new <see cref="TInt"/> with its value incremented by one.</summary>
        /// <param name="i">The field to increment.</param>
        public static TInt operator ++(TInt i) => new TInt(i.InnerValue + 1);

        /// <summary>Returns a new <see cref="TInt"/> with its value decremented by one.</summary>
        /// <param name="i">The field to decrement.</param>
        public static TInt operator --(TInt i) => new TInt(i.InnerValue - 1);

        /// <summary>A <see cref="TInt"/> representing <see cref="int.MaxValue"/> (2,147,483,647).</summary>
        public static TInt MaxValue = int.MaxValue;

        /// <summary>A <see cref="TInt"/> representing <see cref="int.MinValue"/> (-2,147,483,648).</summary>
        public static TInt MinValue = int.MinValue;

        /// <summary>Returns <see cref="int"/> as the underlying CLR type of this field.</summary>
        public override Type   GetUnderlyingType()  => typeof(int);

        /// <summary>Returns the string token <c>"int"</c> used by the ORM schema system.</summary>
        public override string GetTypeDescription()  => "int";

        /// <summary>Returns the current value as a boxed <see cref="int"/>.</summary>
        public override object GetValue()            => InnerValue;

        /// <summary>
        /// Sets the backing value from an arbitrary <see cref="object"/>.
        /// Accepts a <see cref="TInt"/> (copies its inner value) or anything
        /// convertible via <see cref="Convert.ToInt32(object)"/>.
        /// </summary>
        /// <param name="value">The value to assign.</param>
        public override void SetDerivedValue(object value)
        {
            if (value is TInt ti) InnerValue = ti.InnerValue;
            else                  InnerValue = Convert.ToInt32(value);
        }

        /// <summary>
        /// Sets the field to the given <paramref name="value"/> and clears any conversion-error flag.
        /// </summary>
        /// <param name="value">The integer value to store.</param>
        public void SetValue(int value) { base.SetValue(value); ConversionError = false; }

        /// <summary>
        /// Marks the field as null or non-null.  When set to null the backing value is reset to zero.
        /// </summary>
        /// <param name="isNull"><see langword="true"/> to null the field; <see langword="false"/> to un-null it.</param>
        public override void SetNull(bool isNull)
        {
            base.SetNull(isNull);
            if (isNull) InnerValue = 0;
        }

        /// <summary>Returns the default string representation of the current integer value.</summary>
        public override string ToString()                                => InnerValue.ToString();

        /// <summary>Formats the current value using the specified <paramref name="p"/> format provider.</summary>
        /// <param name="p">Culture-specific formatting information.</param>
        public string          ToString(IFormatProvider p)               => InnerValue.ToString(p);

        /// <summary>Formats the current value using <paramref name="fmt"/> and <paramref name="p"/>.</summary>
        /// <param name="fmt">A standard or custom numeric format string.</param>
        /// <param name="p">Culture-specific formatting information.</param>
        public string          ToString(string fmt, IFormatProvider p)   => InnerValue.ToString(fmt, p);

        /// <summary>Formats the current value using the given <paramref name="fmt"/> format string.</summary>
        /// <param name="fmt">A standard or custom numeric format string.</param>
        public string          ToString(string fmt)                      => InnerValue.ToString(fmt);

        /// <summary>
        /// Compares this instance to <paramref name="obj"/>.
        /// Null fields sort before non-null fields; two null fields are considered equal.
        /// Accepts a <see cref="TInt"/> or any value convertible to <see cref="int"/>.
        /// </summary>
        /// <param name="obj">The object to compare against.</param>
        /// <returns>A negative number, zero, or a positive number.</returns>
        public int CompareTo(object obj)
        {
            if (obj is TInt other)
            {
                if (IsNull() && other.IsNull()) return 0;
                if (IsNull()) return -1;
                if (other.IsNull()) return 1;
                return InnerValue.CompareTo(other.InnerValue);
            }
            return InnerValue.CompareTo(Convert.ToInt32(obj));
        }

        /// <summary>Parses <paramref name="s"/> using the default format and returns a new <see cref="TInt"/>.</summary>
        /// <param name="s">A string containing the number to parse.</param>
        public static TInt Parse(string s)                                     => int.Parse(s);

        /// <summary>Parses <paramref name="s"/> with the specified <see cref="NumberStyles"/> and returns a new <see cref="TInt"/>.</summary>
        /// <param name="s">A string containing the number to parse.</param>
        /// <param name="style">A bitwise combination of <see cref="NumberStyles"/> values.</param>
        public static TInt Parse(string s, NumberStyles style)                 => int.Parse(s, style);

        /// <summary>Parses <paramref name="s"/> using the specified format provider and returns a new <see cref="TInt"/>.</summary>
        /// <param name="s">A string containing the number to parse.</param>
        /// <param name="p">An <see cref="IFormatProvider"/> supplying culture-specific format information.</param>
        public static TInt Parse(string s, IFormatProvider p)                  => int.Parse(s, p);

        /// <summary>
        /// Parses <paramref name="s"/> using the specified <see cref="NumberStyles"/> and format provider,
        /// and returns a new <see cref="TInt"/>.
        /// </summary>
        /// <param name="s">A string containing the number to parse.</param>
        /// <param name="style">A bitwise combination of <see cref="NumberStyles"/> values.</param>
        /// <param name="p">An <see cref="IFormatProvider"/> supplying culture-specific format information.</param>
        public static TInt Parse(string s, NumberStyles style, IFormatProvider p) => int.Parse(s, style, p);

        /// <summary>Determines value equality using the shared ORM equality helper.</summary>
        public override bool Equals(object obj) => EqualsHelper<TInt, int>(obj);

        /// <summary>Returns zero when null; otherwise delegates to <see cref="int.GetHashCode()"/>.</summary>
        public override int  GetHashCode()      => IsNull() ? 0 : InnerValue.GetHashCode();
    }
}
