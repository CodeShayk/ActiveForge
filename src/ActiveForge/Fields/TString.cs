using System;
using System.Collections;
using System.Diagnostics;
using System.Globalization;

namespace ActiveForge
{
    /// <summary>
    /// ORM field that wraps the CLR <see cref="string"/> type, mapping to a VARCHAR / NVARCHAR /
    /// TEXT (or equivalent) database column.
    /// <para>
    /// The <c>Value</c> property is <c>protected</c>; external callers must use
    /// <see cref="TField.SetValue(object)"/> or the typed overload
    /// <c>SetValue(string)</c> to write a value, <see cref="TField.GetValue()"/> to read
    /// it as <see cref="object"/>, or let the implicit conversions between
    /// <c>TString</c> and <c>string</c> handle casting transparently.
    /// </para>
    /// <para>
    /// An empty string is never automatically converted to <c>null</c> unless
    /// <c>ConvertEmptyStringsToNull</c> is explicitly enabled on the base class.
    /// Null fields compare as less-than all non-null fields in
    /// <see cref="CompareTo(object)"/>.
    /// </para>
    /// </summary>
    [Serializable]
    [DebuggerDisplay("Null={Null} Value={InnerValue}")]
    public class TString : TField, IComparable, ICloneable, IEnumerable
    {
        /// <summary>Backing store for the string value; initialised to an empty string.</summary>
        protected string InnerValue = "";

        /// <summary>
        /// Gets or sets the string value, enforcing null/validity checks on read.
        /// This property is <c>protected</c>; external callers use
        /// <see cref="TField.SetValue(object)"/> or implicit casts.
        /// </summary>
        protected string Value
        {
            get { CheckValidity(); return InnerValue; }
            set { InnerValue = value; }
        }

        /// <summary>
        /// Initialises a new, empty <see cref="TString"/> with empty-string-to-null conversion disabled.
        /// </summary>
        public TString()
        {
            ConvertEmptyStringsToNull = false;
            InnerValue = "";
        }

        /// <summary>
        /// Initialises a new <see cref="TString"/> with the specified string <paramref name="value"/>.
        /// Empty-string-to-null conversion is disabled.
        /// </summary>
        /// <param name="value">The initial string value.</param>
        public TString(string value)  { ConvertEmptyStringsToNull = false; SetValue(value); }

        /// <summary>
        /// Initialises a new <see cref="TString"/> by converting <paramref name="value"/> using
        /// <see cref="TField.SetValue(object)"/>.  Empty-string-to-null conversion is disabled.
        /// </summary>
        /// <param name="value">Any value whose <c>ToString()</c> produces the desired string.</param>
        public TString(object value)  { ConvertEmptyStringsToNull = false; SetValue(value); }

        /// <summary>
        /// Implicitly converts a <see cref="TString"/> to its underlying <see cref="string"/> value.
        /// Returns the raw inner string without null checks.
        /// </summary>
        /// <param name="s">The field instance to convert.</param>
        public static implicit operator string(TString s) => s.InnerValue;

        /// <summary>Implicitly wraps a <see cref="string"/> literal or variable in a new <see cref="TString"/>.</summary>
        /// <param name="s">The string value to wrap; may be <see langword="null"/>.</param>
        public static implicit operator TString(string s) => new TString(s);

        /// <summary>Returns <see langword="true"/> when both <see cref="TString"/> operands are equal (null-aware).</summary>
        public static bool operator ==(TString s1, TString s2) => EqualityOperatorHelper<TString>(s1, s2);

        /// <summary>Returns <see langword="true"/> when the <see cref="TString"/> operands are not equal.</summary>
        public static bool operator !=(TString s1, TString s2) => !(s1 == s2);

        /// <summary>Returns <see langword="true"/> when the plain string <paramref name="s1"/> equals <paramref name="s2"/>.</summary>
        public static bool operator ==(string  s1, TString s2) => (TString)s1 == s2;

        /// <summary>Returns <see langword="true"/> when the plain string <paramref name="s1"/> does not equal <paramref name="s2"/>.</summary>
        public static bool operator !=(string  s1, TString s2) => !((TString)s1 == s2);

        /// <summary>Returns <see langword="true"/> when <paramref name="s1"/> equals the plain string <paramref name="s2"/>.</summary>
        public static bool operator ==(TString s1, string  s2) => s1 == (TString)s2;

        /// <summary>Returns <see langword="true"/> when <paramref name="s1"/> does not equal the plain string <paramref name="s2"/>.</summary>
        public static bool operator !=(TString s1, string  s2) => !(s1 == (TString)s2);

        /// <summary>Returns <see cref="string"/> as the underlying CLR type of this field.</summary>
        public override Type   GetUnderlyingType()  => typeof(string);

        /// <summary>Returns the string token <c>"string"</c> used by the ORM schema system.</summary>
        public override string GetTypeDescription()  => "string";

        /// <summary>Returns the current value as a boxed <see cref="string"/>.</summary>
        public override object GetValue()            => InnerValue;

        /// <summary>
        /// Sets the backing value from an arbitrary <see cref="object"/>.
        /// Accepts a <see cref="TString"/> (copies its inner value) or calls <c>ToString()</c>
        /// on any other object.
        /// </summary>
        /// <param name="value">The value to assign.</param>
        public override void SetDerivedValue(object value)
        {
            if (value is TString ts) { InnerValue = ts.InnerValue; }
            else                     { InnerValue = value.ToString(); }
        }

        /// <summary>
        /// Marks the field as null or non-null.  When set to null the backing value is reset to
        /// an empty string.
        /// </summary>
        /// <param name="isNull"><see langword="true"/> to null the field; <see langword="false"/> to un-null it.</param>
        public override void SetNull(bool isNull)
        {
            base.SetNull(isNull);
            if (isNull) InnerValue = "";
        }

        /// <summary>Returns the raw inner string value.</summary>
        public override string ToString() => InnerValue;

        /// <summary>
        /// Compares this instance to <paramref name="obj"/> using ordinal string comparison.
        /// Null fields are ordered before non-null fields; two null fields are considered equal.
        /// Accepts a <see cref="TString"/> or any object (compared via <c>ToString()</c>).
        /// </summary>
        /// <param name="obj">The object to compare against.</param>
        /// <returns>A negative number, zero, or a positive number.</returns>
        public int     CompareTo(object obj)
        {
            if (obj is TString other)
            {
                if (IsNull() && other.IsNull()) return 0;
                if (IsNull()) return -1;
                if (other.IsNull()) return 1;
                return string.Compare(InnerValue, other.InnerValue, StringComparison.Ordinal);
            }
            return string.Compare(InnerValue, obj?.ToString(), StringComparison.Ordinal);
        }

        /// <summary>Returns a shallow clone of this field as a new <see cref="TString"/> with the same inner value.</summary>
        public object Clone() => new TString(InnerValue);

        /// <summary>
        /// Returns an enumerator over the characters of the inner string, enabling
        /// <c>foreach</c> iteration over individual characters.
        /// </summary>
        IEnumerator IEnumerable.GetEnumerator() => InnerValue.GetEnumerator();

        /// <summary>Gets the number of characters in the current string value, or zero if null.</summary>
        public int    Length => InnerValue?.Length ?? 0;

        /// <summary>
        /// Returns <see langword="true"/> when the inner string contains the specified <paramref name="value"/>.
        /// Returns <see langword="false"/> when the field is null.
        /// </summary>
        /// <param name="value">The substring to search for.</param>
        public bool   Contains(string value)  => InnerValue?.Contains(value) ?? false;

        /// <summary>
        /// Returns a copy of the inner string converted to upper-case using the current culture.
        /// Returns <see langword="null"/> when the field is null.
        /// </summary>
        public string ToUpper()               => InnerValue?.ToUpper();

        /// <summary>
        /// Returns a copy of the inner string converted to lower-case using the current culture.
        /// Returns <see langword="null"/> when the field is null.
        /// </summary>
        public string ToLower()               => InnerValue?.ToLower();

        /// <summary>
        /// Returns a copy of the inner string with leading and trailing white-space removed.
        /// Returns <see langword="null"/> when the field is null.
        /// </summary>
        public string Trim()                  => InnerValue?.Trim();

        /// <summary>
        /// Converts an array of <see cref="TString"/> fields to a plain <see cref="string"/> array
        /// by applying the implicit <c>TString → string</c> conversion to each element.
        /// </summary>
        /// <param name="source">The array of <see cref="TString"/> fields to convert.</param>
        /// <returns>A new <see cref="string"/> array of the same length.</returns>
        public static string[] ConvertArray(TString[] source)
        {
            var result = new string[source.Length];
            for (int i = 0; i < source.Length; i++) result[i] = source[i];
            return result;
        }

        /// <summary>Determines value equality using the shared ORM equality helper.</summary>
        public override bool Equals(object obj) => EqualsHelper<TString, string>(obj);

        /// <summary>Returns zero when null; otherwise delegates to <see cref="string.GetHashCode()"/>.</summary>
        public override int  GetHashCode()      => IsNull() ? 0 : InnerValue.GetHashCode();
    }
}
