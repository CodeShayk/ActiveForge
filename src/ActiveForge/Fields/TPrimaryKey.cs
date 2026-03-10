using System;
using System.Diagnostics;

namespace ActiveForge
{
    /// <summary>
    /// ORM field that represents an auto-generated 32-bit integer primary key, mapping to an
    /// IDENTITY / SERIAL / AUTO_INCREMENT database column.
    /// <para>
    /// <see cref="TPrimaryKey"/> extends <see cref="TKey"/> with primary-key semantics:
    /// cross-type equality operators against <see cref="TForeignKey"/> and <c>int</c> allow
    /// natural join predicates, and increment/decrement operators support sequential key
    /// generation in code.
    /// </para>
    /// <para>
    /// The value is typically populated by the database after an INSERT; do not assign it
    /// manually unless you are implementing a custom key strategy.  Use the implicit
    /// <c>int</c> conversion or <see cref="TField.GetValue()"/> to read the generated key.
    /// </para>
    /// </summary>
    [Serializable]
    [DebuggerDisplay("Null={Null} Value={InnerValue}")]
    public class TPrimaryKey : TKey
    {
        /// <summary>Initialises a new null <see cref="TPrimaryKey"/> with value zero.</summary>
        public TPrimaryKey()            : base() { }

        /// <summary>Initialises a new <see cref="TPrimaryKey"/> with the specified integer <paramref name="value"/>.</summary>
        /// <param name="value">The primary-key integer value.</param>
        public TPrimaryKey(int value)   : base(value) { }

        /// <summary>
        /// Initialises a new <see cref="TPrimaryKey"/> from a <see cref="long"/>, checked-casting to <c>int</c>.
        /// </summary>
        /// <param name="value">The long value; throws <see cref="OverflowException"/> if it exceeds <see cref="int.MaxValue"/>.</param>
        public TPrimaryKey(long value)  : base(checked((int)value)) { }

        /// <summary>
        /// Initialises a new <see cref="TPrimaryKey"/> by converting <paramref name="v"/> via
        /// <see cref="TField.SetValue(object)"/>.
        /// </summary>
        /// <param name="v">Any value convertible to <c>int</c>.</param>
        public TPrimaryKey(object v)    : base(v) { }

        /// <summary>
        /// Initialises a new <see cref="TPrimaryKey"/> by copying the value from an existing <see cref="TInt"/>.
        /// </summary>
        /// <param name="value">The source integer field.</param>
        public TPrimaryKey(TInt value)  { CopyFrom(value); }

        /// <summary>Implicitly wraps an <c>int</c> value as a <see cref="TPrimaryKey"/>.</summary>
        /// <param name="v">The integer value to wrap.</param>
        public static implicit operator TPrimaryKey(int v)          => new TPrimaryKey(v);

        /// <summary>Implicitly wraps a <c>long</c> value as a <see cref="TPrimaryKey"/> (checked cast).</summary>
        /// <param name="v">The long value to wrap.</param>
        public static implicit operator TPrimaryKey(long v)         => new TPrimaryKey(v);

        /// <summary>
        /// Implicitly demotes a <see cref="TPrimaryKey"/> to a <see cref="TForeignKey"/>
        /// by copying its integer value, enabling natural assignment to foreign-key fields.
        /// </summary>
        /// <param name="pk">The primary-key field to demote.</param>
        public static implicit operator TForeignKey(TPrimaryKey pk) => new TForeignKey((TInt)pk);

        /// <summary>Returns <see langword="true"/> when both <see cref="TPrimaryKey"/> operands are equal (null-aware).</summary>
        public static bool operator ==(TPrimaryKey o1, TPrimaryKey o2) => EqualityOperatorHelper<TPrimaryKey>(o1, o2);

        /// <summary>Returns <see langword="true"/> when the <see cref="TPrimaryKey"/> operands are not equal.</summary>
        public static bool operator !=(TPrimaryKey o1, TPrimaryKey o2) => !(o1 == o2);

        /// <summary>Returns <see langword="true"/> when <paramref name="o1"/> equals the integer <paramref name="o2"/>.</summary>
        public static bool operator ==(TPrimaryKey o1, int o2)         => o1 == (TPrimaryKey)o2;

        /// <summary>Returns <see langword="true"/> when <paramref name="o1"/> does not equal the integer <paramref name="o2"/>.</summary>
        public static bool operator !=(TPrimaryKey o1, int o2)         => o1 != (TPrimaryKey)o2;

        /// <summary>Returns <see langword="true"/> when integer <paramref name="o1"/> equals <paramref name="o2"/>.</summary>
        public static bool operator ==(int o1, TPrimaryKey o2)         => (TPrimaryKey)o1 == o2;

        /// <summary>Returns <see langword="true"/> when integer <paramref name="o1"/> does not equal <paramref name="o2"/>.</summary>
        public static bool operator !=(int o1, TPrimaryKey o2)         => (TPrimaryKey)o1 != o2;

        /// <summary>Returns <see langword="true"/> when <paramref name="o1"/> is greater than <paramref name="o2"/>.</summary>
        public static bool operator >(TPrimaryKey o1, TPrimaryKey o2)  => GTHelper<TPrimaryKey>(o1, o2);

        /// <summary>Returns <see langword="true"/> when <paramref name="o1"/> is less than <paramref name="o2"/>.</summary>
        public static bool operator <(TPrimaryKey o1, TPrimaryKey o2)  => LTHelper<TPrimaryKey>(o1, o2);

        /// <summary>Returns <see langword="true"/> when <paramref name="o1"/> is greater than or equal to <paramref name="o2"/>.</summary>
        public static bool operator >=(TPrimaryKey o1, TPrimaryKey o2) => o1 > o2 || o1 == o2;

        /// <summary>Returns <see langword="true"/> when <paramref name="o1"/> is less than or equal to <paramref name="o2"/>.</summary>
        public static bool operator <=(TPrimaryKey o1, TPrimaryKey o2) => o1 < o2 || o1 == o2;

        /// <summary>Returns a new <see cref="TPrimaryKey"/> with its value incremented by one.</summary>
        /// <param name="pk">The primary key to increment.</param>
        public static TPrimaryKey operator ++(TPrimaryKey pk) => new TPrimaryKey(pk.InnerValue + 1);

        /// <summary>Returns a new <see cref="TPrimaryKey"/> with its value decremented by one.</summary>
        /// <param name="pk">The primary key to decrement.</param>
        public static TPrimaryKey operator --(TPrimaryKey pk) => new TPrimaryKey(pk.InnerValue - 1);

        /// <summary>Returns the string token <c>"primarykey"</c> used by the ORM schema system.</summary>
        public override string GetTypeDescription() => "primarykey";

        /// <summary>Delegates to the base <see cref="TKey"/> equality implementation.</summary>
        public override bool   Equals(object obj)   => base.Equals(obj);

        /// <summary>Returns the hash code of the inner integer value.</summary>
        public override int    GetHashCode()         => InnerValue.GetHashCode();
    }
}
