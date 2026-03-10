using System;
using System.Diagnostics;

namespace ActiveForge
{
    [Serializable]
    [DebuggerDisplay("Null={Null} Value={InnerValue}")]
    public class TInt64 : TField, IComparable
    {
        protected long InnerValue;
        protected long Value { get { CheckValidity(); return InnerValue; } set { InnerValue = value; } }

        public TInt64()          { InnerValue = 0; }
        public TInt64(long v)    { SetValue(v); }
        public TInt64(object v)  { SetValue(v); }

        public static implicit operator long(TInt64 t)  => t.InnerValue;
        public static implicit operator TInt64(long v)  => new TInt64(v);
        public static implicit operator TInt64(int v)   => new TInt64(v);

        public static bool operator ==(TInt64 o1, TInt64 o2) => EqualityOperatorHelper<TInt64>(o1, o2);
        public static bool operator !=(TInt64 o1, TInt64 o2) => !(o1 == o2);
        public static bool operator >(TInt64  o1, TInt64 o2) => GTHelper<TInt64>(o1, o2);
        public static bool operator <(TInt64  o1, TInt64 o2) => LTHelper<TInt64>(o1, o2);
        public static bool operator >=(TInt64 o1, TInt64 o2) => o1 > o2 || o1 == o2;
        public static bool operator <=(TInt64 o1, TInt64 o2) => o1 < o2 || o1 == o2;

        public override Type   GetUnderlyingType()  => typeof(long);
        public override string GetTypeDescription()  => "int64";
        public override object GetValue()            => InnerValue;

        public override void SetDerivedValue(object value)
        {
            if (value is TInt64 ti) InnerValue = ti.InnerValue;
            else                    InnerValue = Convert.ToInt64(value);
        }
        public void SetValue(long value) { base.SetValue(value); ConversionError = false; }
        public override void SetNull(bool isNull) { base.SetNull(isNull); if (isNull) InnerValue = 0; }
        public override string ToString() => InnerValue.ToString();
        public int CompareTo(object obj)
        {
            if (obj is TInt64 other) return InnerValue.CompareTo(other.InnerValue);
            return InnerValue.CompareTo(Convert.ToInt64(obj));
        }
        public override bool Equals(object obj) => EqualsHelper<TInt64, long>(obj);
        public override int  GetHashCode()      => IsNull() ? 0 : InnerValue.GetHashCode();
    }
}
