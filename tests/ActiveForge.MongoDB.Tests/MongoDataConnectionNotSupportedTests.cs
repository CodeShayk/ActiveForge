using System;
using FluentAssertions;
using ActiveForge;
using Xunit;

namespace ActiveForge.MongoDB.Tests
{
    /// <summary>
    /// Verifies that SQL-specific operations throw <see cref="NotSupportedException"/>
    /// when called on a <see cref="MongoDataConnection"/>.
    /// </summary>
    public class MongoDataConnectionNotSupportedTests
    {
        private readonly MongoDataConnection _conn =
            new MongoDataConnection("mongodb://localhost:27017", "testdb");

        [Fact]
        public void ExecSQL_String_ThrowsNotSupported()
        {
            var p = new MongoTestProduct(_conn);
            Action act = () => _conn.ExecSQL(p, "SELECT 1");
            act.Should().Throw<NotSupportedException>();
        }

        [Fact]
        public void ExecSQL_StringParams_ThrowsNotSupported()
        {
            var p = new MongoTestProduct(_conn);
            Action act = () => _conn.ExecSQL(p, "SELECT {0}", 1);
            act.Should().Throw<NotSupportedException>();
        }

        [Fact]
        public void ExecSQL_StringDictionary_ThrowsNotSupported()
        {
            var p = new MongoTestProduct(_conn);
            Action act = () => _conn.ExecSQL(p, "SELECT 1", new System.Collections.Generic.Dictionary<string, object>());
            act.Should().Throw<NotSupportedException>();
        }

        [Fact]
        public void ExecSQL_StringStartCount_ThrowsNotSupported()
        {
            var p = new MongoTestProduct(_conn);
            Action act = () => _conn.ExecSQL(p, "SELECT 1", 0, 10);
            act.Should().Throw<NotSupportedException>();
        }

        [Fact]
        public void ExecSQL_StringStartCountParams_ThrowsNotSupported()
        {
            var p = new MongoTestProduct(_conn);
            Action act = () => _conn.ExecSQL(p, "SELECT 1", 0, 10,
                new System.Collections.Generic.Dictionary<string, object>());
            act.Should().Throw<NotSupportedException>();
        }

        [Fact]
        public void ExecSQL_RawString_ThrowsNotSupported()
        {
            Action act = () => _conn.ExecSQL("SELECT 1");
            act.Should().Throw<NotSupportedException>();
        }

        [Fact]
        public void ExecSQL_RawStringWithParams_ThrowsNotSupported()
        {
            Action act = () => _conn.ExecSQL("SELECT 1",
                new System.Collections.Generic.Dictionary<string, CommandBase.Parameter>());
            act.Should().Throw<NotSupportedException>();
        }

        [Fact]
        public void ExecStoredProcedure_ThrowsNotSupported()
        {
            var p = new MongoTestProduct(_conn);
            Action act = () => _conn.ExecStoredProcedure(p, "sp_test", 0, 10);
            act.Should().Throw<NotSupportedException>();
        }

        [Fact]
        public void GetDynamicObjectBinding_ThrowsNotSupported()
        {
            var p = new MongoTestProduct(_conn);
            Action act = () => _conn.GetDynamicObjectBinding(p, null);
            act.Should().Throw<NotSupportedException>();
        }

        [Fact]
        public void GetTargetFieldInfoByClassSource_ThrowsNotSupported()
        {
            Action act = () => _conn.GetTargetFieldInfo("MyClass", "myTable", "myField");
            act.Should().Throw<NotSupportedException>();
        }

        [Fact]
        public void GetTargetFieldInfoBySource_ThrowsNotSupported()
        {
            Action act = () => _conn.GetTargetFieldInfo("myTable");
            act.Should().Throw<NotSupportedException>();
        }
    }
}
