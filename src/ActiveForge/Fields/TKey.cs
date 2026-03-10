using System;
using System.Diagnostics;

namespace ActiveForge
{
    /// <summary>
    /// Abstract base class for integer key fields in the ORM, wrapping a 32-bit signed integer
    /// that represents either a primary or foreign key value in the database.
    /// <para>
    /// <see cref="TKey"/> extends <see cref="TInt"/> with key-specific semantics:
    /// <see cref="GetUnderlyingType()"/> returns the concrete runtime type (e.g.
    /// <see cref="TPrimaryKey"/> or <see cref="TForeignKey"/>) rather than
    /// <see cref="int"/>, allowing the ORM schema layer to distinguish key columns
    /// from plain integer columns.
    /// </para>
    /// <para>
    /// Do not use <see cref="TKey"/> directly on domain objects; use the concrete subclasses
    /// <see cref="TPrimaryKey"/> and <see cref="TForeignKey"/> instead.
    /// </para>
    /// </summary>
    [Serializable]
    [DebuggerDisplay("Null={Null} Value={InnerValue}")]
    public class TKey : TInt
    {
        /// <summary>Initialises a new null <see cref="TKey"/> with value zero.</summary>
        public TKey()           : base() { }

        /// <summary>Initialises a new <see cref="TKey"/> with the specified integer <paramref name="value"/>.</summary>
        /// <param name="value">The key integer value.</param>
        public TKey(int value)  : base(value) { }

        /// <summary>
        /// Initialises a new <see cref="TKey"/> by converting <paramref name="v"/> via
        /// <see cref="TField.SetValue(object)"/>.
        /// </summary>
        /// <param name="v">Any value convertible to <c>int</c>.</param>
        public TKey(object v)   : base(v) { }

        /// <summary>
        /// Returns the concrete runtime type of this key field (e.g. <see cref="TPrimaryKey"/> or
        /// <see cref="TForeignKey"/>), rather than the underlying <see cref="int"/> CLR type.
        /// This allows the ORM schema system to differentiate key fields from plain integer columns.
        /// </summary>
        public override Type GetUnderlyingType() => GetType();

        /// <summary>Returns <see langword="true"/> when <paramref name="k1"/> equals the integer <paramref name="k2"/>.</summary>
        public static bool operator ==(TKey k1, int k2)   => k1 == (TKey)k2;

        /// <summary>Returns <see langword="true"/> when <paramref name="k1"/> does not equal the integer <paramref name="k2"/>.</summary>
        public static bool operator !=(TKey k1, int k2)   => k1 != (TKey)k2;

        /// <summary>Returns <see langword="true"/> when both <see cref="TKey"/> operands are equal (null-aware).</summary>
        public static bool operator ==(TKey k1, TKey k2)  => EqualityOperatorHelper<TKey>(k1, k2);

        /// <summary>Returns <see langword="true"/> when the <see cref="TKey"/> operands are not equal.</summary>
        public static bool operator !=(TKey k1, TKey k2)  => !(k1 == k2);

        /// <summary>Determines value equality using the shared ORM equality helper.</summary>
        public override bool Equals(object obj) => EqualsHelper<TKey, int>(obj);

        /// <summary>Delegates to the base <see cref="TInt"/> hash-code implementation.</summary>
        public override int  GetHashCode()      => base.GetHashCode();
    }
}
