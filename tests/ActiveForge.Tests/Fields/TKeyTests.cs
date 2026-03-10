using FluentAssertions;
using ActiveForge;
using Xunit;

namespace ActiveForge.Tests.Fields
{
    public class TKeyTests
    {
        // ── TPrimaryKey ───────────────────────────────────────────────────────────────

        [Fact]
        public void PrimaryKey_Default_IsNull()
            => new TPrimaryKey().IsNull().Should().BeTrue();

        [Fact]
        public void PrimaryKey_ConstructWithInt_SetsValue()
        {
            var pk = new TPrimaryKey(42);
            pk.IsNull().Should().BeFalse();
            ((int)pk.GetValue()).Should().Be(42);
        }

        [Fact]
        public void PrimaryKey_ImplicitFrom_Int()
        {
            TPrimaryKey pk = 100;
            pk.IsNull().Should().BeFalse();
            ((int)pk.GetValue()).Should().Be(100);
        }

        [Fact]
        public void PrimaryKey_ImplicitFrom_Long()
        {
            TPrimaryKey pk = 200L;
            ((int)pk.GetValue()).Should().Be(200);
        }

        [Fact]
        public void PrimaryKey_Equality_SameValue()
        {
            var pk1 = new TPrimaryKey(5);
            var pk2 = new TPrimaryKey(5);
            (pk1 == pk2).Should().BeTrue();
        }

        [Fact]
        public void PrimaryKey_Equality_WithInt()
        {
            var pk = new TPrimaryKey(7);
            (pk == 7).Should().BeTrue();
            (7 == pk).Should().BeTrue();
            (pk == 8).Should().BeFalse();
        }

        [Fact]
        public void PrimaryKey_Increment()
        {
            var pk = new TPrimaryKey(10);
            var inc = ++pk;
            ((int)inc.GetValue()).Should().Be(11);
        }

        [Fact]
        public void PrimaryKey_Decrement()
        {
            var pk = new TPrimaryKey(10);
            var dec = --pk;
            ((int)dec.GetValue()).Should().Be(9);
        }

        [Fact]
        public void PrimaryKey_GreaterThan()
        {
            var pk1 = new TPrimaryKey(10);
            var pk2 = new TPrimaryKey(5);
            (pk1 > pk2).Should().BeTrue();
            (pk2 > pk1).Should().BeFalse();
        }

        [Fact]
        public void PrimaryKey_LessThan()
        {
            var pk1 = new TPrimaryKey(3);
            var pk2 = new TPrimaryKey(9);
            (pk1 < pk2).Should().BeTrue();
        }

        [Fact]
        public void PrimaryKey_GetTypeDescription()
            => new TPrimaryKey().GetTypeDescription().Should().Be("primarykey");

        // ── TForeignKey ───────────────────────────────────────────────────────────────

        [Fact]
        public void ForeignKey_Default_IsNull()
            => new TForeignKey().IsNull().Should().BeTrue();

        [Fact]
        public void ForeignKey_ConstructWithInt_SetsValue()
        {
            var fk = new TForeignKey(99);
            fk.IsNull().Should().BeFalse();
            ((int)fk.GetValue()).Should().Be(99);
        }

        [Fact]
        public void ForeignKey_ImplicitFrom_Int()
        {
            TForeignKey fk = 55;
            fk.IsNull().Should().BeFalse();
        }

        [Fact]
        public void ForeignKey_ImplicitFrom_Long()
        {
            TForeignKey fk = 300L;
            ((int)fk.GetValue()).Should().Be(300);
        }

        [Fact]
        public void ForeignKey_Equality_SameValue()
        {
            var fk1 = new TForeignKey(10);
            var fk2 = new TForeignKey(10);
            (fk1 == fk2).Should().BeTrue();
        }

        [Fact]
        public void ForeignKey_Equality_WithInt()
        {
            var fk = new TForeignKey(20);
            (fk == 20).Should().BeTrue();
            (20 == fk).Should().BeTrue();
        }

        [Fact]
        public void ForeignKey_GetTypeDescription()
            => new TForeignKey().GetTypeDescription().Should().Be("foreignkey");

        // ── Cross-type PK ↔ FK ────────────────────────────────────────────────────────

        [Fact]
        public void PrimaryKey_ImplicitTo_ForeignKey()
        {
            var pk = new TPrimaryKey(42);
            TForeignKey fk = pk;
            ((int)fk.GetValue()).Should().Be(42);
        }

        [Fact]
        public void ForeignKey_ImplicitTo_PrimaryKey()
        {
            var fk = new TForeignKey(7);
            TPrimaryKey pk = fk;
            ((int)pk.GetValue()).Should().Be(7);
        }

        [Fact]
        public void PrimaryKey_ForeignKey_Equality_ViaConversion()
        {
            // The safe cross-type comparison is to explicitly convert PK → FK first
            var pk = new TPrimaryKey(5);
            var fk = new TForeignKey(5);
            TForeignKey pkAsFk = pk;   // implicit TPrimaryKey → TForeignKey
            (pkAsFk == fk).Should().BeTrue();
        }

        [Fact]
        public void ForeignKey_PrimaryKey_Equality_ViaConversion()
        {
            var pk = new TPrimaryKey(7);
            TForeignKey fkFromPk = pk;
            var fk2 = new TForeignKey(7);
            (fkFromPk == fk2).Should().BeTrue();
        }

        // ── CopyFrom ──────────────────────────────────────────────────────────────────

        [Fact]
        public void PrimaryKey_CopyFrom_CopiesValue()
        {
            var src = new TPrimaryKey(33);
            var dst = new TPrimaryKey();
            dst.CopyFrom(src);
            dst.IsNull().Should().BeFalse();
            ((int)dst.GetValue()).Should().Be(33);
        }

        [Fact]
        public void ForeignKey_CopyFrom_CopiesValue()
        {
            var src = new TForeignKey(77);
            var dst = new TForeignKey();
            dst.CopyFrom(src);
            ((int)dst.GetValue()).Should().Be(77);
        }

        // ── SetNull ───────────────────────────────────────────────────────────────────

        [Fact]
        public void PrimaryKey_SetNull_ResetsToZero()
        {
            var pk = new TPrimaryKey(99);
            pk.SetNull(true);
            pk.IsNull().Should().BeTrue();
            ((int)pk.GetValue()).Should().Be(0);
        }

        [Fact]
        public void ForeignKey_SetNull_ResetsToZero()
        {
            var fk = new TForeignKey(99);
            fk.SetNull(true);
            fk.IsNull().Should().BeTrue();
        }
    }
}
