using System;
using System.Diagnostics;

namespace ActiveForge
{
    [Serializable]
    [DebuggerDisplay("Null={Null} Value={InnerValue}")]
    public class TSByte : TField, IComparable
    {
        protected sbyte InnerValue;
        protected sbyte Value { get { CheckValidity(); return InnerValue; } set { InnerValue = value; } }

        public TSByte()           { InnerValue = 0; }
        public TSByte(sbyte v)    { SetValue(v); }
        public TSByte(object v)   { SetValue(v); }

        public static implicit operator sbyte(TSByte t) => t.InnerValue;
        public static implicit operator TSByte(sbyte v) => new TSByte(v);

        public static bool operator ==(TSByte o1, TSByte o2) => EqualityOperatorHelper<TSByte>(o1, o2);
        public static bool operator !=(TSByte o1, TSByte o2) => !(o1 == o2);

        public override Type   GetUnderlyingType()  => typeof(sbyte);
        public override string GetTypeDescription()  => "sbyte";
        public override object GetValue()            => InnerValue;

        public override void SetDerivedValue(object value)
        {
            if (value is TSByte ts) InnerValue = ts.InnerValue;
            else                    InnerValue = Convert.ToSByte(value);
        }
        public void SetValue(sbyte value) { base.SetValue(value); ConversionError = false; }
        public override void SetNull(bool isNull) { base.SetNull(isNull); if (isNull) InnerValue = 0; }
        public override string ToString() => InnerValue.ToString();
        public int CompareTo(object obj)
        {
            if (obj is TSByte other) return InnerValue.CompareTo(other.InnerValue);
            return InnerValue.CompareTo(Convert.ToSByte(obj));
        }
        public override bool Equals(object obj) => EqualsHelper<TSByte, sbyte>(obj);
        public override int  GetHashCode()      => IsNull() ? 0 : InnerValue.GetHashCode();
    }
}
