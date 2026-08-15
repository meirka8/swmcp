using System.Text.Json;
using swmcp.server.Services;
using Xunit;

namespace swmcp.server.tests
{
    public class UnitParserTests
    {
        private static JsonElement Parse(string json) => JsonDocument.Parse(json).RootElement.Clone();

        [Fact]
        public void Length_BareNumber_IsMeters()
        {
            Assert.True(UnitParser.TryParseLength(Parse("0.005"), out var meters, out var error));
            Assert.Null(error);
            Assert.Equal(0.005, meters, 9);
        }

        [Theory]
        [InlineData("\"5 mm\"", 0.005)]
        [InlineData("\"5mm\"", 0.005)]
        [InlineData("\"1 cm\"", 0.01)]
        [InlineData("\"1 in\"", 0.0254)]
        [InlineData("\"0.5 m\"", 0.5)]
        [InlineData("\"20\"", 20.0)] // bare-number string, no unit suffix -> canonical unit (meters)
        public void Length_QuantityString_ConvertsToMeters(string json, double expectedMeters)
        {
            Assert.True(UnitParser.TryParseLength(Parse(json), out var meters, out var error));
            Assert.Null(error);
            Assert.Equal(expectedMeters, meters, 9);
        }

        [Fact]
        public void Angle_BareNumber_IsRadians()
        {
            Assert.True(UnitParser.TryParseAngle(Parse("1.5707963268"), out var radians, out var error));
            Assert.Null(error);
            Assert.Equal(Math.PI / 2, radians, 6);
        }

        [Theory]
        [InlineData("\"30 deg\"", 30.0)]
        [InlineData("\"90deg\"", 90.0)]
        [InlineData("\"180 degrees\"", 180.0)]
        public void Angle_DegreeString_ConvertsToRadians(string json, double expectedDegrees)
        {
            Assert.True(UnitParser.TryParseAngle(Parse(json), out var radians, out var error));
            Assert.Null(error);
            Assert.Equal(expectedDegrees * Math.PI / 180.0, radians, 9);
        }

        [Fact]
        public void Length_UnknownUnit_Fails()
        {
            Assert.False(UnitParser.TryParseLength(Parse("\"5 furlongs\""), out _, out var error));
            Assert.Contains("Unknown length unit", error);
        }

        [Fact]
        public void Length_Unparsable_Fails()
        {
            Assert.False(UnitParser.TryParseLength(Parse("\"not a number\""), out _, out var error));
            Assert.NotNull(error);
        }

        [Fact]
        public void Length_WrongJsonKind_Fails()
        {
            Assert.False(UnitParser.TryParseLength(Parse("true"), out _, out var error));
            Assert.NotNull(error);
        }
    }
}
