using System;
using System.Diagnostics;

namespace ActiveForge
{
    [Serializable]
    [DebuggerDisplay("Null={Null} Value={InnerValue}")]
    public class TDecimal : TField, IComparable, IFormattable
    {
        protected decimal InnerValue;
        protected decimal Value { get { CheckValidity(); return InnerValue; } set { InnerValue = value; } }

        public TDecimal()             { InnerValue = 0; }
        public TDecimal(decimal v)    { SetValue(v); }
        public TDecimal(object v)     { SetValue(v); }

        public static implicit operator decimal(TDecimal t) => t.InnerValue;
        public static implicit operator TDecimal(decimal v) => new TDecimal(v);

        public static bool operator ==(TDecimal o1, TDecimal o2) => EqualityOperatorHelper<TDecimal>(o1, o2);
        public static bool operator !=(TDecimal o1, TDecimal o2) => !(o1 == o2);
        public static bool operator >(TDecimal  o1, TDecimal o2) => GTHelper<TDecimal>(o1, o2);
        public static bool operator <(TDecimal  o1, TDecimal o2) => LTHelper<TDecimal>(o1, o2);
        public static bool operator >=(TDecimal o1, TDecimal o2) => o1 > o2 || o1 == o2;
        public static bool operator <=(TDecimal o1, TDecimal o2) => o1 < o2 || o1 == o2;

        public override Type   GetUnderlyingType()  => typeof(decimal);
        public override string GetTypeDescription()  => "decimal";
        public override object GetValue()            => InnerValue;

        public override void SetDerivedValue(object value)
        {
            if (value is TDecimal td) InnerValue = td.InnerValue;
            else                      InnerValue = Convert.ToDecimal(value);
        }

        public void SetValue(decimal value) { base.SetValue(value); ConversionError = false; }

        public override void SetNull(bool isNull) { base.SetNull(isNull); if (isNull) InnerValue = 0; }

        public override string ToString()                              => InnerValue.ToString();
        public string ToString(string fmt, IFormatProvider p)          => InnerValue.ToString(fmt, p);
        public string ToString(string fmt)                             => InnerValue.ToString(fmt);
        public int CompareTo(object obj)
        {
            if (obj is TDecimal other) return InnerValue.CompareTo(other.InnerValue);
            return InnerValue.CompareTo(Convert.ToDecimal(obj));
        }
        public override bool Equals(object obj) => EqualsHelper<TDecimal, decimal>(obj);
        public override int  GetHashCode()      => IsNull() ? 0 : InnerValue.GetHashCode();
    }
}
