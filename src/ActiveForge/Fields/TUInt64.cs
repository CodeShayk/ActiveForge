using System;
using System.Diagnostics;

namespace ActiveForge
{
    [Serializable]
    [DebuggerDisplay("Null={Null} Value={InnerValue}")]
    public class TUInt64 : TField, IComparable
    {
        protected ulong InnerValue;
        protected ulong Value { get { CheckValidity(); return InnerValue; } set { InnerValue = value; } }

        public TUInt64()           { InnerValue = 0; }
        public TUInt64(ulong v)    { SetValue(v); }
        public TUInt64(object v)   { SetValue(v); }

        public static implicit operator ulong(TUInt64 t) => t.InnerValue;
        public static implicit operator TUInt64(ulong v) => new TUInt64(v);

        public static bool operator ==(TUInt64 o1, TUInt64 o2) => EqualityOperatorHelper<TUInt64>(o1, o2);
        public static bool operator !=(TUInt64 o1, TUInt64 o2) => !(o1 == o2);

        public override Type   GetUnderlyingType()  => typeof(ulong);
        public override string GetTypeDescription()  => "uint64";
        public override object GetValue()            => InnerValue;

        public override void SetDerivedValue(object value)
        {
            if (value is TUInt64 tu) InnerValue = tu.InnerValue;
            else                     InnerValue = Convert.ToUInt64(value);
        }
        public void SetValue(ulong value) { base.SetValue(value); ConversionError = false; }
        public override void SetNull(bool isNull) { base.SetNull(isNull); if (isNull) InnerValue = 0; }
        public override string ToString() => InnerValue.ToString();
        public int CompareTo(object obj)
        {
            if (obj is TUInt64 other) return InnerValue.CompareTo(other.InnerValue);
            return InnerValue.CompareTo(Convert.ToUInt64(obj));
        }
        public override bool Equals(object obj) => EqualsHelper<TUInt64, ulong>(obj);
        public override int  GetHashCode()      => IsNull() ? 0 : InnerValue.GetHashCode();
    }
}
