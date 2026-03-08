using System;
using System.Data;
using FluentAssertions;
using Moq;
using Turquoise.ORM.Transactions;
using Xunit;

namespace Turquoise.ORM.Tests.Transactions
{
    /// <summary>Tests for With.Transaction static helpers.</summary>
    public class WithTransactionTests
    {
        // ── Helpers ───────────────────────────────────────────────────────────────────

        private static Mock<IUnitOfWork> CreateMockUow(bool throwOnCommit = false)
        {
            var mock = new Mock<IUnitOfWork>(MockBehavior.Strict);
            mock.Setup(u => u.InTransaction).Returns(false);
            mock.Setup(u => u.CreateTransaction(It.IsAny<IsolationLevel>())).Returns((TransactionBase)null);

            if (throwOnCommit)
                mock.Setup(u => u.Commit()).Throws<InvalidOperationException>();
            else
                mock.Setup(u => u.Commit()).Verifiable();

            mock.Setup(u => u.Rollback()).Verifiable();
            mock.Setup(u => u.Dispose()).Verifiable();
            return mock;
        }

        // ── Action overload ───────────────────────────────────────────────────────────

        [Fact]
        public void Transaction_Action_CommitsOnSuccess()
        {
            var mock   = CreateMockUow();
            bool ran   = false;

            With.Transaction(mock.Object, () => { ran = true; });

            ran.Should().BeTrue();
            mock.Verify(u => u.Commit(), Times.Once);
            mock.Verify(u => u.Rollback(), Times.Never);
        }

        [Fact]
        public void Transaction_Action_RollsBackOnException()
        {
            var mock = CreateMockUow();
            mock.Setup(u => u.Rollback()).Verifiable();

            Action act = () => With.Transaction(mock.Object, () => throw new InvalidOperationException("boom"));

            act.Should().Throw<InvalidOperationException>().WithMessage("boom");
            mock.Verify(u => u.Rollback(), Times.Once);
            mock.Verify(u => u.Commit(), Times.Never);
        }

        [Fact]
        public void Transaction_Action_PassesIsolationLevel()
        {
            IsolationLevel captured = IsolationLevel.Unspecified;
            var mock = new Mock<IUnitOfWork>(MockBehavior.Strict);
            mock.Setup(u => u.CreateTransaction(It.IsAny<IsolationLevel>()))
                .Callback<IsolationLevel>(l => captured = l)
                .Returns((TransactionBase)null);
            mock.Setup(u => u.Commit()).Verifiable();
            mock.Setup(u => u.Dispose()).Verifiable();

            With.Transaction(mock.Object, () => { }, IsolationLevel.Serializable);

            captured.Should().Be(IsolationLevel.Serializable);
        }

        // ── Func<T> overload ──────────────────────────────────────────────────────────

        [Fact]
        public void Transaction_Func_ReturnsValueAndCommits()
        {
            var mock = CreateMockUow();

            int result = With.Transaction<int>(mock.Object, () => 42);

            result.Should().Be(42);
            mock.Verify(u => u.Commit(), Times.Once);
        }

        [Fact]
        public void Transaction_Func_RollsBackOnException()
        {
            var mock = CreateMockUow();

            Action act = () => With.Transaction<int>(mock.Object, () => throw new ApplicationException("fail"));

            act.Should().Throw<ApplicationException>();
            mock.Verify(u => u.Rollback(), Times.Once);
            mock.Verify(u => u.Commit(), Times.Never);
        }

        // ── Null guard ────────────────────────────────────────────────────────────────

        [Fact]
        public void Transaction_NullUow_Throws()
        {
            Action act = () => With.Transaction((IUnitOfWork)null, () => { });
            act.Should().Throw<ArgumentNullException>().WithParameterName("uow");
        }

        [Fact]
        public void Transaction_NullAction_Throws()
        {
            var mock = CreateMockUow();
            Action act = () => With.Transaction(mock.Object, (Action)null);
            act.Should().Throw<ArgumentNullException>().WithParameterName("action");
        }

        // ── Service locator overload ──────────────────────────────────────────────────

        [Fact]
        public void Transaction_ResolvesUowFromLocator()
        {
            var mock = CreateMockUow();
            mock.Setup(u => u.Dispose()).Verifiable();

            TurquoiseServiceLocator.SetUnitOfWorkFactory(() => mock.Object);
            try
            {
                bool ran = false;
                With.Transaction(() => { ran = true; });
                ran.Should().BeTrue();
                mock.Verify(u => u.Commit(), Times.Once);
            }
            finally
            {
                TurquoiseServiceLocator.Reset();
            }
        }

        // ── Isolation-level shorthands ────────────────────────────────────────────────

        [Fact]
        public void SerializableTransaction_PassesSerializableIsolation()
        {
            IsolationLevel captured = IsolationLevel.Unspecified;
            var mock = new Mock<IUnitOfWork>(MockBehavior.Strict);
            mock.Setup(u => u.CreateTransaction(It.IsAny<IsolationLevel>()))
                .Callback<IsolationLevel>(l => captured = l)
                .Returns((TransactionBase)null);
            mock.Setup(u => u.Commit()).Verifiable();
            mock.Setup(u => u.Dispose()).Verifiable();

            With.SerializableTransaction(mock.Object, () => { });

            captured.Should().Be(IsolationLevel.Serializable);
        }
    }
}
