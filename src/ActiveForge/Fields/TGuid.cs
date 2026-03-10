using System;
using System.Diagnostics;

namespace ActiveForge
{
    [Serializable]
    [DebuggerDisplay("Null={Null} Value={InnerValue}")]
    public class TGuid : TField, IComparable
    {
        protected Guid InnerValue;
        protected Guid Value { get { CheckValidity(); return InnerValue; } set { InnerValue = value; } }

        public TGuid()          { InnerValue = Guid.Empty; }
        public TGuid(Guid v)    { SetValue(v); }
        public TGuid(object v)  { SetValue(v); }

        public static implicit operator Guid(TGuid t)  => t.InnerValue;
        public static implicit operator TGuid(Guid v)  => new TGuid(v);

        public static bool operator ==(TGuid o1, TGuid o2) => EqualityOperatorHelper<TGuid>(o1, o2);
        public static bool operator !=(TGuid o1, TGuid o2) => !(o1 == o2);

        public override Type   GetUnderlyingType()  => typeof(Guid);
        public override string GetTypeDescription()  => "guid";
        public override object GetValue()            => InnerValue;

        public override void SetDerivedValue(object value)
        {
            if (value is TGuid tg)   InnerValue = tg.InnerValue;
            else if (value is Guid g) InnerValue = g;
            else                      InnerValue = new Guid(value.ToString());
        }
        public void SetValue(Guid value) { base.SetValue(value); ConversionError = false; }
        public override void SetNull(bool isNull) { base.SetNull(isNull); if (isNull) InnerValue = Guid.Empty; }
        public override string ToString() => InnerValue.ToString();
        public int CompareTo(object obj)
        {
            if (obj is TGuid other) return InnerValue.CompareTo(other.InnerValue);
            if (obj is Guid  g)     return InnerValue.CompareTo(g);
            return InnerValue.CompareTo(new Guid(obj.ToString()));
        }
        public override bool Equals(object obj) => EqualsHelper<TGuid, Guid>(obj);
        public override int  GetHashCode()      => IsNull() ? 0 : InnerValue.GetHashCode();
    }
}
