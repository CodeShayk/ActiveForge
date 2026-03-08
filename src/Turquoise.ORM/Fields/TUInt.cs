using System;
using System.Diagnostics;

namespace Turquoise.ORM
{
    [Serializable]
    [DebuggerDisplay("Null={Null} Value={InnerValue}")]
    public class TUInt : TField, IComparable
    {
        protected uint InnerValue;
        protected uint Value { get { CheckValidity(); return InnerValue; } set { InnerValue = value; } }

        public TUInt()           { InnerValue = 0; }
        public TUInt(uint v)     { SetValue(v); }
        public TUInt(object v)   { SetValue(v); }

        public static implicit operator uint(TUInt t) => t.InnerValue;
        public static implicit operator TUInt(uint v) => new TUInt(v);

        public static bool operator ==(TUInt o1, TUInt o2) => EqualityOperatorHelper<TUInt>(o1, o2);
        public static bool operator !=(TUInt o1, TUInt o2) => !(o1 == o2);

        public override Type   GetUnderlyingType()  => typeof(uint);
        public override string GetTypeDescription()  => "uint";
        public override object GetValue()            => InnerValue;

        public override void SetDerivedValue(object value)
        {
            if (value is TUInt tu) InnerValue = tu.InnerValue;
            else                   InnerValue = Convert.ToUInt32(value);
        }
        public void SetValue(uint value) { base.SetValue(value); ConversionError = false; }
        public override void SetNull(bool isNull) { base.SetNull(isNull); if (isNull) InnerValue = 0; }
        public override string ToString() => InnerValue.ToString();
        public int CompareTo(object obj)
        {
            if (obj is TUInt other) return InnerValue.CompareTo(other.InnerValue);
            return InnerValue.CompareTo(Convert.ToUInt32(obj));
        }
        public override bool Equals(object obj) => EqualsHelper<TUInt, uint>(obj);
        public override int  GetHashCode()      => IsNull() ? 0 : InnerValue.GetHashCode();
    }
}
