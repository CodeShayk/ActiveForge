using System;
using System.Linq;
using System.Linq.Expressions;
using FluentAssertions;
using Turquoise.ORM.Linq;
using Turquoise.ORM.Query;
using Turquoise.ORM.Tests.Helpers;
using Xunit;

namespace Turquoise.ORM.Tests.Linq
{
    /// <summary>
    /// Tests for <see cref="OrmQueryable{T}"/> LINQ operator accumulation.
    /// No real database execution is performed.
    /// </summary>
    public class OrmQueryableTests
    {
        private static readonly StubDataConnection Conn = new StubDataConnection();

        private sealed class Item : DataObject
        {
            public TString  Label = new TString();
            public TInt     Count = new TInt();
            public TDecimal Price = new TDecimal();
            public Item() : base(Conn) { }
        }

        private static Item T() => new Item();

        // ── Direct API ────────────────────────────────────────────────────────────────

        [Fact]
        public void NewQueryable_HasNoWhereTerm()
        {
            var q = new OrmQueryable<Item>(Conn, T());
            q.WhereTerm.Should().BeNull();
        }

        [Fact]
        public void WithWhere_SetsWhereTerm()
        {
            var template = T();
            var q    = new OrmQueryable<Item>(Conn, template);
            var term = new EqualTerm(template, template.Label, "hello");
            q.WithWhere(term).WhereTerm.Should().BeSameAs(term);
        }

        [Fact]
        public void WithWhere_AndExistingTerm_CombinesWithAnd()
        {
            var template = T();
            var q  = new OrmQueryable<Item>(Conn, template);
            var t1 = new EqualTerm(template, template.Label, "A");
            var t2 = new EqualTerm(template, template.Label, "B");
            q.WithWhere(t1).WithWhere(t2).WhereTerm.Should().BeOfType<AndTerm>();
        }

        [Fact]
        public void WithTake_SetsPageSize()
            => new OrmQueryable<Item>(Conn, T()).WithTake(25).PageSize.Should().Be(25);

        [Fact]
        public void WithSkip_SetsSkipCount()
            => new OrmQueryable<Item>(Conn, T()).WithSkip(10).SkipCount.Should().Be(10);

        [Fact]
        public void WithSort_SetsFirstSortOrder()
        {
            var template = T();
            var sort = new OrderAscending(template, template.Label);
            new OrmQueryable<Item>(Conn, template).WithSort(sort, reset: true).SortOrder.Should().BeSameAs(sort);
        }

        [Fact]
        public void WithSort_Appended_CombinesIntoComposite()
        {
            var template = T();
            var s1 = new OrderAscending(template, template.Label);
            var s2 = new OrderDescending(template, template.Count);
            new OrmQueryable<Item>(Conn, template)
                .WithSort(s1, reset: true)
                .WithSort(s2, reset: false)
                .SortOrder.Should().BeOfType<CombinedSortOrder>();
        }

        [Fact]
        public void WithSort_Reset_ReplacesExisting()
        {
            var template = T();
            var s1 = new OrderAscending(template, template.Label);
            var s2 = new OrderDescending(template, template.Count);
            new OrmQueryable<Item>(Conn, template)
                .WithSort(s1, reset: true)
                .WithSort(s2, reset: true)
                .SortOrder.Should().BeSameAs(s2);
        }

        // ── Immutability ──────────────────────────────────────────────────────────────

        [Fact]
        public void WithWhere_DoesNotMutateOriginal()
        {
            var template = T();
            var original = new OrmQueryable<Item>(Conn, template);
            _ = original.WithWhere(new EqualTerm(template, template.Label, "X"));
            original.WhereTerm.Should().BeNull();
        }

        [Fact]
        public void WithTake_DoesNotMutateOriginal()
        {
            var original = new OrmQueryable<Item>(Conn, T());
            _ = original.WithTake(5);
            original.PageSize.Should().Be(0);
        }

        // ── IQueryable metadata ───────────────────────────────────────────────────────

        [Fact]
        public void ElementType_IsT()
            => new OrmQueryable<Item>(Conn, T()).ElementType.Should().Be(typeof(Item));

        [Fact]
        public void Expression_IsConstantOfSelf()
        {
            var q = new OrmQueryable<Item>(Conn, T());
            ((ConstantExpression)q.Expression).Value.Should().BeSameAs(q);
        }

        [Fact]
        public void Provider_IsOrmQueryProvider()
            => new OrmQueryable<Item>(Conn, T()).Provider.Should().BeOfType<OrmQueryProvider<Item>>();

        // ── LINQ operator chain ───────────────────────────────────────────────────────

        [Fact]
        public void LinqWhere_AccumulatesQueryTerm()
        {
            var orm = (OrmQueryable<Item>)new OrmQueryable<Item>(Conn, T())
                .Where(i => i.Count > 3);
            orm.WhereTerm.Should().BeOfType<GreaterThanTerm>();
        }

        [Fact]
        public void LinqWhere_TwoPredicates_CombineWithAnd()
        {
            var orm = (OrmQueryable<Item>)new OrmQueryable<Item>(Conn, T())
                .Where(i => i.Count > 0)
                .Where(i => i.Label == "X");
            orm.WhereTerm.Should().BeOfType<AndTerm>();
        }

        [Fact]
        public void LinqOrderBy_SetsAscendingSort()
        {
            var orm = (OrmQueryable<Item>)new OrmQueryable<Item>(Conn, T()).OrderBy(i => i.Label);
            orm.SortOrder.Should().BeOfType<OrderAscending>();
        }

        [Fact]
        public void LinqOrderByDescending_SetsDescendingSort()
        {
            var orm = (OrmQueryable<Item>)new OrmQueryable<Item>(Conn, T()).OrderByDescending(i => i.Count);
            orm.SortOrder.Should().BeOfType<OrderDescending>();
        }

        [Fact]
        public void LinqThenBy_AppendsSortAsComposite()
        {
            var orm = (OrmQueryable<Item>)new OrmQueryable<Item>(Conn, T())
                .OrderBy(i => i.Label)
                .ThenBy(i => i.Count);
            orm.SortOrder.Should().BeOfType<CombinedSortOrder>();
        }

        [Fact]
        public void LinqTake_SetsPageSize()
        {
            var orm = (OrmQueryable<Item>)new OrmQueryable<Item>(Conn, T()).Take(10);
            orm.PageSize.Should().Be(10);
        }

        [Fact]
        public void LinqSkip_SetsSkipCount()
        {
            var orm = (OrmQueryable<Item>)new OrmQueryable<Item>(Conn, T()).Skip(5);
            orm.SkipCount.Should().Be(5);
        }

        [Fact]
        public void LinqChain_AllOperators_AccumulatesState()
        {
            var orm = (OrmQueryable<Item>)new OrmQueryable<Item>(Conn, T())
                .Where(i => i.Label == "Test")
                .OrderBy(i => i.Label)
                .ThenByDescending(i => i.Count)
                .Skip(5)
                .Take(20);

            orm.WhereTerm.Should().BeOfType<EqualTerm>();
            orm.SortOrder.Should().BeOfType<CombinedSortOrder>();
            orm.SkipCount.Should().Be(5);
            orm.PageSize.Should().Be(20);
        }

        // ── Unsupported LINQ method ───────────────────────────────────────────────────

        [Fact]
        public void UnsupportedLinqMethod_Throws()
        {
            Action act = () => new OrmQueryable<Item>(Conn, T())
                .GroupBy(i => i.Label).ToList();
            act.Should().Throw<NotSupportedException>();
        }
    }
}
