using System;
using System.Diagnostics;

namespace ActiveForge
{
    /// <summary>
    /// Database field that wraps a CLR <see cref="byte"/> value, mapping to a SQL
    /// TINYINT (or equivalent 8-bit unsigned integer) column.
    /// <para>
    /// Use <c>SetValue(byte)</c> / <c>GetValue()</c> to assign and retrieve the value
    /// programmatically, or use the implicit conversion operators to work with plain
    /// <see cref="byte"/> literals and variables.
    /// </para>
    /// <example>
    /// <code>
    /// record.Flag.SetValue((byte)42);
    /// byte b = record.Flag;   // implicit cast to byte
    /// record.Flag = 0;        // implicit cast from byte
    /// </code>
    /// </example>
    /// </summary>
    [Serializable]
    [DebuggerDisplay("Null={Null} Value={InnerValue}")]
    public class TByte : TField, IComparable
    {
        /// <summary>
        /// Backing store for the byte value.  Defaults to <c>0</c> when the field is
        /// in the null state.
        /// </summary>
        protected byte InnerValue;

        /// <summary>
        /// Gets or sets the underlying byte value.  The getter calls
        /// <see cref="TField.CheckValidity"/> and throws
        /// <see cref="PersistenceException"/> if the field is null.
        /// External callers should use the implicit cast or <see cref="GetValue()"/>
        /// instead.
        /// </summary>
        protected byte Value { get { CheckValidity(); return InnerValue; } set { InnerValue = value; } }

        /// <summary>
        /// Initialises a new <see cref="TByte"/> in the null state with
        /// <c>InnerValue</c> set to <c>0</c>.
        /// </summary>
        public TByte()          { InnerValue = 0; }

        /// <summary>
        /// Initialises a new <see cref="TByte"/> with the specified byte value.
        /// The field is immediately non-null.
        /// </summary>
        /// <param name="v">The initial byte value.</param>
        public TByte(byte v)    { SetValue(v); }

        /// <summary>
        /// Initialises a new <see cref="TByte"/> from an untyped object via
        /// <see cref="TField.SetValue(object)"/>.  Supports any value that
        /// <see cref="Convert.ToByte(object)"/> can coerce.
        /// </summary>
        /// <param name="v">The value to convert and store.</param>
        public TByte(object v)  { SetValue(v); }

        /// <summary>
        /// Implicitly converts a <see cref="TByte"/> field to a plain <see cref="byte"/>.
        /// Returns <c>InnerValue</c> directly without a null-guard; callers should check
        /// <see cref="TField.IsNull()"/> first if the field may be null.
        /// </summary>
        /// <param name="t">The source <see cref="TByte"/> field.</param>
        /// <returns>The underlying byte value.</returns>
        public static implicit operator byte(TByte t) => t.InnerValue;

        /// <summary>
        /// Implicitly wraps a plain <see cref="byte"/> in a new non-null
        /// <see cref="TByte"/> field.
        /// </summary>
        /// <param name="v">The byte value to wrap.</param>
        /// <returns>A new <see cref="TByte"/> containing <paramref name="v"/>.</returns>
        public static implicit operator TByte(byte v) => new TByte(v);

        /// <summary>Determines whether two <see cref="TByte"/> fields are equal.</summary>
        public static bool operator ==(TByte o1, TByte o2) => EqualityOperatorHelper<TByte>(o1, o2);

        /// <summary>Determines whether two <see cref="TByte"/> fields are not equal.</summary>
        public static bool operator !=(TByte o1, TByte o2) => !(o1 == o2);

        /// <summary>Returns <c>typeof(byte)</c>.</summary>
        public override Type   GetUnderlyingType()  => typeof(byte);

        /// <summary>Returns <c>"byte"</c>, the short database type identifier for this field.</summary>
        public override string GetTypeDescription()  => "byte";

        /// <summary>Returns the underlying <see cref="byte"/> value boxed as an <see cref="object"/>.</summary>
        public override object GetValue()            => InnerValue;

        /// <summary>
        /// Sets <c>InnerValue</c> from <paramref name="value"/>.  Accepts another
        /// <see cref="TByte"/> (copies its inner value directly) or any object that
        /// <see cref="Convert.ToByte(object)"/> can coerce.
        /// </summary>
        /// <param name="value">The non-null source value.</param>
        public override void SetDerivedValue(object value)
        {
            if (value is TByte tb) InnerValue = tb.InnerValue;
            else                   InnerValue = Convert.ToByte(value);
        }

        /// <summary>
        /// Sets the field to <paramref name="value"/> and clears any previously recorded
        /// conversion error.
        /// </summary>
        /// <param name="value">The byte value to store.</param>
        public void SetValue(byte value) { base.SetValue(value); ConversionError = false; }

        /// <summary>
        /// Sets the null state of the field.  When <paramref name="isNull"/> is
        /// <c>true</c>, <c>InnerValue</c> is reset to <c>0</c>.
        /// </summary>
        /// <param name="isNull"><c>true</c> to null the field; <c>false</c> to un-null it.</param>
        public override void SetNull(bool isNull) { base.SetNull(isNull); if (isNull) InnerValue = 0; }

        /// <summary>Returns the string representation of <c>InnerValue</c>.</summary>
        public override string ToString() => InnerValue.ToString();

        /// <summary>
        /// Compares this field with <paramref name="obj"/> using byte ordering.
        /// Accepts another <see cref="TByte"/> or any object that
        /// <see cref="Convert.ToByte(object)"/> can coerce.
        /// </summary>
        /// <param name="obj">The value to compare against.</param>
        /// <returns>
        /// A negative integer, zero, or a positive integer indicating whether this
        /// field is less than, equal to, or greater than <paramref name="obj"/>.
        /// </returns>
        public int CompareTo(object obj)
        {
            if (obj is TByte other) return InnerValue.CompareTo(other.InnerValue);
            return InnerValue.CompareTo(Convert.ToByte(obj));
        }

        /// <summary>
        /// Determines whether this field equals <paramref name="obj"/>, using consistent
        /// null semantics: two null-state fields are equal; a null-state field and a
        /// non-null field are not.
        /// </summary>
        /// <param name="obj">The object to compare against.</param>
        /// <returns><c>true</c> if equal; otherwise <c>false</c>.</returns>
        public override bool Equals(object obj) => EqualsHelper<TByte, byte>(obj);

        /// <summary>
        /// Returns a hash code for this field.  Null-state fields return <c>0</c>;
        /// non-null fields return the hash code of the underlying <see cref="byte"/> value.
        /// </summary>
        public override int  GetHashCode()      => IsNull() ? 0 : InnerValue.GetHashCode();
    }
}
