using System;
using System.Diagnostics;

namespace ActiveForge
{
    [Serializable]
    [DebuggerDisplay("Null={Null} Length={InnerValue?.Length}")]
    public class TByteArray : TField
    {
        protected byte[] InnerValue;
        protected byte[] Value { get { CheckValidity(); return InnerValue; } set { InnerValue = value; } }

        public TByteArray()            { }
        public TByteArray(byte[] v)    { SetValue(v); }
        public TByteArray(object v)    { SetValue(v); }

        public static implicit operator byte[](TByteArray t)  => t.InnerValue;
        public static implicit operator TByteArray(byte[] v)  => new TByteArray(v);

        public override Type   GetUnderlyingType()  => typeof(byte[]);
        public override string GetTypeDescription()  => "bytearray";
        public override object GetValue()            => InnerValue;

        public override void SetDerivedValue(object value)
        {
            if (value is TByteArray tb) InnerValue = tb.InnerValue;
            else if (value is byte[] b) InnerValue = b;
            else                        InnerValue = (byte[])value;
        }
        public void SetValue(byte[] value) { base.SetValue(value); ConversionError = false; }
        public override void SetNull(bool isNull) { base.SetNull(isNull); if (isNull) InnerValue = null; }
        public override string ToString() => InnerValue == null ? "" : Convert.ToBase64String(InnerValue);
        public int Length => InnerValue?.Length ?? 0;

        public static bool operator ==(TByteArray o1, TByteArray o2) => EqualityOperatorHelper<TByteArray>(o1, o2);
        public static bool operator !=(TByteArray o1, TByteArray o2) => !(o1 == o2);
        public override bool Equals(object obj) => base.Equals(obj);
        public override int  GetHashCode()      => IsNull() ? 0 : InnerValue.GetHashCode();
    }
}
