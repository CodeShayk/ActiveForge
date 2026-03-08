using System;
using System.Collections;
using System.Diagnostics;
using System.Globalization;

namespace Turquoise.ORM
{
    /// <summary>String database field.</summary>
    [Serializable]
    [DebuggerDisplay("Null={Null} Value={InnerValue}")]
    public class TString : TField, IComparable, ICloneable, IEnumerable
    {
        protected string InnerValue = "";

        protected string Value
        {
            get { CheckValidity(); return InnerValue; }
            set { InnerValue = value; }
        }

        public TString()
        {
            ConvertEmptyStringsToNull = false;
            InnerValue = "";
        }
        public TString(string value)  { ConvertEmptyStringsToNull = false; SetValue(value); }
        public TString(object value)  { ConvertEmptyStringsToNull = false; SetValue(value); }

        public static implicit operator string(TString s) => s.InnerValue;
        public static implicit operator TString(string s) => new TString(s);

        public static bool operator ==(TString s1, TString s2) => EqualityOperatorHelper<TString>(s1, s2);
        public static bool operator !=(TString s1, TString s2) => !(s1 == s2);
        public static bool operator ==(string  s1, TString s2) => (TString)s1 == s2;
        public static bool operator !=(string  s1, TString s2) => !((TString)s1 == s2);
        public static bool operator ==(TString s1, string  s2) => s1 == (TString)s2;
        public static bool operator !=(TString s1, string  s2) => !(s1 == (TString)s2);

        public override Type   GetUnderlyingType()  => typeof(string);
        public override string GetTypeDescription()  => "string";
        public override object GetValue()            => InnerValue;

        public override void SetDerivedValue(object value)
        {
            if (value is TString ts) { InnerValue = ts.InnerValue; }
            else                     { InnerValue = value.ToString(); }
        }

        public override void SetNull(bool isNull)
        {
            base.SetNull(isNull);
            if (isNull) InnerValue = "";
        }

        public override string ToString() => InnerValue;

        public int     CompareTo(object obj)
        {
            if (obj is TString other)
            {
                if (IsNull() && other.IsNull()) return 0;
                if (IsNull()) return -1;
                if (other.IsNull()) return 1;
                return string.Compare(InnerValue, other.InnerValue, StringComparison.Ordinal);
            }
            return string.Compare(InnerValue, obj?.ToString(), StringComparison.Ordinal);
        }

        public object Clone() => new TString(InnerValue);

        IEnumerator IEnumerable.GetEnumerator() => InnerValue.GetEnumerator();

        public int    Length => InnerValue?.Length ?? 0;
        public bool   Contains(string value)  => InnerValue?.Contains(value) ?? false;
        public string ToUpper()               => InnerValue?.ToUpper();
        public string ToLower()               => InnerValue?.ToLower();
        public string Trim()                  => InnerValue?.Trim();

        public static string[] ConvertArray(TString[] source)
        {
            var result = new string[source.Length];
            for (int i = 0; i < source.Length; i++) result[i] = source[i];
            return result;
        }

        public override bool Equals(object obj) => EqualsHelper<TString, string>(obj);
        public override int  GetHashCode()      => IsNull() ? 0 : InnerValue.GetHashCode();
    }
}
