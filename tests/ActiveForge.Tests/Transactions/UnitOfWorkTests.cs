using System;
using System.Data;
using FluentAssertions;
using Moq;
using ActiveForge.Transactions;
using Xunit;

namespace ActiveForge.Tests.Transactions
{
    /// <summary>Tests for UnitOfWorkBase lifecycle, depth counter, and rollback-only flag.</summary>
    public class UnitOfWorkTests
    {
        // ── Helpers ───────────────────────────────────────────────────────────────────

        /// <summary>Concrete subclass that uses a mocked TransactionBase.</summary>
        private sealed class TestUnitOfWork : UnitOfWorkBase
        {
            private readonly Func<TransactionBase> _factory;

            public TestUnitOfWork(Func<TransactionBase> factory = null)
                : base(null)
            {
                _factory = factory ?? (() =>
                {
                    var tx = new Mock<TransactionBase>();
                    tx.Setup(t => t.Commit()).Verifiable();
                    tx.Setup(t => t.Rollback()).Verifiable();
                    tx.Setup(t => t.Dispose()).Verifiable();
                    return tx.Object;
                });
            }

            protected override TransactionBase BeginTransactionCore(IsolationLevel level)
                => _factory();
        }

        private static Mock<TransactionBase> CreateMockTransaction()
        {
            var mock = new Mock<TransactionBase>(MockBehavior.Strict);
            mock.Setup(t => t.Commit()).Verifiable();
            mock.Setup(t => t.Rollback()).Verifiable();
            mock.Setup(t => t.Dispose()).Verifiable();
            return mock;
        }

        // ── InTransaction ─────────────────────────────────────────────────────────────

        [Fact]
        public void InTransaction_IsFalse_BeforeAnyTransaction()
        {
            using var uow = new TestUnitOfWork();
            uow.InTransaction.Should().BeFalse();
        }

        [Fact]
        public void InTransaction_IsTrue_AfterCreateTransaction()
        {
            using var uow = new TestUnitOfWork();
            uow.CreateTransaction();
            uow.InTransaction.Should().BeTrue();
        }

        [Fact]
        public void InTransaction_IsFalse_AfterCommit()
        {
            using var uow = new TestUnitOfWork();
            uow.CreateTransaction();
            uow.Commit();
            uow.InTransaction.Should().BeFalse();
        }

        // ── Commit calls transaction ──────────────────────────────────────────────────

        [Fact]
        public void Commit_CallsTransactionCommit_ThenDispose()
        {
            var mock = CreateMockTransaction();
            using var uow = new TestUnitOfWork(() => mock.Object);

            uow.CreateTransaction();
            uow.Commit();

            mock.Verify(t => t.Commit(), Times.Once);
            mock.Verify(t => t.Dispose(), Times.Once);
        }

        // ── Rollback calls transaction ────────────────────────────────────────────────

        [Fact]
        public void Rollback_CallsTransactionRollback_ThenDispose()
        {
            var mock = CreateMockTransaction();
            using var uow = new TestUnitOfWork(() => mock.Object);

            uow.CreateTransaction();
            uow.Rollback();

            mock.Verify(t => t.Rollback(), Times.Once);
            mock.Verify(t => t.Dispose(), Times.Once);
        }

        // ── Nesting ───────────────────────────────────────────────────────────────────

        [Fact]
        public void NestedCreateTransaction_DoesNotStartSecondTransaction()
        {
            int createCount = 0;
            var uow = new TestUnitOfWork(() => { createCount++; return CreateMockTransaction().Object; });

            uow.CreateTransaction();   // depth 1 — creates real TX
            uow.CreateTransaction();   // depth 2 — reuses existing TX
            createCount.Should().Be(1);

            uow.Commit(); // depth 1
            uow.Commit(); // depth 0 — commits the TX
            uow.Dispose();
        }

        [Fact]
        public void NestedCommit_OnlyCommitsAtDepthZero()
        {
            var mock = CreateMockTransaction();
            using var uow = new TestUnitOfWork(() => mock.Object);

            uow.CreateTransaction();
            uow.CreateTransaction();   // depth 2
            uow.Commit();              // depth 1 — NO commit yet
            mock.Verify(t => t.Commit(), Times.Never);
            uow.Commit();              // depth 0 — NOW commits
            mock.Verify(t => t.Commit(), Times.Once);
        }

        // ── Rollback-only ─────────────────────────────────────────────────────────────

        [Fact]
        public void InnerRollback_CausesOuterCommitToRollback()
        {
            var mock = CreateMockTransaction();
            using var uow = new TestUnitOfWork(() => mock.Object);

            uow.CreateTransaction();
            uow.CreateTransaction();
            uow.Rollback();  // inner rolls back — marks rollback-only
            uow.Commit();    // outer tries to commit — must roll back instead

            mock.Verify(t => t.Commit(), Times.Never);
            mock.Verify(t => t.Rollback(), Times.Once);
        }

        // ── Dispose with open transaction ─────────────────────────────────────────────

        [Fact]
        public void Dispose_WithOpenTransaction_RollsBack()
        {
            var mock = CreateMockTransaction();
            var uow  = new TestUnitOfWork(() => mock.Object);

            uow.CreateTransaction();
            uow.Dispose();   // never committed — should roll back

            mock.Verify(t => t.Rollback(), Times.Once);
            mock.Verify(t => t.Dispose(), Times.Once);
        }

        // ── Error cases ───────────────────────────────────────────────────────────────

        [Fact]
        public void Commit_WithoutTransaction_Throws()
        {
            using var uow = new TestUnitOfWork();
            uow.Invoking(u => u.Commit())
               .Should().Throw<InvalidOperationException>();
        }

        [Fact]
        public void Rollback_WithoutTransaction_Throws()
        {
            using var uow = new TestUnitOfWork();
            uow.Invoking(u => u.Rollback())
               .Should().Throw<InvalidOperationException>();
        }

        [Fact]
        public void CreateTransaction_AfterDispose_Throws()
        {
            var uow = new TestUnitOfWork();
            uow.Dispose();
            uow.Invoking(u => u.CreateTransaction())
               .Should().Throw<ObjectDisposedException>();
        }

        // ── IsolationLevel propagated ─────────────────────────────────────────────────

        // Helper with level parameter
        private sealed class TestUnitOfWork2 : UnitOfWorkBase
        {
            private readonly Func<IsolationLevel, TransactionBase> _factory;
            public TestUnitOfWork2(Func<IsolationLevel, TransactionBase> factory) : base(null)
                => _factory = factory;
            protected override TransactionBase BeginTransactionCore(IsolationLevel level)
                => _factory(level);
        }

        [Fact]
        public void CreateTransaction_Serializable_PassesIsolationLevel()
        {
            IsolationLevel captured = IsolationLevel.Unspecified;
            var uow = new TestUnitOfWork2(level =>
            {
                captured = level;
                return CreateMockTransaction().Object;
            });

            uow.CreateTransaction(IsolationLevel.Serializable);
            captured.Should().Be(IsolationLevel.Serializable);
            uow.Rollback();
            uow.Dispose();
        }
    }
}
