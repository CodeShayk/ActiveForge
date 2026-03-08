using System;
using System.Diagnostics;

namespace Turquoise.ORM
{
    [Serializable]
    [DebuggerDisplay("Null={Null} Value={InnerValue}")]
    public class TUtcDate : TDateTimeBase
    {
        public TUtcDate()           { }
        public TUtcDate(DateTime v) { SetValue(v); }
        public TUtcDate(object v)   { SetValue(v); }

        public static implicit operator DateTime(TUtcDate t) => t.InnerValue;
        public static implicit operator TUtcDate(DateTime v) => new TUtcDate(v);

        public static bool operator ==(TUtcDate o1, TUtcDate o2) => EqualityOperatorHelper<TUtcDate>(o1, o2);
        public static bool operator !=(TUtcDate o1, TUtcDate o2) => !(o1 == o2);

        public override Type   GetUnderlyingType()  => typeof(DateTime);
        public override string GetTypeDescription()  => "utcdate";

        public override bool Equals(object obj) => EqualsHelper<TUtcDate, DateTime>(obj);
        public override int  GetHashCode()      => IsNull() ? 0 : InnerValue.GetHashCode();
    }
}
