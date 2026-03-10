using System;
using System.Diagnostics;

namespace ActiveForge
{
    /// <summary>Boolean database field.</summary>
    [Serializable]
    [DebuggerDisplay("Null={Null} Value={InnerValue}")]
    public class TBool : TField, IComparable
    {
        protected bool InnerValue;
        protected bool Value { get { CheckValidity(); return InnerValue; } set { InnerValue = value; } }

        public TBool()           { InnerValue = false; }
        public TBool(bool value) { SetValue(value); }
        public TBool(object v)   { SetValue(v); }

        public static implicit operator bool(TBool t)   => t.InnerValue;
        public static implicit operator TBool(bool v)   => new TBool(v);

        public static bool operator ==(TBool o1, TBool o2) => EqualityOperatorHelper<TBool>(o1, o2);
        public static bool operator !=(TBool o1, TBool o2) => !(o1 == o2);
        public static bool operator ==(TBool o1, bool  o2) => o1 == (TBool)o2;
        public static bool operator !=(TBool o1, bool  o2) => o1 != (TBool)o2;
        public static bool operator ==(bool  o1, TBool o2) => (TBool)o1 == o2;
        public static bool operator !=(bool  o1, TBool o2) => (TBool)o1 != o2;

        public override Type   GetUnderlyingType()  => typeof(bool);
        public override string GetTypeDescription()  => "bool";
        public override object GetValue()            => InnerValue;

        public override void SetDerivedValue(object value)
        {
            if (value is TBool tb) InnerValue = tb.InnerValue;
            else                   InnerValue = Convert.ToBoolean(value);
        }

        public void SetValue(bool value) { base.SetValue(value); ConversionError = false; }

        public override void SetNull(bool isNull)
        {
            base.SetNull(isNull);
            if (isNull) InnerValue = false;
        }

        public override string ToString() => InnerValue.ToString();
        public int CompareTo(object obj)
        {
            if (obj is TBool other) return InnerValue.CompareTo(other.InnerValue);
            return InnerValue.CompareTo(Convert.ToBoolean(obj));
        }

        public override bool Equals(object obj) => EqualsHelper<TBool, bool>(obj);
        public override int  GetHashCode()      => IsNull() ? 0 : InnerValue.GetHashCode();
    }
}
