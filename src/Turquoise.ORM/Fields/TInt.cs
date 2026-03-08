using System;
using System.Diagnostics;
using System.Globalization;

namespace Turquoise.ORM
{
    /// <summary>32-bit integer database field.</summary>
    [Serializable]
    [DebuggerDisplay("Null={Null} Value={InnerValue}")]
    public class TInt : TField, IComparable, IFormattable
    {
        protected int InnerValue;

        protected int Value
        {
            get { CheckValidity(); return InnerValue; }
            set { InnerValue = value; }
        }

        public TInt()           { InnerValue = 0; }
        public TInt(int value)  { SetValue(value); }
        public TInt(object v)   { SetValue(v); }

        public static implicit operator int(TInt t)   => t.InnerValue;
        public static implicit operator TInt(int v)   => new TInt(v);

        public static bool operator ==(TInt o1, TInt o2)  => EqualityOperatorHelper<TInt>(o1, o2);
        public static bool operator !=(TInt o1, TInt o2)  => !(o1 == o2);
        public static bool operator ==(TInt o1, int  o2)  => o1 == (TInt)o2;
        public static bool operator !=(TInt o1, int  o2)  => o1 != (TInt)o2;
        public static bool operator ==(int  o1, TInt o2)  => (TInt)o1 == o2;
        public static bool operator !=(int  o1, TInt o2)  => (TInt)o1 != o2;
        public static bool operator >(TInt  o1, TInt o2)  => GTHelper<TInt>(o1, o2);
        public static bool operator <(TInt  o1, TInt o2)  => LTHelper<TInt>(o1, o2);
        public static bool operator >=(TInt o1, TInt o2)  => o1 > o2 || o1 == o2;
        public static bool operator <=(TInt o1, TInt o2)  => o1 < o2 || o1 == o2;
        public static bool operator >(TInt  o1, int  o2)  => o1 > (TInt)o2;
        public static bool operator <(TInt  o1, int  o2)  => o1 < (TInt)o2;
        public static bool operator >=(TInt o1, int  o2)  => o1 >= (TInt)o2;
        public static bool operator <=(TInt o1, int  o2)  => o1 <= (TInt)o2;

        public static TInt operator ++(TInt i) => new TInt(i.InnerValue + 1);
        public static TInt operator --(TInt i) => new TInt(i.InnerValue - 1);

        public static TInt MaxValue = int.MaxValue;
        public static TInt MinValue = int.MinValue;

        public override Type   GetUnderlyingType()  => typeof(int);
        public override string GetTypeDescription()  => "int";
        public override object GetValue()            => InnerValue;

        public override void SetDerivedValue(object value)
        {
            if (value is TInt ti) InnerValue = ti.InnerValue;
            else                  InnerValue = Convert.ToInt32(value);
        }

        public void SetValue(int value) { base.SetValue(value); ConversionError = false; }

        public override void SetNull(bool isNull)
        {
            base.SetNull(isNull);
            if (isNull) InnerValue = 0;
        }

        public override string ToString()                                => InnerValue.ToString();
        public string          ToString(IFormatProvider p)               => InnerValue.ToString(p);
        public string          ToString(string fmt, IFormatProvider p)   => InnerValue.ToString(fmt, p);
        public string          ToString(string fmt)                      => InnerValue.ToString(fmt);

        public int CompareTo(object obj)
        {
            if (obj is TInt other)
            {
                if (IsNull() && other.IsNull()) return 0;
                if (IsNull()) return -1;
                if (other.IsNull()) return 1;
                return InnerValue.CompareTo(other.InnerValue);
            }
            return InnerValue.CompareTo(Convert.ToInt32(obj));
        }

        public static TInt Parse(string s)                                     => int.Parse(s);
        public static TInt Parse(string s, NumberStyles style)                 => int.Parse(s, style);
        public static TInt Parse(string s, IFormatProvider p)                  => int.Parse(s, p);
        public static TInt Parse(string s, NumberStyles style, IFormatProvider p) => int.Parse(s, style, p);

        public override bool Equals(object obj) => EqualsHelper<TInt, int>(obj);
        public override int  GetHashCode()      => IsNull() ? 0 : InnerValue.GetHashCode();
    }
}
