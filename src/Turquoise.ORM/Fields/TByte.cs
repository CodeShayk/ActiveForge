using System;
using System.Diagnostics;

namespace Turquoise.ORM
{
    [Serializable]
    [DebuggerDisplay("Null={Null} Value={InnerValue}")]
    public class TByte : TField, IComparable
    {
        protected byte InnerValue;
        protected byte Value { get { CheckValidity(); return InnerValue; } set { InnerValue = value; } }

        public TByte()          { InnerValue = 0; }
        public TByte(byte v)    { SetValue(v); }
        public TByte(object v)  { SetValue(v); }

        public static implicit operator byte(TByte t) => t.InnerValue;
        public static implicit operator TByte(byte v) => new TByte(v);

        public static bool operator ==(TByte o1, TByte o2) => EqualityOperatorHelper<TByte>(o1, o2);
        public static bool operator !=(TByte o1, TByte o2) => !(o1 == o2);

        public override Type   GetUnderlyingType()  => typeof(byte);
        public override string GetTypeDescription()  => "byte";
        public override object GetValue()            => InnerValue;

        public override void SetDerivedValue(object value)
        {
            if (value is TByte tb) InnerValue = tb.InnerValue;
            else                   InnerValue = Convert.ToByte(value);
        }
        public void SetValue(byte value) { base.SetValue(value); ConversionError = false; }
        public override void SetNull(bool isNull) { base.SetNull(isNull); if (isNull) InnerValue = 0; }
        public override string ToString() => InnerValue.ToString();
        public int CompareTo(object obj)
        {
            if (obj is TByte other) return InnerValue.CompareTo(other.InnerValue);
            return InnerValue.CompareTo(Convert.ToByte(obj));
        }
        public override bool Equals(object obj) => EqualsHelper<TByte, byte>(obj);
        public override int  GetHashCode()      => IsNull() ? 0 : InnerValue.GetHashCode();
    }
}
