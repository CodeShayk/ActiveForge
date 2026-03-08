using System;
using System.Diagnostics;

namespace Turquoise.ORM
{
    /// <summary>UTC DateTime database field. Stored as UTC, surfaced as UTC.</summary>
    [Serializable]
    [DebuggerDisplay("Null={Null} Value={InnerValue}")]
    public class TUtcDateTime : TDateTimeBase
    {
        public TUtcDateTime()           { }
        public TUtcDateTime(DateTime v) { SetValue(v.Kind == DateTimeKind.Utc ? v : v.ToUniversalTime()); }
        public TUtcDateTime(object v)   { SetValue(v); }

        public static implicit operator DateTime(TUtcDateTime t)    => t.InnerValue;
        public static implicit operator TUtcDateTime(DateTime v)    => new TUtcDateTime(v);

        public static bool operator ==(TUtcDateTime o1, TUtcDateTime o2) => EqualityOperatorHelper<TUtcDateTime>(o1, o2);
        public static bool operator !=(TUtcDateTime o1, TUtcDateTime o2) => !(o1 == o2);
        public static bool operator >(TUtcDateTime  o1, TUtcDateTime o2) => GTHelper<TUtcDateTime>(o1, o2);
        public static bool operator <(TUtcDateTime  o1, TUtcDateTime o2) => LTHelper<TUtcDateTime>(o1, o2);
        public static bool operator >=(TUtcDateTime o1, TUtcDateTime o2) => o1 > o2 || o1 == o2;
        public static bool operator <=(TUtcDateTime o1, TUtcDateTime o2) => o1 < o2 || o1 == o2;

        public override Type   GetUnderlyingType()  => typeof(DateTime);
        public override string GetTypeDescription()  => "utcdatetime";

        public override bool Equals(object obj) => EqualsHelper<TUtcDateTime, DateTime>(obj);
        public override int  GetHashCode()      => IsNull() ? 0 : InnerValue.GetHashCode();
    }
}
