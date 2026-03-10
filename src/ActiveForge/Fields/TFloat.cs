using System;
using System.Diagnostics;

namespace ActiveForge
{
    /// <summary>
    /// ORM field that wraps the CLR <see cref="float"/> (single-precision) type,
    /// mapping to a single-precision floating-point database column (e.g. REAL / FLOAT4 in SQL).
    /// <para>
    /// Use <see cref="TField.SetValue(object)"/> or the typed overload
    /// <see cref="SetValue(float)"/> to assign a value, <see cref="TField.GetValue()"/>
    /// to retrieve it as <see cref="object"/>, or rely on the implicit conversions
    /// between <c>TFloat</c> and <c>float</c> for transparent usage.
    /// </para>
    /// </summary>
    [Serializable]
    [DebuggerDisplay("Null={Null} Value={InnerValue}")]
    public class TFloat : TField, IComparable
    {
        /// <summary>Backing store for the field value.</summary>
        protected float InnerValue;

        /// <summary>
        /// Gets or sets the field value, enforcing null/validity checks on read.
        /// </summary>
        protected float Value { get { CheckValidity(); return InnerValue; } set { InnerValue = value; } }

        /// <summary>Initialises a new <see cref="TFloat"/> with a value of zero and null state unset.</summary>
        public TFloat()          { InnerValue = 0; }

        /// <summary>Initialises a new <see cref="TFloat"/> with the specified <paramref name="v"/> value.</summary>
        /// <param name="v">The initial float value.</param>
        public TFloat(float v)   { SetValue(v); }

        /// <summary>
        /// Initialises a new <see cref="TFloat"/> by converting <paramref name="v"/> using
        /// <see cref="TField.SetValue(object)"/>.
        /// </summary>
        /// <param name="v">Any value that can be converted to <see cref="float"/>.</param>
        public TFloat(object v)  { SetValue(v); }

        /// <summary>Implicitly converts a <see cref="TFloat"/> to its underlying <see cref="float"/> value.</summary>
        /// <param name="t">The field instance to convert.</param>
        public static implicit operator float(TFloat t) => t.InnerValue;

        /// <summary>Implicitly wraps a <see cref="float"/> literal or variable in a new <see cref="TFloat"/>.</summary>
        /// <param name="v">The float value to wrap.</param>
        public static implicit operator TFloat(float v) => new TFloat(v);

        /// <summary>Returns <see langword="true"/> when both operands are equal (null-aware).</summary>
        public static bool operator ==(TFloat o1, TFloat o2) => EqualityOperatorHelper<TFloat>(o1, o2);

        /// <summary>Returns <see langword="true"/> when the operands are not equal.</summary>
        public static bool operator !=(TFloat o1, TFloat o2) => !(o1 == o2);

        /// <summary>Returns <see cref="float"/> as the underlying CLR type of this field.</summary>
        public override Type   GetUnderlyingType()  => typeof(float);

        /// <summary>Returns the string token <c>"float"</c> used by the ORM schema system.</summary>
        public override string GetTypeDescription()  => "float";

        /// <summary>Returns the current value as a boxed <see cref="float"/>.</summary>
        public override object GetValue()            => InnerValue;

        /// <summary>
        /// Sets the backing value from an arbitrary <see cref="object"/>.
        /// Accepts a <see cref="TFloat"/> (copies its inner value) or anything
        /// convertible via <see cref="Convert.ToSingle(object)"/>.
        /// </summary>
        /// <param name="value">The value to assign.</param>
        public override void SetDerivedValue(object value)
        {
            if (value is TFloat tf) InnerValue = tf.InnerValue;
            else                    InnerValue = Convert.ToSingle(value);
        }

        /// <summary>
        /// Sets the field to the given <paramref name="value"/> and clears any conversion-error flag.
        /// </summary>
        /// <param name="value">The float value to store.</param>
        public void SetValue(float value) { base.SetValue(value); ConversionError = false; }

        /// <summary>
        /// Marks the field as null or non-null.  When set to null the backing value is reset to zero.
        /// </summary>
        /// <param name="isNull"><see langword="true"/> to null the field; <see langword="false"/> to un-null it.</param>
        public override void SetNull(bool isNull) { base.SetNull(isNull); if (isNull) InnerValue = 0; }

        /// <summary>Returns the default string representation of the current float value.</summary>
        public override string ToString() => InnerValue.ToString();

        /// <summary>
        /// Compares this instance to <paramref name="obj"/>.
        /// Accepts a <see cref="TFloat"/> or any value convertible to <see cref="float"/>.
        /// </summary>
        /// <param name="obj">The object to compare against.</param>
        /// <returns>A negative number, zero, or a positive number.</returns>
        public int CompareTo(object obj)
        {
            if (obj is TFloat other) return InnerValue.CompareTo(other.InnerValue);
            return InnerValue.CompareTo(Convert.ToSingle(obj));
        }

        /// <summary>Determines value equality using the shared ORM equality helper.</summary>
        public override bool Equals(object obj) => EqualsHelper<TFloat, float>(obj);

        /// <summary>Returns zero when null; otherwise delegates to <see cref="float.GetHashCode()"/>.</summary>
        public override int  GetHashCode()      => IsNull() ? 0 : InnerValue.GetHashCode();
    }
}
