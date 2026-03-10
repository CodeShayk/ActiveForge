using System;
using System.Diagnostics;

namespace ActiveForge
{
    [Serializable]
    [DebuggerDisplay("Null={Null} Value={InnerValue}")]
    public class TInt16 : TField, IComparable
    {
        protected short InnerValue;
        protected short Value { get { CheckValidity(); return InnerValue; } set { InnerValue = value; } }

        public TInt16()           { InnerValue = 0; }
        public TInt16(short v)    { SetValue(v); }
        public TInt16(object v)   { SetValue(v); }

        public static implicit operator short(TInt16 t) => t.InnerValue;
        public static implicit operator TInt16(short v) => new TInt16(v);

        public static bool operator ==(TInt16 o1, TInt16 o2) => EqualityOperatorHelper<TInt16>(o1, o2);
        public static bool operator !=(TInt16 o1, TInt16 o2) => !(o1 == o2);

        public override Type   GetUnderlyingType()  => typeof(short);
        public override string GetTypeDescription()  => "int16";
        public override object GetValue()            => InnerValue;

        public override void SetDerivedValue(object value)
        {
            if (value is TInt16 ti) InnerValue = ti.InnerValue;
            else                    InnerValue = Convert.ToInt16(value);
        }
        public void SetValue(short value) { base.SetValue(value); ConversionError = false; }
        public override void SetNull(bool isNull) { base.SetNull(isNull); if (isNull) InnerValue = 0; }
        public override string ToString() => InnerValue.ToString();
        public int CompareTo(object obj)
        {
            if (obj is TInt16 other) return InnerValue.CompareTo(other.InnerValue);
            return InnerValue.CompareTo(Convert.ToInt16(obj));
        }
        public override bool Equals(object obj) => EqualsHelper<TInt16, short>(obj);
        public override int  GetHashCode()      => IsNull() ? 0 : InnerValue.GetHashCode();
    }
}
