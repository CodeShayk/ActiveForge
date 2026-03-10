using System;
using System.Linq.Expressions;
using FluentAssertions;
using ActiveForge.Linq;
using ActiveForge.Query;
using ActiveForge.Tests.Helpers;
using Xunit;

namespace ActiveForge.Tests.Linq
{
    public class SortVisitorTests
    {
        private static readonly StubDataConnection Conn = new StubDataConnection();

        private sealed class Widget : DataObject
        {
            public TString Name  = new TString();
            public TInt    Stock = new TInt();
            public Widget() : base(Conn) { }
        }

        private static Widget T() => new Widget();

        [Fact]
        public void TranslateAscending_ProducesOrderAscending()
        {
            Expression<Func<Widget, object>> key = w => w.Name;
            SortOrder sort = ExpressionToSortVisitor.TranslateAscending(key, T());
            sort.Should().BeOfType<OrderAscending>();
        }

        [Fact]
        public void TranslateDescending_ProducesOrderDescending()
        {
            Expression<Func<Widget, object>> key = w => w.Name;
            SortOrder sort = ExpressionToSortVisitor.TranslateDescending(key, T());
            sort.Should().BeOfType<OrderDescending>();
        }

        [Fact]
        public void TranslateAscending_IntField_Works()
        {
            Expression<Func<Widget, object>> key = w => w.Stock;
            SortOrder sort = ExpressionToSortVisitor.TranslateAscending(key, T());
            sort.Should().BeOfType<OrderAscending>();
        }

        [Fact]
        public void TranslateAscending_NonField_Throws()
        {
            Expression<Func<Widget, object>> key = w => "constant";
            Action act = () => ExpressionToSortVisitor.TranslateAscending(key, T());
            act.Should().Throw<NotSupportedException>();
        }
    }
}
