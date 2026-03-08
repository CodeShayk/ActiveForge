using System;
using System.Diagnostics;

namespace Turquoise.ORM
{
    /// <summary>Auto-generated integer primary key field.</summary>
    [Serializable]
    [DebuggerDisplay("Null={Null} Value={InnerValue}")]
    public class TPrimaryKey : TKey
    {
        public TPrimaryKey()            : base() { }
        public TPrimaryKey(int value)   : base(value) { }
        public TPrimaryKey(long value)  : base(checked((int)value)) { }
        public TPrimaryKey(object v)    : base(v) { }
        public TPrimaryKey(TInt value)  { CopyFrom(value); }

        public static implicit operator TPrimaryKey(int v)          => new TPrimaryKey(v);
        public static implicit operator TPrimaryKey(long v)         => new TPrimaryKey(v);
        public static implicit operator TForeignKey(TPrimaryKey pk) => new TForeignKey((TInt)pk);

        public static bool operator ==(TPrimaryKey o1, TPrimaryKey o2) => EqualityOperatorHelper<TPrimaryKey>(o1, o2);
        public static bool operator !=(TPrimaryKey o1, TPrimaryKey o2) => !(o1 == o2);
        public static bool operator ==(TPrimaryKey o1, int o2)         => o1 == (TPrimaryKey)o2;
        public static bool operator !=(TPrimaryKey o1, int o2)         => o1 != (TPrimaryKey)o2;
        public static bool operator ==(int o1, TPrimaryKey o2)         => (TPrimaryKey)o1 == o2;
        public static bool operator !=(int o1, TPrimaryKey o2)         => (TPrimaryKey)o1 != o2;
        public static bool operator >(TPrimaryKey o1, TPrimaryKey o2)  => GTHelper<TPrimaryKey>(o1, o2);
        public static bool operator <(TPrimaryKey o1, TPrimaryKey o2)  => LTHelper<TPrimaryKey>(o1, o2);
        public static bool operator >=(TPrimaryKey o1, TPrimaryKey o2) => o1 > o2 || o1 == o2;
        public static bool operator <=(TPrimaryKey o1, TPrimaryKey o2) => o1 < o2 || o1 == o2;

        public static TPrimaryKey operator ++(TPrimaryKey pk) => new TPrimaryKey(pk.InnerValue + 1);
        public static TPrimaryKey operator --(TPrimaryKey pk) => new TPrimaryKey(pk.InnerValue - 1);

        public override string GetTypeDescription() => "primarykey";
        public override bool   Equals(object obj)   => base.Equals(obj);
        public override int    GetHashCode()         => InnerValue.GetHashCode();
    }
}
