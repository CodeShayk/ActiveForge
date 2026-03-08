using System;
using System.Diagnostics;

namespace Turquoise.ORM
{
    /// <summary>Base for primary and foreign key integer fields.</summary>
    [Serializable]
    [DebuggerDisplay("Null={Null} Value={InnerValue}")]
    public class TKey : TInt
    {
        public TKey()           : base() { }
        public TKey(int value)  : base(value) { }
        public TKey(object v)   : base(v) { }

        public override Type GetUnderlyingType() => GetType();

        public static bool operator ==(TKey k1, int k2)   => k1 == (TKey)k2;
        public static bool operator !=(TKey k1, int k2)   => k1 != (TKey)k2;
        public static bool operator ==(TKey k1, TKey k2)  => EqualityOperatorHelper<TKey>(k1, k2);
        public static bool operator !=(TKey k1, TKey k2)  => !(k1 == k2);

        public override bool Equals(object obj) => EqualsHelper<TKey, int>(obj);
        public override int  GetHashCode()      => base.GetHashCode();
    }
}
