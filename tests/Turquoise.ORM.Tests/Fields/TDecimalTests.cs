using FluentAssertions;
using Turquoise.ORM;
using Xunit;

namespace Turquoise.ORM.Tests.Fields
{
    public class TDecimalTests
    {
        [Fact]
        public void Default_IsNull_True()
            => new TDecimal().IsNull().Should().BeTrue();

        [Fact]
        public void Default_GetValue_Zero()
            => ((decimal)new TDecimal().GetValue()).Should().Be(0m);

        [Fact]
        public void SetValue_Decimal_ClearsNull()
        {
            var d = new TDecimal();
            d.SetValue(3.14m);
            d.IsNull().Should().BeFalse();
            ((decimal)d.GetValue()).Should().Be(3.14m);
        }

        [Fact]
        public void ConstructWithDecimal_SetsValue()
        {
            var d = new TDecimal(9.99m);
            d.IsNull().Should().BeFalse();
            ((decimal)d).Should().Be(9.99m);
        }

        [Fact]
        public void ImplicitFrom_Decimal()
        {
            TDecimal d = 1.23m;
            d.IsNull().Should().BeFalse();
            ((decimal)d).Should().Be(1.23m);
        }

        [Fact]
        public void ImplicitTo_Decimal()
        {
            var d = new TDecimal(7.5m);
            decimal v = d;
            v.Should().Be(7.5m);
        }

        [Fact]
        public void SetValue_Null_SetsNull()
        {
            var d = new TDecimal(1m);
            d.SetValue((object)null);
            d.IsNull().Should().BeTrue();
        }

        [Fact]
        public void SetNull_ResetsToZero()
        {
            var d = new TDecimal(5m);
            d.SetNull(true);
            d.IsNull().Should().BeTrue();
            ((decimal)d.GetValue()).Should().Be(0m);
        }

        [Fact]
        public void Equality_SameValue_True()
        {
            var d1 = new TDecimal(2.5m);
            var d2 = new TDecimal(2.5m);
            (d1 == d2).Should().BeTrue();
        }

        [Fact]
        public void Equality_DifferentValue_False()
        {
            var d1 = new TDecimal(1m);
            var d2 = new TDecimal(2m);
            (d1 == d2).Should().BeFalse();
        }

        [Fact]
        public void GreaterThan_Works()
        {
            var d1 = new TDecimal(10m);
            var d2 = new TDecimal(5m);
            (d1 > d2).Should().BeTrue();
            (d2 > d1).Should().BeFalse();
        }

        [Fact]
        public void LessThan_Works()
        {
            var d1 = new TDecimal(1m);
            var d2 = new TDecimal(2m);
            (d1 < d2).Should().BeTrue();
        }

        [Fact]
        public void GreaterOrEqual_Works()
        {
            var d = new TDecimal(5m);
            (d >= new TDecimal(5m)).Should().BeTrue();
            (d >= new TDecimal(4m)).Should().BeTrue();
            (d >= new TDecimal(6m)).Should().BeFalse();
        }

        [Fact]
        public void CompareTo_SameValue_Zero()
        {
            var d1 = new TDecimal(1.5m);
            var d2 = new TDecimal(1.5m);
            d1.CompareTo(d2).Should().Be(0);
        }

        [Fact]
        public void CopyFrom_CopiesValue()
        {
            var src = new TDecimal(99.9m);
            var dst = new TDecimal();
            dst.CopyFrom(src);
            dst.IsNull().Should().BeFalse();
            ((decimal)dst.GetValue()).Should().Be(99.9m);
        }

        [Fact]
        public void GetUnderlyingType_IsDecimal()
            => new TDecimal().GetUnderlyingType().Should().Be(typeof(decimal));

        [Fact]
        public void GetTypeDescription_IsDecimal()
            => new TDecimal().GetTypeDescription().Should().Be("decimal");

        [Fact]
        public void GetHashCode_Consistent()
        {
            var d1 = new TDecimal(42m);
            var d2 = new TDecimal(42m);
            d1.GetHashCode().Should().Be(d2.GetHashCode());
        }

        [Fact]
        public void GetHashCode_Null_IsZero()
            => new TDecimal().GetHashCode().Should().Be(0);
    }
}
