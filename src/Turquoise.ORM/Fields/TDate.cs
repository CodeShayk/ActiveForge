using System;
using System.Diagnostics;

namespace Turquoise.ORM
{
    /// <summary>Date-only database field (time component is ignored).</summary>
    [Serializable]
    [DebuggerDisplay("Null={Null} Value={InnerValue}")]
    public class TDate : TDateTimeBase
    {
        public TDate()           { }
        public TDate(DateTime v) { SetValue(v.Date); }
        public TDate(object v)   { SetValue(v); }

        public static implicit operator DateTime(TDate t) => t.InnerValue;
        public static implicit operator TDate(DateTime v) => new TDate(v);

        public static bool operator ==(TDate o1, TDate o2) => EqualityOperatorHelper<TDate>(o1, o2);
        public static bool operator !=(TDate o1, TDate o2) => !(o1 == o2);
        public static bool operator >(TDate  o1, TDate o2) => GTHelper<TDate>(o1, o2);
        public static bool operator <(TDate  o1, TDate o2) => LTHelper<TDate>(o1, o2);
        public static bool operator >=(TDate o1, TDate o2) => o1 > o2 || o1 == o2;
        public static bool operator <=(TDate o1, TDate o2) => o1 < o2 || o1 == o2;

        public override Type   GetUnderlyingType()  => typeof(DateTime);
        public override string GetTypeDescription()  => "date";

        public override void SetDerivedValue(object value)
        {
            base.SetDerivedValue(value);
            InnerValue = InnerValue.Date;  // strip time
        }

        public override bool Equals(object obj) => EqualsHelper<TDate, DateTime>(obj);
        public override int  GetHashCode()      => IsNull() ? 0 : InnerValue.Date.GetHashCode();
    }
}
