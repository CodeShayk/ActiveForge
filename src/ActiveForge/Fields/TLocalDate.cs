using System;
using System.Diagnostics;

namespace ActiveForge
{
    [Serializable]
    [DebuggerDisplay("Null={Null} Value={InnerValue}")]
    public class TLocalDate : TDateTimeBase
    {
        public TLocalDate()           { }
        public TLocalDate(DateTime v) { SetValue(v); }
        public TLocalDate(object v)   { SetValue(v); }

        public static implicit operator DateTime(TLocalDate t) => t.InnerValue;
        public static implicit operator TLocalDate(DateTime v) => new TLocalDate(v);

        public static bool operator ==(TLocalDate o1, TLocalDate o2) => EqualityOperatorHelper<TLocalDate>(o1, o2);
        public static bool operator !=(TLocalDate o1, TLocalDate o2) => !(o1 == o2);

        public override Type   GetUnderlyingType()  => typeof(DateTime);
        public override string GetTypeDescription()  => "localdate";

        public override bool Equals(object obj) => EqualsHelper<TLocalDate, DateTime>(obj);
        public override int  GetHashCode()      => IsNull() ? 0 : InnerValue.GetHashCode();
    }
}
