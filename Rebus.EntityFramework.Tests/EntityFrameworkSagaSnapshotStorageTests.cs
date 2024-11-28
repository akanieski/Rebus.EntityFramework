using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Rebus.Exceptions;
using Rebus.Logging;
using Rebus.Sagas;

namespace Rebus.EntityFramework.Tests
{
    [TestClass]
    public class EntityFrameworkSagaSnapshotStorageTests
    {
        private RebusDbContext _dbContext;
        private EntityFrameworkSagaSnapshotStorage _snapshotStorage;
        private Mock<ILog> _loggerMock;
        [TestInitialize]
        public void Setup()
        {
            _loggerMock = new Mock<ILog>();
            _dbContext = new RebusDbContext(_loggerMock.Object, o => o
                .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
                .UseInMemoryDatabase(databaseName: "TestDatabase"));
            _snapshotStorage = new EntityFrameworkSagaSnapshotStorage(_dbContext);
        }

        [TestMethod]
        public async Task Save_ShouldAddSnapshot_WhenNewSagaDataProvided()
        {
            // Arrange
            var sagaData = new TestSagaData { Id = Guid.NewGuid(), Revision = 0 };
            var sagaAuditMetadata = new Dictionary<string, string> { { "Key", "Value" } };

            // Act
            await _snapshotStorage.Save(sagaData, sagaAuditMetadata);

            // Assert
            var snapshot = await _dbContext.SagaSnapshots.SingleOrDefaultAsync(s => s.Id == sagaData.Id);
            Assert.IsNotNull(snapshot);
            Assert.AreEqual(sagaData.Id, snapshot.Id);
        }

        [TestMethod]
        public async Task Save_ShouldUpdateSnapshot_WhenExistingInitialSagaDataSnapshotProvided()
        {
            // Arrange
            var sagaData = new TestSagaData { Id = Guid.NewGuid(), Revision = 0 };
            var sagaAuditMetadata = new Dictionary<string, string> { { "Key", "Value" } };
            var existingSnapshot = new SagaSnapshot
            {
                Id = sagaData.Id,
                Revision = sagaData.Revision,
                Data = System.Text.Encoding.UTF8.GetBytes(JsonSerializer.Serialize(sagaData)),
                Metadata = JsonSerializer.Serialize(sagaAuditMetadata)
            };

            _dbContext.SagaSnapshots.Add(existingSnapshot);
            await _dbContext.SaveChangesAsync();

            // Act
            await _snapshotStorage.Save(sagaData, sagaAuditMetadata);

            // Assert
            var snapshot = await _dbContext.SagaSnapshots.SingleOrDefaultAsync(s => s.Id == sagaData.Id && s.Revision == sagaData.Revision);
            Assert.IsNotNull(snapshot);
            Assert.AreEqual(sagaData.Id, snapshot.Id);
        }

        [TestMethod]
        public async Task Save_ShouldThrowException_WhenDuplicateRevisionProvided()
        {
            // Arrange
            var sagaData = new TestSagaData { Id = Guid.NewGuid(), Revision = 1 };
            var sagaAuditMetadata = new Dictionary<string, string> { { "Key", "Value" } };
            var existingSnapshot = new SagaSnapshot
            {
                Id = sagaData.Id,
                Revision = sagaData.Revision,
                Data = System.Text.Encoding.UTF8.GetBytes(JsonSerializer.Serialize(sagaData)),
                Metadata = JsonSerializer.Serialize(sagaAuditMetadata)
            };

            _dbContext.SagaSnapshots.Add(existingSnapshot);
            await _dbContext.SaveChangesAsync();

            // Act & Assert
            await Assert.ThrowsExceptionAsync<RebusApplicationException>(() => _snapshotStorage.Save(sagaData, sagaAuditMetadata));
        }
    }
}