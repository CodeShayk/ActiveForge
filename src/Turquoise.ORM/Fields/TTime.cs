using System;
using System.Diagnostics;

namespace Turquoise.ORM
{
    [Serializable]
    [DebuggerDisplay("Null={Null} Value={InnerValue}")]
    public class TTime : TField, IComparable
    {
        protected TimeSpan InnerValue;
        protected TimeSpan Value { get { CheckValidity(); return InnerValue; } set { InnerValue = value; } }

        public TTime()              { InnerValue = TimeSpan.Zero; }
        public TTime(TimeSpan v)    { SetValue(v); }
        public TTime(object v)      { SetValue(v); }

        public static implicit operator TimeSpan(TTime t) => t.InnerValue;
        public static implicit operator TTime(TimeSpan v) => new TTime(v);

        public static bool operator ==(TTime o1, TTime o2) => EqualityOperatorHelper<TTime>(o1, o2);
        public static bool operator !=(TTime o1, TTime o2) => !(o1 == o2);

        public override Type   GetUnderlyingType()  => typeof(TimeSpan);
        public override string GetTypeDescription()  => "time";
        public override object GetValue()            => InnerValue;

        public override void SetDerivedValue(object value)
        {
            if (value is TTime tt)        InnerValue = tt.InnerValue;
            else if (value is TimeSpan ts) InnerValue = ts;
            else if (value is DateTime dt) InnerValue = dt.TimeOfDay;
            else                           InnerValue = TimeSpan.Parse(value.ToString());
        }
        public void SetValue(TimeSpan value) { base.SetValue(value); ConversionError = false; }
        public override void SetNull(bool isNull) { base.SetNull(isNull); if (isNull) InnerValue = TimeSpan.Zero; }
        public override string ToString() => InnerValue.ToString();
        public int CompareTo(object obj)
        {
            if (obj is TTime other) return InnerValue.CompareTo(other.InnerValue);
            return InnerValue.CompareTo(TimeSpan.Parse(obj.ToString()));
        }
        public override bool Equals(object obj) => EqualsHelper<TTime, TimeSpan>(obj);
        public override int  GetHashCode()      => IsNull() ? 0 : InnerValue.GetHashCode();
    }
}
