using System;
using System.Diagnostics;

namespace ActiveForge
{
    /// <summary>
    /// ORM field that wraps a 32-bit integer foreign-key value, referencing the identity
    /// (primary key) of a related <c>Record</c> in another table.
    /// <para>
    /// Inherits all integer arithmetic from <see cref="TKey"/> / <see cref="TInt"/> and
    /// adds cross-type equality operators against <see cref="TPrimaryKey"/> and <c>int</c>
    /// so that join predicates can be expressed naturally without explicit casts.
    /// </para>
    /// <para>
    /// Use <see cref="TField.SetValue(object)"/> or the typed <c>int</c> implicit conversion
    /// to assign a value, and <see cref="TField.GetValue()"/> or implicit cast to <c>int</c>
    /// to read it back.
    /// </para>
    /// </summary>
    [Serializable]
    [DebuggerDisplay("Null={Null} Value={InnerValue}")]
    public class TForeignKey : TKey
    {
        /// <summary>Initialises a new null <see cref="TForeignKey"/> with value zero.</summary>
        public TForeignKey()            : base() { }

        /// <summary>Initialises a new <see cref="TForeignKey"/> with the specified integer <paramref name="value"/>.</summary>
        /// <param name="value">The foreign-key integer value.</param>
        public TForeignKey(int value)   : base(value) { }

        /// <summary>
        /// Initialises a new <see cref="TForeignKey"/> from a <see cref="long"/>, checked-casting to <c>int</c>.
        /// </summary>
        /// <param name="value">The long value; throws <see cref="OverflowException"/> if it exceeds <see cref="int.MaxValue"/>.</param>
        public TForeignKey(long value)  : base(checked((int)value)) { }

        /// <summary>
        /// Initialises a new <see cref="TForeignKey"/> by converting <paramref name="v"/> via
        /// <see cref="TField.SetValue(object)"/>.
        /// </summary>
        /// <param name="v">Any value convertible to <c>int</c>.</param>
        public TForeignKey(object v)    : base(v) { }

        /// <summary>
        /// Initialises a new <see cref="TForeignKey"/> by copying the value from an existing <see cref="TInt"/>.
        /// </summary>
        /// <param name="value">The source integer field.</param>
        public TForeignKey(TInt value)  { CopyFrom(value); }

        /// <summary>Implicitly wraps an <c>int</c> value as a <see cref="TForeignKey"/>.</summary>
        /// <param name="v">The integer value to wrap.</param>
        public static implicit operator TForeignKey(int v)          => new TForeignKey(v);

        /// <summary>Implicitly wraps a <c>long</c> value as a <see cref="TForeignKey"/> (checked cast).</summary>
        /// <param name="v">The long value to wrap.</param>
        public static implicit operator TForeignKey(long v)         => new TForeignKey(v);

        /// <summary>
        /// Implicitly promotes a <see cref="TForeignKey"/> to a <see cref="TPrimaryKey"/>
        /// by copying its integer value.
        /// </summary>
        /// <param name="fk">The foreign-key field to promote.</param>
        public static implicit operator TPrimaryKey(TForeignKey fk) => new TPrimaryKey((TInt)fk);

        /// <summary>Returns <see langword="true"/> when both <see cref="TForeignKey"/> operands are equal (null-aware).</summary>
        public static bool operator ==(TForeignKey o1, TForeignKey o2) => EqualityOperatorHelper<TForeignKey>(o1, o2);

        /// <summary>Returns <see langword="true"/> when the <see cref="TForeignKey"/> operands are not equal.</summary>
        public static bool operator !=(TForeignKey o1, TForeignKey o2) => !(o1 == o2);

        /// <summary>Returns <see langword="true"/> when <paramref name="o1"/> equals the integer <paramref name="o2"/>.</summary>
        public static bool operator ==(TForeignKey o1, int o2)         => o1 == (TForeignKey)o2;

        /// <summary>Returns <see langword="true"/> when <paramref name="o1"/> does not equal the integer <paramref name="o2"/>.</summary>
        public static bool operator !=(TForeignKey o1, int o2)         => o1 != (TForeignKey)o2;

        /// <summary>Returns <see langword="true"/> when integer <paramref name="o1"/> equals <paramref name="o2"/>.</summary>
        public static bool operator ==(int o1, TForeignKey o2)         => (TForeignKey)o1 == o2;

        /// <summary>Returns <see langword="true"/> when integer <paramref name="o1"/> does not equal <paramref name="o2"/>.</summary>
        public static bool operator !=(int o1, TForeignKey o2)         => (TForeignKey)o1 != o2;

        /// <summary>
        /// Returns <see langword="true"/> when <paramref name="o1"/> (a primary key) equals
        /// <paramref name="o2"/> (a foreign key) after converting both to the same type.
        /// </summary>
        public static bool operator ==(TPrimaryKey o1, TForeignKey o2) => (TForeignKey)(TInt)o1 == o2;

        /// <summary>
        /// Returns <see langword="true"/> when <paramref name="o1"/> (a primary key) does not equal
        /// <paramref name="o2"/> (a foreign key).
        /// </summary>
        public static bool operator !=(TPrimaryKey o1, TForeignKey o2) => !((TForeignKey)(TInt)o1 == o2);

        /// <summary>
        /// Returns <see langword="true"/> when <paramref name="o1"/> (a foreign key) equals
        /// <paramref name="o2"/> (a primary key) after converting both to the same type.
        /// </summary>
        public static bool operator ==(TForeignKey o1, TPrimaryKey o2) => o1 == (TForeignKey)(TInt)o2;

        /// <summary>
        /// Returns <see langword="true"/> when <paramref name="o1"/> (a foreign key) does not equal
        /// <paramref name="o2"/> (a primary key).
        /// </summary>
        public static bool operator !=(TForeignKey o1, TPrimaryKey o2) => !(o1 == (TForeignKey)(TInt)o2);

        /// <summary>Returns <see langword="true"/> when <paramref name="o1"/> is greater than <paramref name="o2"/>.</summary>
        public static bool operator >(TForeignKey  o1, TForeignKey o2) => GTHelper<TForeignKey>(o1, o2);

        /// <summary>Returns <see langword="true"/> when <paramref name="o1"/> is less than <paramref name="o2"/>.</summary>
        public static bool operator <(TForeignKey  o1, TForeignKey o2) => LTHelper<TForeignKey>(o1, o2);

        /// <summary>Returns <see langword="true"/> when <paramref name="o1"/> is greater than or equal to <paramref name="o2"/>.</summary>
        public static bool operator >=(TForeignKey o1, TForeignKey o2) => o1 > o2 || o1 == o2;

        /// <summary>Returns <see langword="true"/> when <paramref name="o1"/> is less than or equal to <paramref name="o2"/>.</summary>
        public static bool operator <=(TForeignKey o1, TForeignKey o2) => o1 < o2 || o1 == o2;

        /// <summary>Returns the string token <c>"foreignkey"</c> used by the ORM schema system.</summary>
        public override string GetTypeDescription() => "foreignkey";

        /// <summary>Delegates to the base <see cref="TKey"/> equality implementation.</summary>
        public override bool   Equals(object obj)   => base.Equals(obj);

        /// <summary>Delegates to the base <see cref="TKey"/> hash-code implementation.</summary>
        public override int    GetHashCode()         => base.GetHashCode();
    }
}
