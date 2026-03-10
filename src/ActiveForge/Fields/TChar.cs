using System;
using System.Diagnostics;

namespace ActiveForge
{
    /// <summary>
    /// Database field that wraps a CLR <see cref="char"/> value, mapping to a SQL
    /// CHAR(1) (or equivalent single-character) column.
    /// <para>
    /// Use <c>SetValue(char)</c> / <c>GetValue()</c> to assign and retrieve the value
    /// programmatically, or use the implicit conversion operators to work with plain
    /// <see cref="char"/> literals and variables.
    /// </para>
    /// <para>
    /// When constructed from a <see cref="string"/>, only the first character is used.
    /// A null-state field has <c>InnerValue</c> equal to <c>'\0'</c>.
    /// </para>
    /// <example>
    /// <code>
    /// record.Grade.SetValue('A');
    /// char c = record.Grade;   // implicit cast to char
    /// record.Grade = 'B';      // implicit cast from char
    /// </code>
    /// </example>
    /// </summary>
    [Serializable]
    [DebuggerDisplay("Null={Null} Value={InnerValue}")]
    public class TChar : TField, IComparable
    {
        /// <summary>
        /// Backing store for the char value.  Defaults to <c>'\0'</c> when the field
        /// is in the null state.
        /// </summary>
        protected char InnerValue;

        /// <summary>
        /// Gets or sets the underlying char value.  The getter calls
        /// <see cref="TField.CheckValidity"/> and throws
        /// <see cref="PersistenceException"/> if the field is null.
        /// External callers should use the implicit cast or <see cref="GetValue()"/>
        /// instead.
        /// </summary>
        protected char Value { get { CheckValidity(); return InnerValue; } set { InnerValue = value; } }

        /// <summary>
        /// Initialises a new <see cref="TChar"/> in the null state with
        /// <c>InnerValue</c> set to <c>'\0'</c>.
        /// </summary>
        public TChar()          { InnerValue = '\0'; }

        /// <summary>
        /// Initialises a new <see cref="TChar"/> with the specified character value.
        /// The field is immediately non-null.
        /// </summary>
        /// <param name="v">The initial character value.</param>
        public TChar(char v)    { SetValue(v); }

        /// <summary>
        /// Initialises a new <see cref="TChar"/> from an untyped object via
        /// <see cref="TField.SetValue(object)"/>.  Supports a plain <see cref="char"/>,
        /// a single-character <see cref="string"/>, or any object that
        /// <see cref="Convert.ToChar(object)"/> can coerce.
        /// </summary>
        /// <param name="v">The value to convert and store.</param>
        public TChar(object v)  { SetValue(v); }

        /// <summary>
        /// Implicitly converts a <see cref="TChar"/> field to a plain <see cref="char"/>.
        /// Returns <c>InnerValue</c> directly without a null-guard; callers should check
        /// <see cref="TField.IsNull()"/> first if the field may be null.
        /// </summary>
        /// <param name="t">The source <see cref="TChar"/> field.</param>
        /// <returns>The underlying character value.</returns>
        public static implicit operator char(TChar t) => t.InnerValue;

        /// <summary>
        /// Implicitly wraps a plain <see cref="char"/> in a new non-null
        /// <see cref="TChar"/> field.
        /// </summary>
        /// <param name="v">The character value to wrap.</param>
        /// <returns>A new <see cref="TChar"/> containing <paramref name="v"/>.</returns>
        public static implicit operator TChar(char v) => new TChar(v);

        /// <summary>Determines whether two <see cref="TChar"/> fields are equal.</summary>
        public static bool operator ==(TChar o1, TChar o2) => EqualityOperatorHelper<TChar>(o1, o2);

        /// <summary>Determines whether two <see cref="TChar"/> fields are not equal.</summary>
        public static bool operator !=(TChar o1, TChar o2) => !(o1 == o2);

        /// <summary>Returns <c>typeof(char)</c>.</summary>
        public override Type   GetUnderlyingType()  => typeof(char);

        /// <summary>Returns <c>"char"</c>, the short database type identifier for this field.</summary>
        public override string GetTypeDescription()  => "char";

        /// <summary>Returns the underlying <see cref="char"/> value boxed as an <see cref="object"/>.</summary>
        public override object GetValue()            => InnerValue;

        /// <summary>
        /// Sets <c>InnerValue</c> from <paramref name="value"/>.  Accepts another
        /// <see cref="TChar"/> (copies its inner value directly), a plain
        /// <see cref="char"/>, a non-empty <see cref="string"/> (takes the first
        /// character), or any object that <see cref="Convert.ToChar(object)"/> can coerce.
        /// </summary>
        /// <param name="value">The non-null source value.</param>
        public override void SetDerivedValue(object value)
        {
            if (value is TChar tc)   InnerValue = tc.InnerValue;
            else if (value is char c) InnerValue = c;
            else if (value is string s && s.Length > 0) InnerValue = s[0];
            else                      InnerValue = Convert.ToChar(value);
        }

        /// <summary>
        /// Sets the field to <paramref name="value"/> and clears any previously recorded
        /// conversion error.
        /// </summary>
        /// <param name="value">The character value to store.</param>
        public void SetValue(char value) { base.SetValue(value); ConversionError = false; }

        /// <summary>
        /// Sets the null state of the field.  When <paramref name="isNull"/> is
        /// <c>true</c>, <c>InnerValue</c> is reset to <c>'\0'</c>.
        /// </summary>
        /// <param name="isNull"><c>true</c> to null the field; <c>false</c> to un-null it.</param>
        public override void SetNull(bool isNull) { base.SetNull(isNull); if (isNull) InnerValue = '\0'; }

        /// <summary>Returns the string representation of <c>InnerValue</c> (a one-character string).</summary>
        public override string ToString() => InnerValue.ToString();

        /// <summary>
        /// Compares this field with <paramref name="obj"/> using Unicode ordinal ordering.
        /// Accepts another <see cref="TChar"/> or any object that
        /// <see cref="Convert.ToChar(object)"/> can coerce.
        /// </summary>
        /// <param name="obj">The value to compare against.</param>
        /// <returns>
        /// A negative integer, zero, or a positive integer indicating whether this
        /// field is less than, equal to, or greater than <paramref name="obj"/>.
        /// </returns>
        public int CompareTo(object obj)
        {
            if (obj is TChar other) return InnerValue.CompareTo(other.InnerValue);
            return InnerValue.CompareTo(Convert.ToChar(obj));
        }

        /// <summary>
        /// Determines whether this field equals <paramref name="obj"/>, using consistent
        /// null semantics: two null-state fields are equal; a null-state field and a
        /// non-null field are not.
        /// </summary>
        /// <param name="obj">The object to compare against.</param>
        /// <returns><c>true</c> if equal; otherwise <c>false</c>.</returns>
        public override bool Equals(object obj) => EqualsHelper<TChar, char>(obj);

        /// <summary>
        /// Returns a hash code for this field.  Null-state fields return <c>0</c>;
        /// non-null fields return the hash code of the underlying <see cref="char"/> value.
        /// </summary>
        public override int  GetHashCode()      => IsNull() ? 0 : InnerValue.GetHashCode();
    }
}
