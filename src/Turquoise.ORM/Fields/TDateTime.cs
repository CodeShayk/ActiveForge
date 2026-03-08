using System;
using System.Diagnostics;

namespace Turquoise.ORM
{
    /// <summary>DateTime database field (local server time).</summary>
    [Serializable]
    [DebuggerDisplay("Null={Null} Value={InnerValue}")]
    public class TDateTime : TDateTimeBase
    {
        public TDateTime()           { }
        public TDateTime(DateTime v) { SetValue(v); }
        public TDateTime(object v)   { SetValue(v); }

        public static implicit operator DateTime(TDateTime t) => t.InnerValue;
        public static implicit operator TDateTime(DateTime v) => new TDateTime(v);

        public static bool operator ==(TDateTime o1, TDateTime o2) => EqualityOperatorHelper<TDateTime>(o1, o2);
        public static bool operator !=(TDateTime o1, TDateTime o2) => !(o1 == o2);
        public static bool operator >(TDateTime  o1, TDateTime o2) => GTHelper<TDateTime>(o1, o2);
        public static bool operator <(TDateTime  o1, TDateTime o2) => LTHelper<TDateTime>(o1, o2);
        public static bool operator >=(TDateTime o1, TDateTime o2) => o1 > o2 || o1 == o2;
        public static bool operator <=(TDateTime o1, TDateTime o2) => o1 < o2 || o1 == o2;

        public override Type   GetUnderlyingType()  => typeof(DateTime);
        public override string GetTypeDescription()  => "datetime";

        public override bool Equals(object obj) => EqualsHelper<TDateTime, DateTime>(obj);
        public override int  GetHashCode()      => IsNull() ? 0 : InnerValue.GetHashCode();
    }
}
