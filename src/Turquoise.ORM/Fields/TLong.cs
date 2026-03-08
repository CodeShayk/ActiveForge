using System;
using System.Diagnostics;

namespace Turquoise.ORM
{
    // TLong is an alias for TInt64 kept for semantic clarity.
    [Serializable]
    [DebuggerDisplay("Null={Null} Value={InnerValue}")]
    public class TLong : TInt64
    {
        public TLong()          : base() { }
        public TLong(long v)    : base(v) { }
        public TLong(object v)  : base(v) { }

        public static implicit operator long(TLong t)  => t.InnerValue;
        public static implicit operator TLong(long v)  => new TLong(v);

        public static bool operator ==(TLong o1, TLong o2) => EqualityOperatorHelper<TLong>(o1, o2);
        public static bool operator !=(TLong o1, TLong o2) => !(o1 == o2);
        public override string GetTypeDescription()         => "long";
        public override bool   Equals(object obj)           => base.Equals(obj);
        public override int    GetHashCode()                => base.GetHashCode();
    }
}
