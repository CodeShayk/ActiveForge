using System;
using System.Diagnostics;

namespace ActiveForge
{
    /// <summary>
    /// ORM field that wraps the CLR <see cref="long"/> (64-bit signed integer) type,
    /// mapping to a BIGINT database column.
    /// <para>
    /// <see cref="TLong"/> is a semantic alias for <see cref="TInt64"/>, kept for
    /// readability in domain models that prefer the name "long" over the SQL-derived
    /// "int64".  Both classes are fully interchangeable; use whichever name suits the
    /// domain better.
    /// </para>
    /// <para>
    /// Use <see cref="TField.SetValue(object)"/> or the typed <c>long</c> implicit
    /// conversion inherited from <see cref="TInt64"/> to assign a value, and
    /// <see cref="TField.GetValue()"/> or implicit cast to <c>long</c> to read it back.
    /// </para>
    /// </summary>
    [Serializable]
    [DebuggerDisplay("Null={Null} Value={InnerValue}")]
    public class TLong : TInt64
    {
        /// <summary>Initialises a new <see cref="TLong"/> with a value of zero and null state unset.</summary>
        public TLong()          : base() { }

        /// <summary>Initialises a new <see cref="TLong"/> with the specified <paramref name="v"/> value.</summary>
        /// <param name="v">The initial long value.</param>
        public TLong(long v)    : base(v) { }

        /// <summary>
        /// Initialises a new <see cref="TLong"/> by converting <paramref name="v"/> using
        /// <see cref="TField.SetValue(object)"/>.
        /// </summary>
        /// <param name="v">Any value that can be converted to <see cref="long"/>.</param>
        public TLong(object v)  : base(v) { }

        /// <summary>Implicitly converts a <see cref="TLong"/> to its underlying <see cref="long"/> value.</summary>
        /// <param name="t">The field instance to convert.</param>
        public static implicit operator long(TLong t)  => t.InnerValue;

        /// <summary>Implicitly wraps a <see cref="long"/> literal or variable in a new <see cref="TLong"/>.</summary>
        /// <param name="v">The long value to wrap.</param>
        public static implicit operator TLong(long v)  => new TLong(v);

        /// <summary>Returns <see langword="true"/> when both operands are equal (null-aware).</summary>
        public static bool operator ==(TLong o1, TLong o2) => EqualityOperatorHelper<TLong>(o1, o2);

        /// <summary>Returns <see langword="true"/> when the operands are not equal.</summary>
        public static bool operator !=(TLong o1, TLong o2) => !(o1 == o2);

        /// <summary>Returns the string token <c>"long"</c> used by the ORM schema system.</summary>
        public override string GetTypeDescription()         => "long";

        /// <summary>Delegates to the base <see cref="TInt64"/> equality implementation.</summary>
        public override bool   Equals(object obj)           => base.Equals(obj);

        /// <summary>Delegates to the base <see cref="TInt64"/> hash-code implementation.</summary>
        public override int    GetHashCode()                => base.GetHashCode();
    }
}
