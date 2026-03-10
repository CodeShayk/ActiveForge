using System;
using System.Diagnostics;

namespace ActiveForge
{
    /// <summary>
    /// Database field that wraps a CLR <see cref="byte"/> array (<c>byte[]</c>),
    /// mapping to a SQL VARBINARY, BINARY, IMAGE, or BLOB column.
    /// <para>
    /// Use <c>SetValue(byte[])</c> / <c>GetValue()</c> to assign and retrieve the value
    /// programmatically, or use the implicit conversion operators to work with plain
    /// <c>byte[]</c> arrays directly.
    /// </para>
    /// <para>
    /// A null-state field (where no array has been assigned) returns <c>null</c> from
    /// <see cref="GetValue()"/> and has <see cref="Length"/> equal to <c>0</c>.
    /// <see cref="ToString()"/> returns an empty string for a null field, or the
    /// Base-64 encoding of the array for a non-null field.
    /// </para>
    /// <example>
    /// <code>
    /// record.Thumbnail.SetValue(imageBytes);
    /// byte[] raw = record.Thumbnail;   // implicit cast to byte[]
    /// record.Thumbnail = new byte[0];  // implicit cast from byte[]
    /// </code>
    /// </example>
    /// </summary>
    [Serializable]
    [DebuggerDisplay("Null={Null} Length={InnerValue?.Length}")]
    public class TByteArray : TField
    {
        /// <summary>
        /// Backing store for the byte-array value.  <c>null</c> when the field is in
        /// the null state.
        /// </summary>
        protected byte[] InnerValue;

        /// <summary>
        /// Gets or sets the underlying byte array.  The getter calls
        /// <see cref="TField.CheckValidity"/> and throws
        /// <see cref="PersistenceException"/> if the field is null.
        /// External callers should use the implicit cast or <see cref="GetValue()"/>
        /// instead.
        /// </summary>
        protected byte[] Value { get { CheckValidity(); return InnerValue; } set { InnerValue = value; } }

        /// <summary>
        /// Initialises a new <see cref="TByteArray"/> in the null state with
        /// <c>InnerValue</c> set to <c>null</c>.
        /// </summary>
        public TByteArray()            { }

        /// <summary>
        /// Initialises a new <see cref="TByteArray"/> with the specified byte array.
        /// The field is immediately non-null (unless <paramref name="v"/> is <c>null</c>).
        /// </summary>
        /// <param name="v">The initial byte array, or <c>null</c> to leave the field null.</param>
        public TByteArray(byte[] v)    { SetValue(v); }

        /// <summary>
        /// Initialises a new <see cref="TByteArray"/> from an untyped object via
        /// <see cref="TField.SetValue(object)"/>.  The object must be a <c>byte[]</c>,
        /// another <see cref="TByteArray"/>, or <c>null</c>.
        /// </summary>
        /// <param name="v">The value to convert and store.</param>
        public TByteArray(object v)    { SetValue(v); }

        /// <summary>
        /// Implicitly converts a <see cref="TByteArray"/> field to a plain <c>byte[]</c>.
        /// Returns <c>InnerValue</c> directly without a null-guard; the result is
        /// <c>null</c> when the field is in the null state.
        /// </summary>
        /// <param name="t">The source <see cref="TByteArray"/> field.</param>
        /// <returns>The underlying byte array, or <c>null</c>.</returns>
        public static implicit operator byte[](TByteArray t)  => t.InnerValue;

        /// <summary>
        /// Implicitly wraps a plain <c>byte[]</c> in a new <see cref="TByteArray"/> field.
        /// Passing <c>null</c> creates a null-state field.
        /// </summary>
        /// <param name="v">The byte array to wrap.</param>
        /// <returns>A new <see cref="TByteArray"/> containing <paramref name="v"/>.</returns>
        public static implicit operator TByteArray(byte[] v)  => new TByteArray(v);

        /// <summary>Returns <c>typeof(byte[])</c>.</summary>
        public override Type   GetUnderlyingType()  => typeof(byte[]);

        /// <summary>Returns <c>"bytearray"</c>, the short database type identifier for this field.</summary>
        public override string GetTypeDescription()  => "bytearray";

        /// <summary>
        /// Returns the underlying <c>byte[]</c> value boxed as an <see cref="object"/>,
        /// or <c>null</c> when the field is in the null state.
        /// </summary>
        public override object GetValue()            => InnerValue;

        /// <summary>
        /// Sets <c>InnerValue</c> from <paramref name="value"/>.  Accepts another
        /// <see cref="TByteArray"/> (copies its inner array reference), a plain
        /// <c>byte[]</c>, or any object that can be cast directly to <c>byte[]</c>.
        /// </summary>
        /// <param name="value">The non-null source value.</param>
        public override void SetDerivedValue(object value)
        {
            if (value is TByteArray tb) InnerValue = tb.InnerValue;
            else if (value is byte[] b) InnerValue = b;
            else                        InnerValue = (byte[])value;
        }

        /// <summary>
        /// Sets the field to <paramref name="value"/> and clears any previously recorded
        /// conversion error.
        /// </summary>
        /// <param name="value">The byte array to store, or <c>null</c> to null the field.</param>
        public void SetValue(byte[] value) { base.SetValue(value); ConversionError = false; }

        /// <summary>
        /// Sets the null state of the field.  When <paramref name="isNull"/> is
        /// <c>true</c>, <c>InnerValue</c> is set to <c>null</c>.
        /// </summary>
        /// <param name="isNull"><c>true</c> to null the field; <c>false</c> to un-null it.</param>
        public override void SetNull(bool isNull) { base.SetNull(isNull); if (isNull) InnerValue = null; }

        /// <summary>
        /// Returns an empty string when the field is null, or the Base-64 encoding of
        /// the underlying byte array when non-null.
        /// </summary>
        public override string ToString() => InnerValue == null ? "" : Convert.ToBase64String(InnerValue);

        /// <summary>
        /// Gets the number of bytes in the underlying array, or <c>0</c> when the field
        /// is in the null state.
        /// </summary>
        public int Length => InnerValue?.Length ?? 0;

        /// <summary>Determines whether two <see cref="TByteArray"/> fields are equal.</summary>
        public static bool operator ==(TByteArray o1, TByteArray o2) => EqualityOperatorHelper<TByteArray>(o1, o2);

        /// <summary>Determines whether two <see cref="TByteArray"/> fields are not equal.</summary>
        public static bool operator !=(TByteArray o1, TByteArray o2) => !(o1 == o2);

        /// <summary>
        /// Determines whether this field equals <paramref name="obj"/>.  Delegates to
        /// the base <see cref="TField.Equals(object)"/> implementation (reference equality
        /// on the array instance).
        /// </summary>
        /// <param name="obj">The object to compare against.</param>
        /// <returns><c>true</c> if equal; otherwise <c>false</c>.</returns>
        public override bool Equals(object obj) => base.Equals(obj);

        /// <summary>
        /// Returns a hash code for this field.  Null-state fields return <c>0</c>;
        /// non-null fields return the identity hash code of the underlying array
        /// reference (not a content hash).
        /// </summary>
        public override int  GetHashCode()      => IsNull() ? 0 : InnerValue.GetHashCode();
    }
}
