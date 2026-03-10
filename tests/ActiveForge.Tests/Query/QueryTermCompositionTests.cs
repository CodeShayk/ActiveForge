using FluentAssertions;
using ActiveForge;
using ActiveForge.Query;
using Xunit;

namespace ActiveForge.Tests.Query
{
    /// <summary>
    /// Tests for QueryTerm logical composition operators (&amp;, |, !) and for
    /// RawSqlTerm, AndTerm, OrTerm, NotTerm SQL generation.
    /// These tests use RawSqlTerm which requires no DataObject or connection.
    /// </summary>
    public class QueryTermCompositionTests
    {
        // ── AND operator ──────────────────────────────────────────────────────────────

        [Fact]
        public void And_TwoTerms_ProducesAndTerm()
        {
            QueryTerm t1 = new RawSqlTerm("a=1");
            QueryTerm t2 = new RawSqlTerm("b=2");
            var result = t1 & t2;
            result.Should().BeOfType<AndTerm>();
        }

        [Fact]
        public void And_NullLeft_ReturnRight()
        {
            QueryTerm t2 = new RawSqlTerm("b=2");
            var result = (QueryTerm)null & t2;
            result.Should().BeSameAs(t2);
        }

        [Fact]
        public void And_NullRight_ReturnLeft()
        {
            QueryTerm t1 = new RawSqlTerm("a=1");
            var result = t1 & (QueryTerm)null;
            result.Should().BeSameAs(t1);
        }

        [Fact]
        public void And_BothNull_ReturnNull()
        {
            var result = (QueryTerm)null & (QueryTerm)null;
            result.Should().BeNull();
        }

        // ── OR operator ───────────────────────────────────────────────────────────────

        [Fact]
        public void Or_TwoTerms_ProducesOrTerm()
        {
            QueryTerm t1 = new RawSqlTerm("a=1");
            QueryTerm t2 = new RawSqlTerm("b=2");
            var result = t1 | t2;
            result.Should().BeOfType<OrTerm>();
        }

        [Fact]
        public void Or_NullLeft_ReturnRight()
        {
            QueryTerm t2 = new RawSqlTerm("b=2");
            var result = (QueryTerm)null | t2;
            result.Should().BeSameAs(t2);
        }

        [Fact]
        public void Or_NullRight_ReturnLeft()
        {
            QueryTerm t1 = new RawSqlTerm("a=1");
            var result = t1 | (QueryTerm)null;
            result.Should().BeSameAs(t1);
        }

        // ── NOT operator ──────────────────────────────────────────────────────────────

        [Fact]
        public void Not_ProducesNotTerm()
        {
            QueryTerm t = new RawSqlTerm("a=1");
            var result = !t;
            result.Should().BeOfType<NotTerm>();
        }

        // ── SQL generation ────────────────────────────────────────────────────────────

        [Fact]
        public void RawSqlTerm_GetSQL_ReturnsSql()
        {
            var term = new RawSqlTerm("x > 5");
            int n = 1;
            var fragment = term.GetSQL(null, ref n);
            fragment.SQL.Should().Be("x > 5");
        }

        [Fact]
        public void RawSqlTerm_GetDeleteSQL_ReturnsSql()
        {
            var term = new RawSqlTerm("x > 5");
            int n = 1;
            term.GetDeleteSQL(null, ref n).Should().Be("x > 5");
        }

        [Fact]
        public void RawSqlTerm_TermNumber_Unchanged()
        {
            var term = new RawSqlTerm("1=1");
            int n = 7;
            term.GetSQL(null, ref n);
            n.Should().Be(7);   // RawSqlTerm has no parameters so doesn't increment
        }

        [Fact]
        public void AndTerm_GetSQL_ProducesParenthesisedAnd()
        {
            var t1 = new RawSqlTerm("a=1");
            var t2 = new RawSqlTerm("b=2");
            var and = (AndTerm)(t1 & t2);
            int n = 1;
            var sql = and.GetSQL(null, ref n).SQL;
            sql.Should().Be("(a=1 AND b=2)");
        }

        [Fact]
        public void OrTerm_GetSQL_ProducesParenthesisedOr()
        {
            var t1 = new RawSqlTerm("a=1");
            var t2 = new RawSqlTerm("b=2");
            var or = (OrTerm)(t1 | t2);
            int n = 1;
            var sql = or.GetSQL(null, ref n).SQL;
            sql.Should().Be("(a=1 OR b=2)");
        }

        [Fact]
        public void NotTerm_GetSQL_ProducesNot()
        {
            var t = new RawSqlTerm("a=1");
            var not = (NotTerm)!t;
            int n = 1;
            var sql = not.GetSQL(null, ref n).SQL;
            sql.Should().Be("NOT (a=1)");
        }

        // ── Nested composition ────────────────────────────────────────────────────────

        [Fact]
        public void NestedAnd_SQL_CorrectlyParenthesised()
        {
            var t1 = new RawSqlTerm("a=1");
            var t2 = new RawSqlTerm("b=2");
            var t3 = new RawSqlTerm("c=3");
            var compound = (t1 & t2) & t3;
            int n = 1;
            var sql = compound.GetSQL(null, ref n).SQL;
            sql.Should().Be("((a=1 AND b=2) AND c=3)");
        }

        [Fact]
        public void OrInsideAnd_SQL_CorrectlyParenthesised()
        {
            var t1 = new RawSqlTerm("a=1");
            var t2 = new RawSqlTerm("b=2");
            var t3 = new RawSqlTerm("c=3");
            var compound = (t1 | t2) & t3;
            int n = 1;
            var sql = compound.GetSQL(null, ref n).SQL;
            sql.Should().Be("((a=1 OR b=2) AND c=3)");
        }

        [Fact]
        public void NotOfAnd_SQL_CorrectlyNested()
        {
            var t1 = new RawSqlTerm("a=1");
            var t2 = new RawSqlTerm("b=2");
            var notAnd = !(t1 & t2);
            int n = 1;
            var sql = notAnd.GetSQL(null, ref n).SQL;
            sql.Should().Be("NOT ((a=1 AND b=2))");
        }

        // ── IncludesLookupDataObject ──────────────────────────────────────────────────

        [Fact]
        public void RawSqlTerm_IncludesLookup_False()
        {
            var term = new RawSqlTerm("1=1");
            term.IncludesLookupDataObject(null).Should().BeFalse();
        }

        // ── BindParameters / GetParameterDebugInfo ────────────────────────────────────

        [Fact]
        public void RawSqlTerm_BindParameters_IsNoOp()
        {
            var term = new RawSqlTerm("1=1");
            int n = 1;
            // Should not throw; no parameters to bind
            term.BindParameters(null, null, null, ref n);
            n.Should().Be(1);
        }

        [Fact]
        public void RawSqlTerm_GetParameterDebugInfo_NoChange()
        {
            var term = new RawSqlTerm("1=1");
            int n = 1;
            string result = "";
            // RawSqlTerm.Field is null → base impl should return without modification
            term.GetParameterDebugInfo(null, null, ref n, ref result);
            result.Should().Be("");
        }

        [Fact]
        public void AndTerm_GetParameterDebugInfo_PropagatesBoth()
        {
            // Both children are RawSqlTerm with null field → result stays ""
            var t1 = new RawSqlTerm("a=1");
            var t2 = new RawSqlTerm("b=2");
            var and = (AndTerm)(t1 & t2);
            int n = 1;
            string result = "";
            and.GetParameterDebugInfo(null, null, ref n, ref result);
            result.Should().Be("");   // RawSqlTerms have no parameters
        }

        // ── QueryFragment ─────────────────────────────────────────────────────────────

        [Fact]
        public void QueryFragment_SQL_Property_RoundTrips()
        {
            var frag = new QueryFragment("SELECT 1");
            frag.SQL.Should().Be("SELECT 1");
        }
    }
}
