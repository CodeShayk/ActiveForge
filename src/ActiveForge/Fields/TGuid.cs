using System;
using System.Diagnostics;

namespace ActiveForge
{
    /// <summary>
    /// ORM field that wraps the CLR <see cref="Guid"/> type, mapping to a UNIQUEIDENTIFIER
    /// (or equivalent UUID) database column.
    /// <para>
    /// Use <see cref="TField.SetValue(object)"/> or the typed overload
    /// <see cref="SetValue(Guid)"/> to assign a value, <see cref="TField.GetValue()"/>
    /// to retrieve it as <see cref="object"/>, or rely on the implicit conversions
    /// between <c>TGuid</c> and <c>Guid</c> for transparent usage.
    /// String values passed to <see cref="TField.SetValue(object)"/> are parsed with
    /// <c>new Guid(string)</c>, so they must be in a recognised GUID format.
    /// </para>
    /// </summary>
    [Serializable]
    [DebuggerDisplay("Null={Null} Value={InnerValue}")]
    public class TGuid : TField, IComparable
    {
        /// <summary>Backing store for the field value.</summary>
        protected Guid InnerValue;

        /// <summary>
        /// Gets or sets the field value, enforcing null/validity checks on read.
        /// </summary>
        protected Guid Value { get { CheckValidity(); return InnerValue; } set { InnerValue = value; } }

        /// <summary>Initialises a new <see cref="TGuid"/> set to <see cref="Guid.Empty"/> and null state unset.</summary>
        public TGuid()          { InnerValue = Guid.Empty; }

        /// <summary>Initialises a new <see cref="TGuid"/> with the specified <paramref name="v"/> value.</summary>
        /// <param name="v">The initial GUID value.</param>
        public TGuid(Guid v)    { SetValue(v); }

        /// <summary>
        /// Initialises a new <see cref="TGuid"/> by converting <paramref name="v"/> using
        /// <see cref="TField.SetValue(object)"/>.
        /// </summary>
        /// <param name="v">A <see cref="Guid"/>, <see cref="TGuid"/>, or string in GUID format.</param>
        public TGuid(object v)  { SetValue(v); }

        /// <summary>Implicitly converts a <see cref="TGuid"/> to its underlying <see cref="Guid"/> value.</summary>
        /// <param name="t">The field instance to convert.</param>
        public static implicit operator Guid(TGuid t)  => t.InnerValue;

        /// <summary>Implicitly wraps a <see cref="Guid"/> in a new <see cref="TGuid"/>.</summary>
        /// <param name="v">The GUID value to wrap.</param>
        public static implicit operator TGuid(Guid v)  => new TGuid(v);

        /// <summary>Returns <see langword="true"/> when both operands are equal (null-aware).</summary>
        public static bool operator ==(TGuid o1, TGuid o2) => EqualityOperatorHelper<TGuid>(o1, o2);

        /// <summary>Returns <see langword="true"/> when the operands are not equal.</summary>
        public static bool operator !=(TGuid o1, TGuid o2) => !(o1 == o2);

        /// <summary>Returns <see cref="Guid"/> as the underlying CLR type of this field.</summary>
        public override Type   GetUnderlyingType()  => typeof(Guid);

        /// <summary>Returns the string token <c>"guid"</c> used by the ORM schema system.</summary>
        public override string GetTypeDescription()  => "guid";

        /// <summary>Returns the current value as a boxed <see cref="Guid"/>.</summary>
        public override object GetValue()            => InnerValue;

        /// <summary>
        /// Sets the backing value from an arbitrary <see cref="object"/>.
        /// Accepts a <see cref="TGuid"/> (copies its inner value), a <see cref="Guid"/>,
        /// or any object whose <c>ToString()</c> produces a valid GUID string.
        /// </summary>
        /// <param name="value">The value to assign.</param>
        public override void SetDerivedValue(object value)
        {
            if (value is TGuid tg)   InnerValue = tg.InnerValue;
            else if (value is Guid g) InnerValue = g;
            else                      InnerValue = new Guid(value.ToString());
        }

        /// <summary>
        /// Sets the field to the given <paramref name="value"/> and clears any conversion-error flag.
        /// </summary>
        /// <param name="value">The GUID value to store.</param>
        public void SetValue(Guid value) { base.SetValue(value); ConversionError = false; }

        /// <summary>
        /// Marks the field as null or non-null.  When set to null the backing value is reset to <see cref="Guid.Empty"/>.
        /// </summary>
        /// <param name="isNull"><see langword="true"/> to null the field; <see langword="false"/> to un-null it.</param>
        public override void SetNull(bool isNull) { base.SetNull(isNull); if (isNull) InnerValue = Guid.Empty; }

        /// <summary>Returns the standard 32-hex-digit GUID string representation.</summary>
        public override string ToString() => InnerValue.ToString();

        /// <summary>
        /// Compares this instance to <paramref name="obj"/>.
        /// Accepts a <see cref="TGuid"/>, a <see cref="Guid"/>, or any object whose
        /// <c>ToString()</c> is a valid GUID string.
        /// </summary>
        /// <param name="obj">The object to compare against.</param>
        /// <returns>A negative number, zero, or a positive number.</returns>
        public int CompareTo(object obj)
        {
            if (obj is TGuid other) return InnerValue.CompareTo(other.InnerValue);
            if (obj is Guid  g)     return InnerValue.CompareTo(g);
            return InnerValue.CompareTo(new Guid(obj.ToString()));
        }

        /// <summary>Determines value equality using the shared ORM equality helper.</summary>
        public override bool Equals(object obj) => EqualsHelper<TGuid, Guid>(obj);

        /// <summary>Returns zero when null; otherwise delegates to <see cref="Guid.GetHashCode()"/>.</summary>
        public override int  GetHashCode()      => IsNull() ? 0 : InnerValue.GetHashCode();
    }
}
