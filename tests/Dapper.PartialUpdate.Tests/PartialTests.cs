using Xunit;

namespace Dapper.PartialUpdate.Tests;

public class PartialTests
{
    [Fact]
    public void Constructor_SetsValueAndIsSet()
    {
        // Arrange & Act
        var partial = new Partial<string>("test");

        // Assert
        Assert.True(partial.IsSet);
        Assert.Equal("test", partial.Value);
    }

    [Fact]
    public void ImplicitOperator_SetsValueAndIsSet()
    {
        // Arrange & Act
        Partial<string> partial = "test";

        // Assert
        Assert.True(partial.IsSet);
        Assert.Equal("test", partial.Value);
    }

    [Fact]
    public void ValueSetter_SetsIsSetToTrue()
    {
        // Arrange
        var partial = new Partial<string>();

        // Act
        partial.Value = "new value";

        // Assert
        Assert.True(partial.IsSet);
        Assert.Equal("new value", partial.Value);
    }

    [Fact]
    public void Unset_ResetsValueAndIsSet()
    {
        // Arrange
        var partial = new Partial<string>("test");

        // Act
        partial.Unset();

        // Assert
        Assert.False(partial.IsSet);
    }

    [Fact]
    public void ToString_ReturnsValueWhenSet()
    {
        // Arrange
        var partial = new Partial<int>(42);

        // Act & Assert
        Assert.Equal("42", partial.ToString());
    }

    [Fact]
    public void ToString_ReturnsUnsetWhenNotSet()
    {
        // Arrange
        Partial<int> partial = default;

        // Act & Assert
        Assert.Equal("<unset>", partial.ToString());
    }

    [Fact]
    public void DefaultPartial_IsNotSet()
    {
        // Arrange
        Partial<string> partial = default;

        // Assert
        Assert.False(partial.IsSet);
    }
}
