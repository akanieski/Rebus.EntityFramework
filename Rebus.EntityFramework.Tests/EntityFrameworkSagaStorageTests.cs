using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Rebus.EntityFramework.Sagas;
using Rebus.Injection;
using Rebus.Logging;
using Rebus.Sagas;
using SagaModel = Rebus.EntityFramework.Sagas.Saga;

namespace Rebus.EntityFramework.Tests
{
    [TestClass]
    public class EntityFrameworkSagaStorageTests
    {
        private SagasDbContext _dbContext;
        private EntityFrameworkSagaStorage _sagaStorage;
        private Mock<ILog> _loggerMock;
        private Mock<SagasDbContextFactory> _dbContextFactoryMock;
        [TestInitialize]
        public void Setup()
        {
            _loggerMock = new Mock<ILog>();
            _dbContext = new SagasDbContext(_loggerMock.Object, o => o
                .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
                .UseInMemoryDatabase(databaseName: "TestDatabase"));
            
            
            // Arrange
            var resolutionContextMock = new Mock<IResolutionContext>();
            var loggerFactoryMock = new Mock<IRebusLoggerFactory>();
            var loggerMock = new Mock<ILog>();
            resolutionContextMock.Setup(rc => rc.Get<IRebusLoggerFactory>()).Returns(loggerFactoryMock.Object);
            loggerFactoryMock.Setup(lf => lf.GetLogger<SagasDbContext>()).Returns(loggerMock.Object);

            var optionsBuilderSetup = new Action<DbContextOptionsBuilder>(options => options
                .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
                .UseInMemoryDatabase("Test"));
            var factory = new SagasDbContextFactory(resolutionContextMock.Object, optionsBuilderSetup);
            
            _dbContext = factory.Create();
            _sagaStorage = new EntityFrameworkSagaStorage(factory);
        }

        [TestMethod]
        public async Task Find_ShouldReturnSagaData_WhenSagaExists()
        {
            // Arrange
            var sagaDataType = typeof(TestSagaData);
            var propertyName = "Id";
            var propertyValue = Guid.NewGuid();
            var sagaData = new TestSagaData { Id = propertyValue, Revision = 0 };
            var saga = new SagaModel { Id = propertyValue, Data = System.Text.Encoding.UTF8.GetBytes(JsonSerializer.Serialize(sagaData)) };

            _dbContext.Sagas.Add(saga);
            await _dbContext.SaveChangesAsync();

            // Act
            var result = await _sagaStorage.Find(sagaDataType, propertyName, propertyValue);

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual(sagaData.Id, result.Id);
        }

        [TestMethod]
        public async Task Insert_ShouldAddSagaData_WhenValidSagaDataProvided()
        {
            // Arrange
            var sagaData = new TestSagaData { Id = Guid.NewGuid(), Revision = 0 };
            var correlationProperties = new List<ISagaCorrelationProperty>();

            // Act
            await _sagaStorage.Insert(sagaData, correlationProperties);

            // Assert
            var insertedSaga = await _dbContext.Sagas.SingleOrDefaultAsync(s => s.Id == sagaData.Id);
            Assert.IsNotNull(insertedSaga);
        }

        [TestMethod]
        public async Task Update_ShouldUpdateSagaData_WhenValidSagaDataProvided()
        {
            // Arrange
            var sagaData = new TestSagaData { Id = Guid.NewGuid(), Revision = 0 };
            var correlationProperties = new List<ISagaCorrelationProperty>();
            var saga = new SagaModel { Id = sagaData.Id, Data = System.Text.Encoding.UTF8.GetBytes(JsonSerializer.Serialize(sagaData)) };

            _dbContext.Sagas.Add(saga);
            await _dbContext.SaveChangesAsync();

            // Act
            await _sagaStorage.Update(sagaData, correlationProperties);

            // Assert
            var updatedSaga = await _dbContext.Sagas.SingleOrDefaultAsync(s => s.Id == sagaData.Id);
            Assert.IsNotNull(updatedSaga);
        }

        [TestMethod]
        public async Task Delete_ShouldRemoveSagaData_WhenValidSagaDataProvided()
        {
            // Arrange
            var sagaData = new TestSagaData { Id = Guid.NewGuid(), Revision = 0 };
            var saga = new SagaModel { Id = sagaData.Id, Data = System.Text.Encoding.UTF8.GetBytes(JsonSerializer.Serialize(sagaData)) };

            _dbContext.Sagas.Add(saga);
            await _dbContext.SaveChangesAsync();

            // Act
            await _sagaStorage.Delete(sagaData);

            // Assert
            var deletedSaga = await _dbContext.Sagas.SingleOrDefaultAsync(s => s.Id == sagaData.Id);
            Assert.IsNull(deletedSaga);
        }
    }

    public class TestSagaData : ISagaData
    {
        public Guid Id { get; set; }
        public int Revision { get; set; }
    }
}