using System;
using System.Diagnostics;

namespace ActiveForge
{
    [Serializable]
    [DebuggerDisplay("Null={Null} Value={InnerValue}")]
    public class TChar : TField, IComparable
    {
        protected char InnerValue;
        protected char Value { get { CheckValidity(); return InnerValue; } set { InnerValue = value; } }

        public TChar()          { InnerValue = '\0'; }
        public TChar(char v)    { SetValue(v); }
        public TChar(object v)  { SetValue(v); }

        public static implicit operator char(TChar t) => t.InnerValue;
        public static implicit operator TChar(char v) => new TChar(v);

        public static bool operator ==(TChar o1, TChar o2) => EqualityOperatorHelper<TChar>(o1, o2);
        public static bool operator !=(TChar o1, TChar o2) => !(o1 == o2);

        public override Type   GetUnderlyingType()  => typeof(char);
        public override string GetTypeDescription()  => "char";
        public override object GetValue()            => InnerValue;

        public override void SetDerivedValue(object value)
        {
            if (value is TChar tc)   InnerValue = tc.InnerValue;
            else if (value is char c) InnerValue = c;
            else if (value is string s && s.Length > 0) InnerValue = s[0];
            else                      InnerValue = Convert.ToChar(value);
        }
        public void SetValue(char value) { base.SetValue(value); ConversionError = false; }
        public override void SetNull(bool isNull) { base.SetNull(isNull); if (isNull) InnerValue = '\0'; }
        public override string ToString() => InnerValue.ToString();
        public int CompareTo(object obj)
        {
            if (obj is TChar other) return InnerValue.CompareTo(other.InnerValue);
            return InnerValue.CompareTo(Convert.ToChar(obj));
        }
        public override bool Equals(object obj) => EqualsHelper<TChar, char>(obj);
        public override int  GetHashCode()      => IsNull() ? 0 : InnerValue.GetHashCode();
    }
}
