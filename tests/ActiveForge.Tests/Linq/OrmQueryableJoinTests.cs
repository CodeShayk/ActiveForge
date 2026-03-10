using System.Linq;
using FluentAssertions;
using ActiveForge;
using ActiveForge.Linq;
using ActiveForge.Query;
using ActiveForge.Tests.Helpers;
using Xunit;

namespace ActiveForge.Tests.Linq
{
    /// <summary>
    /// Tests for JOIN support in <see cref="OrmQueryable{T}"/>:
    ///   1. Fluent join-type overrides (<c>InnerJoin</c>, <c>LeftOuterJoin</c>)
    ///   2. Cross-join field access in <c>Where</c> predicates
    ///   3. Cross-join field access in <c>OrderBy</c> key selectors
    /// No real database execution is required — all tests exercise query accumulation
    /// and expression translation only.
    /// </summary>
    public class OrmQueryableJoinTests
    {
        private static readonly StubDataConnection Conn = new StubDataConnection();

        // ── Test entities ─────────────────────────────────────────────────────────────

        /// <summary>Embedded DataObject acting as a joined table.</summary>
        private sealed class Tag : DataObject
        {
            public TString TagName = new TString();
            public TInt    Rating  = new TInt();
            public Tag() : base(Conn) { }
        }

        /// <summary>Root entity with an embedded Tag (simulates a join).</summary>
        private sealed class TaggedItem : DataObject
        {
            public TString Label  = new TString();
            public TInt    Count  = new TInt();
            public TInt    TagId  = new TInt();   // FK
            public Tag     Tag    = new Tag();    // embedded → join
            public TaggedItem() : base(Conn) { }
        }

        private static TaggedItem T() => new TaggedItem();

        // ── InnerJoin / LeftOuterJoin accumulation ────────────────────────────────────

        [Fact]
        public void InnerJoin_AccumulatesJoinOverride()
        {
            var q = new OrmQueryable<TaggedItem>(Conn, T()).InnerJoin<Tag>();
            q.Joins.Should().HaveCount(1);
            q.Joins[0].TargetType.Should().Be(typeof(Tag));
            q.Joins[0].JoinType.Should().Be(JoinSpecification.JoinTypeEnum.InnerJoin);
        }

        [Fact]
        public void LeftOuterJoin_AccumulatesJoinOverride()
        {
            var q = new OrmQueryable<TaggedItem>(Conn, T()).LeftOuterJoin<Tag>();
            q.Joins.Should().HaveCount(1);
            q.Joins[0].TargetType.Should().Be(typeof(Tag));
            q.Joins[0].JoinType.Should().Be(JoinSpecification.JoinTypeEnum.LeftOuterJoin);
        }

        [Fact]
        public void NewQueryable_HasNoJoins()
        {
            var q = new OrmQueryable<TaggedItem>(Conn, T());
            q.Joins.Should().BeNull();
        }

        [Fact]
        public void JoinOverride_ReplacesExistingOverrideForSameType()
        {
            // Start with LeftOuterJoin, then switch to InnerJoin for the same type.
            var q = new OrmQueryable<TaggedItem>(Conn, T())
                .LeftOuterJoin<Tag>()
                .InnerJoin<Tag>();

            q.Joins.Should().HaveCount(1);
            q.Joins[0].JoinType.Should().Be(JoinSpecification.JoinTypeEnum.InnerJoin);
        }

        [Fact]
        public void JoinOverride_DoesNotMutateOriginal()
        {
            var original = new OrmQueryable<TaggedItem>(Conn, T());
            _ = original.LeftOuterJoin<Tag>();
            original.Joins.Should().BeNull();
        }

        [Fact]
        public void JoinOverride_ChainsWithWhereAndSort()
        {
            var template = T();
            var q = new OrmQueryable<TaggedItem>(Conn, template)
                .LeftOuterJoin<Tag>()
                .Where(i => i.Count > 0)
                .OrderBy(i => i.Label);

            var orm = (OrmQueryable<TaggedItem>)q;
            orm.Joins.Should().HaveCount(1);
            orm.WhereTerm.Should().BeOfType<GreaterThanTerm>();
            orm.SortOrder.Should().BeOfType<OrderAscending>();
        }

        // ── Cross-join Where predicates ───────────────────────────────────────────────

        [Fact]
        public void CrossJoinWhere_EqualTerm_DoesNotThrow()
        {
            // x => x.Tag.TagName == "sports"  — field on embedded DataObject
            var template = T();
            QueryTerm term = ExpressionToQueryTermVisitor.Translate(
                (System.Linq.Expressions.Expression<System.Func<TaggedItem, bool>>)(x => x.Tag.TagName == "sports"),
                template);
            term.Should().BeOfType<EqualTerm>();
        }

        [Fact]
        public void CrossJoinWhere_GreaterThanTerm_DoesNotThrow()
        {
            var template = T();
            QueryTerm term = ExpressionToQueryTermVisitor.Translate(
                (System.Linq.Expressions.Expression<System.Func<TaggedItem, bool>>)(x => x.Tag.Rating > 3),
                template);
            term.Should().BeOfType<GreaterThanTerm>();
        }

        [Fact]
        public void CrossJoinWhere_IsNullTerm_DoesNotThrow()
        {
            var template = T();
            QueryTerm term = ExpressionToQueryTermVisitor.Translate(
                (System.Linq.Expressions.Expression<System.Func<TaggedItem, bool>>)(x => x.Tag.TagName == (TString)null),
                template);
            term.Should().BeOfType<IsNullTerm>();
        }

        [Fact]
        public void CrossJoinWhere_NotNullTerm_DoesNotThrow()
        {
            var template = T();
            QueryTerm term = ExpressionToQueryTermVisitor.Translate(
                (System.Linq.Expressions.Expression<System.Func<TaggedItem, bool>>)(x => x.Tag.TagName != (TString)null),
                template);
            term.Should().BeOfType<NotTerm>();
        }

        [Fact]
        public void CrossJoinWhere_CombinedWithDirectField_ProducesAndTerm()
        {
            // (x.Label == "hello") && (x.Tag.TagName == "sports")
            var template = T();
            QueryTerm term = ExpressionToQueryTermVisitor.Translate(
                (System.Linq.Expressions.Expression<System.Func<TaggedItem, bool>>)(
                    x => x.Label == "hello" && x.Tag.TagName == "sports"),
                template);
            term.Should().BeOfType<AndTerm>();
        }

        [Fact]
        public void CrossJoinWhere_ViaLinqChain_AccumulatesCorrectly()
        {
            var q = (OrmQueryable<TaggedItem>)
                new OrmQueryable<TaggedItem>(Conn, T())
                    .Where(x => x.Tag.TagName == "sports");

            q.WhereTerm.Should().BeOfType<EqualTerm>();
        }

        // ── Cross-join OrderBy selectors ──────────────────────────────────────────────

        [Fact]
        public void CrossJoinOrderBy_Ascending_DoesNotThrow()
        {
            var template = T();
            SortOrder sort = ExpressionToSortVisitor.TranslateAscending(
                (System.Linq.Expressions.Expression<System.Func<TaggedItem, TString>>)(x => x.Tag.TagName),
                template);
            sort.Should().BeOfType<OrderAscending>();
        }

        [Fact]
        public void CrossJoinOrderBy_Descending_DoesNotThrow()
        {
            var template = T();
            SortOrder sort = ExpressionToSortVisitor.TranslateDescending(
                (System.Linq.Expressions.Expression<System.Func<TaggedItem, TInt>>)(x => x.Tag.Rating),
                template);
            sort.Should().BeOfType<OrderDescending>();
        }

        [Fact]
        public void CrossJoinOrderBy_ViaLinqChain_AccumulatesCorrectly()
        {
            var q = (OrmQueryable<TaggedItem>)
                new OrmQueryable<TaggedItem>(Conn, T())
                    .OrderBy(x => x.Tag.TagName);

            q.SortOrder.Should().BeOfType<OrderAscending>();
        }

        [Fact]
        public void CrossJoinThenBy_ViaLinqChain_ProducesCombinedSort()
        {
            var q = (OrmQueryable<TaggedItem>)
                new OrmQueryable<TaggedItem>(Conn, T())
                    .OrderBy(x => x.Label)
                    .ThenByDescending(x => x.Tag.Rating);

            q.SortOrder.Should().BeOfType<CombinedSortOrder>();
        }

        // ── Direct field access still works after refactor ────────────────────────────

        [Fact]
        public void DirectField_Where_StillWorks()
        {
            var template = T();
            QueryTerm term = ExpressionToQueryTermVisitor.Translate(
                (System.Linq.Expressions.Expression<System.Func<TaggedItem, bool>>)(x => x.Count > 5),
                template);
            term.Should().BeOfType<GreaterThanTerm>();
        }

        [Fact]
        public void DirectField_OrderBy_StillWorks()
        {
            var template = T();
            SortOrder sort = ExpressionToSortVisitor.TranslateAscending(
                (System.Linq.Expressions.Expression<System.Func<TaggedItem, TString>>)(x => x.Label),
                template);
            sort.Should().BeOfType<OrderAscending>();
        }

        // ── WithJoin internal API ─────────────────────────────────────────────────────

        [Fact]
        public void WithJoin_SetsJoinOverride()
        {
            var ov = new JoinOverride(typeof(Tag), JoinSpecification.JoinTypeEnum.LeftOuterJoin);
            var q = new OrmQueryable<TaggedItem>(Conn, T()).WithJoin(ov);
            q.Joins.Should().HaveCount(1);
            q.Joins[0].JoinType.Should().Be(JoinSpecification.JoinTypeEnum.LeftOuterJoin);
        }

        [Fact]
        public void WithJoin_DoesNotMutateOriginal()
        {
            var ov       = new JoinOverride(typeof(Tag), JoinSpecification.JoinTypeEnum.LeftOuterJoin);
            var original = new OrmQueryable<TaggedItem>(Conn, T());
            _            = original.WithJoin(ov);
            original.Joins.Should().BeNull();
        }
    }
}
