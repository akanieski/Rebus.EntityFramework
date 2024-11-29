using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Rebus.Injection;
using Rebus.Logging;
using Rebus.EntityFramework.Sagas;
using System;
using Microsoft.EntityFrameworkCore;
using Rebus.EntityFramework.Transports;

namespace Rebus.EntityFramework.Tests
{
    [TestClass]
    public class TransportsDbContextFactoryTests
    {
        [TestMethod]
        public void Create_ShouldInitializeDbContext_WhenCalledFirstTime()
        {
            // Arrange
            var resolutionContextMock = new Mock<IResolutionContext>();
            var loggerFactoryMock = new Mock<IRebusLoggerFactory>();
            var loggerMock = new Mock<ILog>();
            resolutionContextMock.Setup(rc => rc.Get<IRebusLoggerFactory>()).Returns(loggerFactoryMock.Object);
            loggerFactoryMock.Setup(lf => lf.GetLogger<TransportsDbContext>()).Returns(loggerMock.Object);

            var optionsBuilderSetup = new Action<DbContextOptionsBuilder>(options => options.UseInMemoryDatabase("Test"));
            var factory = new TransportsDbContextFactory(resolutionContextMock.Object, optionsBuilderSetup);

            // Act
            var dbContext = factory.Create();

            // Assert
            Assert.IsNotNull(dbContext);
            resolutionContextMock.Verify(rc => rc.Get<IRebusLoggerFactory>(), Times.Once);
            loggerFactoryMock.Verify(lf => lf.GetLogger<TransportsDbContext>(), Times.Once);
        }
    }
}