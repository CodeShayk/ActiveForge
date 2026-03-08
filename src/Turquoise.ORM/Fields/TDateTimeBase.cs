using System;
using System.Diagnostics;

namespace Turquoise.ORM
{
    /// <summary>Base for all date/datetime field types.</summary>
    [Serializable]
    public abstract class TDateTimeBase : TField, IComparable
    {
        protected DateTime InnerValue;

        protected DateTime Value
        {
            get { CheckValidity(); return InnerValue; }
            set { InnerValue = value; }
        }

        protected TDateTimeBase() { InnerValue = DateTime.MinValue; }

        public override object GetValue() => InnerValue;

        public override void SetDerivedValue(object value)
        {
            if (value is TDateTimeBase tb) InnerValue = tb.InnerValue;
            else if (value is DateTime dt) InnerValue = dt;
            else                           InnerValue = Convert.ToDateTime(value);
        }

        public void SetValue(DateTime value) { base.SetValue(value); ConversionError = false; }

        public override void SetNull(bool isNull)
        {
            base.SetNull(isNull);
            if (isNull) InnerValue = DateTime.MinValue;
        }

        public override string ToString() => InnerValue.ToString();

        public int CompareTo(object obj)
        {
            if (obj is TDateTimeBase other)
            {
                if (IsNull() && other.IsNull()) return 0;
                if (IsNull()) return -1;
                if (other.IsNull()) return 1;
                return InnerValue.CompareTo(other.InnerValue);
            }
            return InnerValue.CompareTo(Convert.ToDateTime(obj));
        }

        public DateTime ToDateTime() => InnerValue;
        public int Year  => InnerValue.Year;
        public int Month => InnerValue.Month;
        public int Day   => InnerValue.Day;
    }
}
