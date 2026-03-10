using System;
using System.Diagnostics;

namespace ActiveForge
{
    /// <summary>
    /// Database field that wraps a CLR <see cref="bool"/> value, mapping to a SQL BIT
    /// (or equivalent boolean) column.
    /// <para>
    /// Use <c>SetValue(bool)</c> / <c>GetValue()</c> to assign and retrieve the value
    /// programmatically, or use the implicit conversion operators to work with plain
    /// <see cref="bool"/> literals and variables.
    /// </para>
    /// <example>
    /// <code>
    /// product.InStock.SetValue(true);
    /// bool b = product.InStock;   // implicit cast to bool
    /// product.InStock = false;    // implicit cast from bool
    /// </code>
    /// </example>
    /// </summary>
    [Serializable]
    [DebuggerDisplay("Null={Null} Value={InnerValue}")]
    public class TBool : TField, IComparable
    {
        /// <summary>
        /// Backing store for the boolean value.  Defaults to <c>false</c> when the
        /// field is in the null state.
        /// </summary>
        protected bool InnerValue;

        /// <summary>
        /// Gets or sets the underlying boolean value.  The getter calls
        /// <see cref="TField.CheckValidity"/> and throws
        /// <see cref="PersistenceException"/> if the field is null.
        /// External callers should use the implicit cast or <see cref="GetValue()"/>
        /// instead.
        /// </summary>
        protected bool Value { get { CheckValidity(); return InnerValue; } set { InnerValue = value; } }

        /// <summary>
        /// Initialises a new <see cref="TBool"/> in the null state with
        /// <c>InnerValue</c> set to <c>false</c>.
        /// </summary>
        public TBool()           { InnerValue = false; }

        /// <summary>
        /// Initialises a new <see cref="TBool"/> with the specified boolean value.
        /// The field is immediately non-null.
        /// </summary>
        /// <param name="value">The initial boolean value.</param>
        public TBool(bool value) { SetValue(value); }

        /// <summary>
        /// Initialises a new <see cref="TBool"/> from an untyped object via
        /// <see cref="TField.SetValue(object)"/>.  Supports any value that
        /// <see cref="Convert.ToBoolean(object)"/> can coerce.
        /// </summary>
        /// <param name="v">The value to convert and store.</param>
        public TBool(object v)   { SetValue(v); }

        /// <summary>
        /// Implicitly converts a <see cref="TBool"/> field to a plain <see cref="bool"/>.
        /// Returns <c>InnerValue</c> directly without a null-guard; callers should check
        /// <see cref="TField.IsNull()"/> first if the field may be null.
        /// </summary>
        /// <param name="t">The source <see cref="TBool"/> field.</param>
        /// <returns>The underlying boolean value.</returns>
        public static implicit operator bool(TBool t)   => t.InnerValue;

        /// <summary>
        /// Implicitly wraps a plain <see cref="bool"/> in a new non-null
        /// <see cref="TBool"/> field.
        /// </summary>
        /// <param name="v">The boolean value to wrap.</param>
        /// <returns>A new <see cref="TBool"/> containing <paramref name="v"/>.</returns>
        public static implicit operator TBool(bool v)   => new TBool(v);

        /// <summary>Determines whether two <see cref="TBool"/> fields are equal.</summary>
        public static bool operator ==(TBool o1, TBool o2) => EqualityOperatorHelper<TBool>(o1, o2);

        /// <summary>Determines whether two <see cref="TBool"/> fields are not equal.</summary>
        public static bool operator !=(TBool o1, TBool o2) => !(o1 == o2);

        /// <summary>Determines whether a <see cref="TBool"/> field equals a plain <see cref="bool"/>.</summary>
        public static bool operator ==(TBool o1, bool  o2) => o1 == (TBool)o2;

        /// <summary>Determines whether a <see cref="TBool"/> field does not equal a plain <see cref="bool"/>.</summary>
        public static bool operator !=(TBool o1, bool  o2) => o1 != (TBool)o2;

        /// <summary>Determines whether a plain <see cref="bool"/> equals a <see cref="TBool"/> field.</summary>
        public static bool operator ==(bool  o1, TBool o2) => (TBool)o1 == o2;

        /// <summary>Determines whether a plain <see cref="bool"/> does not equal a <see cref="TBool"/> field.</summary>
        public static bool operator !=(bool  o1, TBool o2) => (TBool)o1 != o2;

        /// <summary>Returns <c>typeof(bool)</c>.</summary>
        public override Type   GetUnderlyingType()  => typeof(bool);

        /// <summary>Returns <c>"bool"</c>, the short database type identifier for this field.</summary>
        public override string GetTypeDescription()  => "bool";

        /// <summary>Returns the underlying <see cref="bool"/> value boxed as an <see cref="object"/>.</summary>
        public override object GetValue()            => InnerValue;

        /// <summary>
        /// Sets <c>InnerValue</c> from <paramref name="value"/>.  Accepts another
        /// <see cref="TBool"/> (copies its inner value directly) or any object that
        /// <see cref="Convert.ToBoolean(object)"/> can coerce.
        /// </summary>
        /// <param name="value">The non-null source value.</param>
        public override void SetDerivedValue(object value)
        {
            if (value is TBool tb) InnerValue = tb.InnerValue;
            else                   InnerValue = Convert.ToBoolean(value);
        }

        /// <summary>
        /// Sets the field to <paramref name="value"/> and clears any previously recorded
        /// conversion error.
        /// </summary>
        /// <param name="value">The boolean value to store.</param>
        public void SetValue(bool value) { base.SetValue(value); ConversionError = false; }

        /// <summary>
        /// Sets the null state of the field.  When <paramref name="isNull"/> is
        /// <c>true</c>, <c>InnerValue</c> is reset to <c>false</c>.
        /// </summary>
        /// <param name="isNull"><c>true</c> to null the field; <c>false</c> to un-null it.</param>
        public override void SetNull(bool isNull)
        {
            base.SetNull(isNull);
            if (isNull) InnerValue = false;
        }

        /// <summary>
        /// Returns the string representation of <c>InnerValue</c>
        /// (<c>"True"</c> or <c>"False"</c>).
        /// </summary>
        public override string ToString() => InnerValue.ToString();

        /// <summary>
        /// Compares this field with <paramref name="obj"/> using boolean ordering
        /// (<c>false</c> &lt; <c>true</c>).  Accepts another <see cref="TBool"/> or
        /// any object that <see cref="Convert.ToBoolean(object)"/> can coerce.
        /// </summary>
        /// <param name="obj">The value to compare against.</param>
        /// <returns>
        /// A negative integer, zero, or a positive integer indicating whether this
        /// field is less than, equal to, or greater than <paramref name="obj"/>.
        /// </returns>
        public int CompareTo(object obj)
        {
            if (obj is TBool other) return InnerValue.CompareTo(other.InnerValue);
            return InnerValue.CompareTo(Convert.ToBoolean(obj));
        }

        /// <summary>
        /// Determines whether this field equals <paramref name="obj"/>, using consistent
        /// null semantics: two null-state fields are equal; a null-state field and a
        /// non-null field are not.
        /// </summary>
        /// <param name="obj">The object to compare against.</param>
        /// <returns><c>true</c> if equal; otherwise <c>false</c>.</returns>
        public override bool Equals(object obj) => EqualsHelper<TBool, bool>(obj);

        /// <summary>
        /// Returns a hash code for this field.  Null-state fields return <c>0</c>;
        /// non-null fields return the hash code of the underlying <see cref="bool"/> value.
        /// </summary>
        public override int  GetHashCode()      => IsNull() ? 0 : InnerValue.GetHashCode();
    }
}
