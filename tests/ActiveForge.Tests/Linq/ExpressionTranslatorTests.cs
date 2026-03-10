using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using FluentAssertions;
using ActiveForge.Linq;
using ActiveForge.Query;
using ActiveForge.Tests.Helpers;
using Xunit;

namespace ActiveForge.Tests.Linq
{
    /// <summary>
    /// Unit tests for <see cref="ExpressionToQueryTermVisitor"/>.
    /// No real database connection is used.
    /// </summary>
    public class ExpressionTranslatorTests
    {
        // ── Test entity ───────────────────────────────────────────────────────────────

        private static readonly StubDataConnection Conn = new StubDataConnection();

        private sealed class Product : Record
        {
            public TString  Name     = new TString();
            public TInt     Quantity = new TInt();
            public TDecimal Price    = new TDecimal();
            public TBool    IsActive = new TBool();

            public Product() : base(Conn) { }
        }

        private static Product T() => new Product();

        // ── Equality ─────────────────────────────────────────────────────────────────

        [Fact]
        public void Equal_String_ProducesEqualTerm()
        {
            Expression<Func<Product, bool>> expr = p => p.Name == "Acme";
            QueryTerm term = ExpressionToQueryTermVisitor.Translate(expr, T());
            term.Should().BeOfType<EqualTerm>();
            term.Value.Should().Be("Acme");
        }

        [Fact]
        public void Equal_Int_ProducesEqualTerm()
        {
            Expression<Func<Product, bool>> expr = p => p.Quantity == 10;
            QueryTerm term = ExpressionToQueryTermVisitor.Translate(expr, T());
            term.Should().BeOfType<EqualTerm>();
            term.Value.Should().Be(10);
        }

        [Fact]
        public void Equal_Null_ProducesIsNullTerm()
        {
            Expression<Func<Product, bool>> expr = p => p.Name == (TString)null;
            QueryTerm term = ExpressionToQueryTermVisitor.Translate(expr, T());
            term.Should().BeOfType<IsNullTerm>();
        }

        // ── Inequality ────────────────────────────────────────────────────────────────

        [Fact]
        public void NotEqual_String_ProducesNotOfEqualTerm()
        {
            Expression<Func<Product, bool>> expr = p => p.Name != "Acme";
            QueryTerm term = ExpressionToQueryTermVisitor.Translate(expr, T());
            term.Should().BeOfType<NotTerm>();
        }

        [Fact]
        public void NotEqual_Null_ProducesNotOfIsNullTerm()
        {
            Expression<Func<Product, bool>> expr = p => p.Name != (TString)null;
            QueryTerm term = ExpressionToQueryTermVisitor.Translate(expr, T());
            term.Should().BeOfType<NotTerm>();
        }

        // ── Comparison operators ──────────────────────────────────────────────────────

        [Fact]
        public void GreaterThan_ProducesGreaterThanTerm()
        {
            Expression<Func<Product, bool>> expr = p => p.Quantity > 5;
            QueryTerm term = ExpressionToQueryTermVisitor.Translate(expr, T());
            term.Should().BeOfType<GreaterThanTerm>();
            term.Value.Should().Be(5);
        }

        [Fact]
        public void GreaterOrEqual_ProducesGreaterOrEqualTerm()
        {
            Expression<Func<Product, bool>> expr = p => p.Quantity >= 5;
            QueryTerm term = ExpressionToQueryTermVisitor.Translate(expr, T());
            term.Should().BeOfType<GreaterOrEqualTerm>();
        }

        [Fact]
        public void LessThan_ProducesLessThanTerm()
        {
            Expression<Func<Product, bool>> expr = p => p.Price < 100m;
            QueryTerm term = ExpressionToQueryTermVisitor.Translate(expr, T());
            term.Should().BeOfType<LessThanTerm>();
            term.Value.Should().Be(100m);
        }

        [Fact]
        public void LessOrEqual_ProducesLessOrEqualTerm()
        {
            Expression<Func<Product, bool>> expr = p => p.Price <= 99.99m;
            QueryTerm term = ExpressionToQueryTermVisitor.Translate(expr, T());
            term.Should().BeOfType<LessOrEqualTerm>();
        }

        // ── Logical composition ───────────────────────────────────────────────────────

        [Fact]
        public void AndAlso_ProducesAndTerm()
        {
            Expression<Func<Product, bool>> expr = p => p.Name == "Acme" && p.Quantity > 0;
            QueryTerm term = ExpressionToQueryTermVisitor.Translate(expr, T());
            term.Should().BeOfType<AndTerm>();
        }

        [Fact]
        public void OrElse_ProducesOrTerm()
        {
            Expression<Func<Product, bool>> expr = p => p.Name == "Acme" || p.Name == "Widget";
            QueryTerm term = ExpressionToQueryTermVisitor.Translate(expr, T());
            term.Should().BeOfType<OrTerm>();
        }

        [Fact]
        public void Not_ProducesNotTerm()
        {
            Expression<Func<Product, bool>> expr = p => !(p.Name == "Acme");
            QueryTerm term = ExpressionToQueryTermVisitor.Translate(expr, T());
            term.Should().BeOfType<NotTerm>();
        }

        // ── Contains / IN ─────────────────────────────────────────────────────────────

        [Fact]
        public void Contains_OnList_ProducesInTerm()
        {
            // List<string>.Contains(TString) — implicit TString→string conversion applies
            var names = new List<string> { "Acme", "Widget", "Gizmo" };
            Expression<Func<Product, bool>> expr = p => names.Contains(p.Name);
            QueryTerm term = ExpressionToQueryTermVisitor.Translate(expr, T());
            term.Should().BeOfType<InTerm>();
        }

        [Fact]
        public void Contains_SecondList_ProducesInTerm()
        {
            // Second form: another List<string> — verifies not affected by order of assertion
            var codes = new List<string> { "X", "Y", "Z" };
            Expression<Func<Product, bool>> expr = p => codes.Contains(p.Name);
            QueryTerm term = ExpressionToQueryTermVisitor.Translate(expr, T());
            term.Should().BeOfType<InTerm>();
        }

        // ── Local variable capture ────────────────────────────────────────────────────

        [Fact]
        public void LocalVariable_IsEvaluatedAtTranslationTime()
        {
            string localName = "Captured";
            Expression<Func<Product, bool>> expr = p => p.Name == localName;
            QueryTerm term = ExpressionToQueryTermVisitor.Translate(expr, T());
            term.Should().BeOfType<EqualTerm>();
            term.Value.Should().Be("Captured");
        }

        [Fact]
        public void LocalInt_IsEvaluatedAtTranslationTime()
        {
            int threshold = 42;
            Expression<Func<Product, bool>> expr = p => p.Quantity >= threshold;
            QueryTerm term = ExpressionToQueryTermVisitor.Translate(expr, T());
            term.Should().BeOfType<GreaterOrEqualTerm>();
            term.Value.Should().Be(42);
        }

        // ── Chained composition ───────────────────────────────────────────────────────

        [Fact]
        public void AndAndOr_ProducesCorrectTree()
        {
            Expression<Func<Product, bool>> expr =
                p => (p.Name == "A" || p.Name == "B") && p.Quantity > 0;
            QueryTerm term = ExpressionToQueryTermVisitor.Translate(expr, T());
            term.Should().BeOfType<AndTerm>();
        }

        // ── Null guards ───────────────────────────────────────────────────────────────

        [Fact]
        public void Translate_NullPredicate_Throws()
        {
            Action act = () => ExpressionToQueryTermVisitor.Translate(null, T());
            act.Should().Throw<ArgumentNullException>().WithParameterName("predicate");
        }

        [Fact]
        public void Translate_NullTemplate_Throws()
        {
            Expression<Func<Product, bool>> expr = p => p.Name == "X";
            Action act = () => ExpressionToQueryTermVisitor.Translate(expr, null);
            act.Should().Throw<ArgumentNullException>().WithParameterName("template");
        }

        // ── Unsupported expression ────────────────────────────────────────────────────

        [Fact]
        public void Translate_UnsupportedNodeType_Throws()
        {
            // p.Quantity + 1 == 5 — the Add node is not a direct field access
            Expression<Func<Product, bool>> expr = p => p.Quantity + 1 == 5;
            Action act = () => ExpressionToQueryTermVisitor.Translate(expr, T());
            act.Should().Throw<NotSupportedException>();
        }
    }
}
