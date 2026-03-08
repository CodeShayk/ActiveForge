using System;
using System.Diagnostics;

namespace Turquoise.ORM
{
    /// <summary>Local DateTime field - converted to local time on read.</summary>
    [Serializable]
    [DebuggerDisplay("Null={Null} Value={InnerValue}")]
    public class TLocalDateTime : TDateTimeBase
    {
        public TLocalDateTime()           { }
        public TLocalDateTime(DateTime v) { SetValue(v); }
        public TLocalDateTime(object v)   { SetValue(v); }

        public static implicit operator DateTime(TLocalDateTime t) => t.InnerValue;
        public static implicit operator TLocalDateTime(DateTime v) => new TLocalDateTime(v);

        public static bool operator ==(TLocalDateTime o1, TLocalDateTime o2) => EqualityOperatorHelper<TLocalDateTime>(o1, o2);
        public static bool operator !=(TLocalDateTime o1, TLocalDateTime o2) => !(o1 == o2);

        public override Type   GetUnderlyingType()  => typeof(DateTime);
        public override string GetTypeDescription()  => "localdatetime";

        public override bool Equals(object obj) => EqualsHelper<TLocalDateTime, DateTime>(obj);
        public override int  GetHashCode()      => IsNull() ? 0 : InnerValue.GetHashCode();
    }
}
