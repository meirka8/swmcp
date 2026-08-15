using System.Runtime.InteropServices;
using System.Text.Json;
using swmcp.server.Models;
using swmcp.server.Services;
using Xunit;

namespace swmcp.server.tests
{
    public class OperationBindingTests
    {
        private static JsonElement Parse(string json) => JsonDocument.Parse(json).RootElement.Clone();

        private static OperationParam Param(string name, string type, string? defaultJson = null, bool required = false) => new()
        {
            Name = name,
            Type = type,
            Required = required,
            Default = defaultJson == null ? default : Parse(defaultJson),
        };

        [Fact]
        public void Bind_UsesSuppliedArgsInDeclarationOrder()
        {
            var recipe = new OperationRecipe
            {
                Params = new List<OperationParam>
                {
                    Param("depth", "length"),
                    Param("reverse", "bool"),
                    Param("endCondition", "enum"),
                },
            };

            var args = new Dictionary<string, JsonElement>
            {
                ["depth"] = Parse("\"5 mm\""),
                ["reverse"] = Parse("true"),
                ["endCondition"] = Parse("1"),
            };

            var (positional, error) = OperationRunner.Bind(recipe, args);

            Assert.Null(error);
            Assert.Equal(3, positional.Length);
            Assert.Equal(0.005, (double)positional[0]!, 9);
            Assert.Equal(true, positional[1]);
            Assert.Equal(1, positional[2]);
        }

        [Fact]
        public void Bind_MissingRequiredParam_ReturnsError()
        {
            var recipe = new OperationRecipe
            {
                Params = new List<OperationParam> { Param("depth1", "length", required: true) },
            };

            var (_, error) = OperationRunner.Bind(recipe, args: null);

            Assert.NotNull(error);
            Assert.Contains("depth1", error);
        }

        [Fact]
        public void Bind_MissingOptionalParam_UsesDeclaredDefault()
        {
            var recipe = new OperationRecipe
            {
                Params = new List<OperationParam> { Param("merge", "bool", defaultJson: "true") },
            };

            var (positional, error) = OperationRunner.Bind(recipe, args: null);

            Assert.Null(error);
            Assert.Equal(true, positional[0]);
        }

        [Fact]
        public void Bind_ComNullParam_AlwaysBindsDispatchWrapperOfNull()
        {
            var recipe = new OperationRecipe
            {
                Params = new List<OperationParam> { Param("callout", "comNull") },
            };

            // Even if a caller somehow supplied a value, comNull params ignore it.
            var args = new Dictionary<string, JsonElement> { ["callout"] = Parse("\"whatever\"") };

            var (positional, error) = OperationRunner.Bind(recipe, args);

            Assert.Null(error);
            var wrapper = Assert.IsType<DispatchWrapper>(positional[0]);
            Assert.Null(wrapper.WrappedObject);
        }

        [Fact]
        public void Bind_UnsuppliedNonRequiredNonDefaultParam_UsesTypeZeroValue()
        {
            var recipe = new OperationRecipe
            {
                Params = new List<OperationParam>
                {
                    Param("x", "double"),
                    Param("count", "int"),
                    Param("label", "string"),
                    Param("flag", "bool"),
                },
            };

            var (positional, error) = OperationRunner.Bind(recipe, args: null);

            Assert.Null(error);
            Assert.Equal(0.0, positional[0]);
            Assert.Equal(0, positional[1]);
            Assert.Equal("", positional[2]);
            Assert.Equal(false, positional[3]);
        }

        [Fact]
        public void Bind_BadLengthString_ReturnsParameterScopedError()
        {
            var recipe = new OperationRecipe
            {
                Params = new List<OperationParam> { Param("radius", "length", required: true) },
            };

            var args = new Dictionary<string, JsonElement> { ["radius"] = Parse("\"5 furlongs\"") };

            var (_, error) = OperationRunner.Bind(recipe, args);

            Assert.NotNull(error);
            Assert.Contains("radius", error);
            Assert.Contains("Unknown length unit", error);
        }

        [Fact]
        public void ConvertParam_Angle_DegreesString_ConvertsToRadians()
        {
            var p = Param("draftAngle1", "angle");
            var (value, error) = OperationRunner.ConvertParam(p, Parse("\"5 deg\""));

            Assert.Null(error);
            Assert.Equal(5.0 * Math.PI / 180.0, (double)value!, 9);
        }
    }
}
