using System;
using System.Diagnostics;

namespace ActiveForge
{
    [Serializable]
    [DebuggerDisplay("Null={Null} Value={InnerValue}")]
    public class TFloat : TField, IComparable
    {
        protected float InnerValue;
        protected float Value { get { CheckValidity(); return InnerValue; } set { InnerValue = value; } }

        public TFloat()          { InnerValue = 0; }
        public TFloat(float v)   { SetValue(v); }
        public TFloat(object v)  { SetValue(v); }

        public static implicit operator float(TFloat t) => t.InnerValue;
        public static implicit operator TFloat(float v) => new TFloat(v);

        public static bool operator ==(TFloat o1, TFloat o2) => EqualityOperatorHelper<TFloat>(o1, o2);
        public static bool operator !=(TFloat o1, TFloat o2) => !(o1 == o2);

        public override Type   GetUnderlyingType()  => typeof(float);
        public override string GetTypeDescription()  => "float";
        public override object GetValue()            => InnerValue;

        public override void SetDerivedValue(object value)
        {
            if (value is TFloat tf) InnerValue = tf.InnerValue;
            else                    InnerValue = Convert.ToSingle(value);
        }
        public void SetValue(float value) { base.SetValue(value); ConversionError = false; }
        public override void SetNull(bool isNull) { base.SetNull(isNull); if (isNull) InnerValue = 0; }
        public override string ToString() => InnerValue.ToString();
        public int CompareTo(object obj)
        {
            if (obj is TFloat other) return InnerValue.CompareTo(other.InnerValue);
            return InnerValue.CompareTo(Convert.ToSingle(obj));
        }
        public override bool Equals(object obj) => EqualsHelper<TFloat, float>(obj);
        public override int  GetHashCode()      => IsNull() ? 0 : InnerValue.GetHashCode();
    }
}
