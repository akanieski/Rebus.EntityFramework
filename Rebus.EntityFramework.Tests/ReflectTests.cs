using Rebus.EntityFramework.Reflection;

namespace Rebus.EntityFramework.Tests;
using System;
using System.Linq.Expressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

[TestClass]
public class ReflectTests
{
    private class TestClass
    {
        public string Property1 { get; set; }
        public NestedClass Property2 { get; set; }
    }

    private class NestedClass
    {
        public string NestedProperty { get; set; }
    }

    [TestMethod]
    public void Path_ShouldReturnPropertyName()
    {
        Expression<Func<TestClass, object>> expression = x => x.Property1;
        var result = Reflect.Path(expression);
        Assert.AreEqual("Property1", result);
    }

    [TestMethod]
    public void Path_ShouldReturnNestedPropertyName()
    {
        Expression<Func<TestClass, object>> expression = x => x.Property2.NestedProperty;
        var result = Reflect.Path(expression);
        Assert.AreEqual("Property2.NestedProperty", result);
    }

    [TestMethod]
    public void Value_ShouldReturnPropertyValue()
    {
        var obj = new TestClass { Property1 = "Value1" };
        var result = Reflect.Value(obj, "Property1");
        Assert.AreEqual("Value1", result);
    }

    [TestMethod]
    public void Value_ShouldReturnNestedPropertyValue()
    {
        var obj = new TestClass { Property2 = new NestedClass { NestedProperty = "NestedValue" } };
        var result = Reflect.Value(obj, "Property2.NestedProperty");
        Assert.AreEqual("NestedValue", result);
    }

    [TestMethod]
    public void Value_ShouldReturnNullForInvalidPath()
    {
        var obj = new TestClass { Property1 = "Value1" };
        var result = Reflect.Value(obj, "InvalidProperty");
        Assert.IsNull(result);
    }
}
