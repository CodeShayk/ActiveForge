using System;
using System.Diagnostics;

namespace Turquoise.ORM
{
    [Serializable]
    [DebuggerDisplay("Null={Null} Value={InnerValue}")]
    public class TDouble : TField, IComparable, IFormattable
    {
        protected double InnerValue;
        protected double Value { get { CheckValidity(); return InnerValue; } set { InnerValue = value; } }

        public TDouble()           { InnerValue = 0; }
        public TDouble(double v)   { SetValue(v); }
        public TDouble(object v)   { SetValue(v); }

        public static implicit operator double(TDouble t) => t.InnerValue;
        public static implicit operator TDouble(double v) => new TDouble(v);

        public static bool operator ==(TDouble o1, TDouble o2) => EqualityOperatorHelper<TDouble>(o1, o2);
        public static bool operator !=(TDouble o1, TDouble o2) => !(o1 == o2);
        public static bool operator >(TDouble  o1, TDouble o2) => GTHelper<TDouble>(o1, o2);
        public static bool operator <(TDouble  o1, TDouble o2) => LTHelper<TDouble>(o1, o2);
        public static bool operator >=(TDouble o1, TDouble o2) => o1 > o2 || o1 == o2;
        public static bool operator <=(TDouble o1, TDouble o2) => o1 < o2 || o1 == o2;

        public override Type   GetUnderlyingType()  => typeof(double);
        public override string GetTypeDescription()  => "double";
        public override object GetValue()            => InnerValue;

        public override void SetDerivedValue(object value)
        {
            if (value is TDouble td) InnerValue = td.InnerValue;
            else                     InnerValue = Convert.ToDouble(value);
        }
        public void SetValue(double value) { base.SetValue(value); ConversionError = false; }
        public override void SetNull(bool isNull) { base.SetNull(isNull); if (isNull) InnerValue = 0; }
        public override string ToString()             => InnerValue.ToString();
        public string ToString(string fmt, IFormatProvider p) => InnerValue.ToString(fmt, p);
        public int CompareTo(object obj)
        {
            if (obj is TDouble other) return InnerValue.CompareTo(other.InnerValue);
            return InnerValue.CompareTo(Convert.ToDouble(obj));
        }
        public override bool Equals(object obj) => EqualsHelper<TDouble, double>(obj);
        public override int  GetHashCode()      => IsNull() ? 0 : InnerValue.GetHashCode();
    }
}
