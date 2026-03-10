using System;
using System.Diagnostics;

namespace ActiveForge
{
    [Serializable]
    [DebuggerDisplay("Null={Null} Value={InnerValue}")]
    public class TUInt16 : TField, IComparable
    {
        protected ushort InnerValue;
        protected ushort Value { get { CheckValidity(); return InnerValue; } set { InnerValue = value; } }

        public TUInt16()            { InnerValue = 0; }
        public TUInt16(ushort v)    { SetValue(v); }
        public TUInt16(object v)    { SetValue(v); }

        public static implicit operator ushort(TUInt16 t) => t.InnerValue;
        public static implicit operator TUInt16(ushort v) => new TUInt16(v);

        public static bool operator ==(TUInt16 o1, TUInt16 o2) => EqualityOperatorHelper<TUInt16>(o1, o2);
        public static bool operator !=(TUInt16 o1, TUInt16 o2) => !(o1 == o2);

        public override Type   GetUnderlyingType()  => typeof(ushort);
        public override string GetTypeDescription()  => "uint16";
        public override object GetValue()            => InnerValue;

        public override void SetDerivedValue(object value)
        {
            if (value is TUInt16 tu) InnerValue = tu.InnerValue;
            else                     InnerValue = Convert.ToUInt16(value);
        }
        public void SetValue(ushort value) { base.SetValue(value); ConversionError = false; }
        public override void SetNull(bool isNull) { base.SetNull(isNull); if (isNull) InnerValue = 0; }
        public override string ToString() => InnerValue.ToString();
        public int CompareTo(object obj)
        {
            if (obj is TUInt16 other) return InnerValue.CompareTo(other.InnerValue);
            return InnerValue.CompareTo(Convert.ToUInt16(obj));
        }
        public override bool Equals(object obj) => EqualsHelper<TUInt16, ushort>(obj);
        public override int  GetHashCode()      => IsNull() ? 0 : InnerValue.GetHashCode();
    }
}
