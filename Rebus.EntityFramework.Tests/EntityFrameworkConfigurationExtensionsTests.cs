using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Rebus.Auditing.Sagas;
using Rebus.Config;
using Rebus.EntityFramework.Config;
using Rebus.Injection;
using Rebus.Logging;

namespace Rebus.EntityFramework.Tests;

[TestClass]
public class EntityFrameworkConfigurationExtensionsTests
{
    [TestMethod]
    public void SetupRebusContext_ShouldInitializeDbContext()
    {
        // Arrange
        var loggerFactoryMock = new Mock<IRebusLoggerFactory>();
        var loggerMock = new Mock<Rebus.Logging.ILog>();
        loggerFactoryMock.Setup(lf => lf.GetLogger<RebusDbContext>()).Returns(loggerMock.Object);
        var resolutionContextMock = new Mock<IResolutionContext>();
        resolutionContextMock.Setup(c => c.Get<IRebusLoggerFactory>()).Returns(loggerFactoryMock.Object);

        Action<DbContextOptionsBuilder> optionsBuilderSetup = options => options.UseInMemoryDatabase("TestDatabase");

        // Act
        var dbContext = EntityFrameworkConfigurationExtensions.SetupRebusContext(resolutionContextMock.Object, optionsBuilderSetup);

        // Assert
        Assert.IsNotNull(dbContext);
        Assert.IsInstanceOfType(dbContext, typeof(RebusDbContext));
        Assert.IsTrue(dbContext.Initialized);
    }
}