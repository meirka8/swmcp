using System.Runtime.InteropServices;
using System.Text.Json;
using SwBridge;
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
            Default = defaultJson == null ? null : Parse(defaultJson),
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

            var (positional, boundArgs, error) = OperationRunner.Bind(recipe, args);

            Assert.Null(error);
            Assert.Equal(3, positional.Length);
            Assert.Equal(0.005, (double)positional[0]!, 9);
            Assert.Equal(true, positional[1]);
            Assert.Equal(1, positional[2]);

            // B1: boundArgs echoes the final SI value actually sent to COM.
            Assert.Equal(0.005, (double)boundArgs["depth"]!, 9);
            Assert.Equal(true, boundArgs["reverse"]);
            Assert.Equal(1, boundArgs["endCondition"]);
        }

        [Fact]
        public void Bind_MissingRequiredParam_ReturnsError()
        {
            var recipe = new OperationRecipe
            {
                Params = new List<OperationParam> { Param("depth1", "length", required: true) },
            };

            var (_, _, error) = OperationRunner.Bind(recipe, args: null);

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

            var (positional, boundArgs, error) = OperationRunner.Bind(recipe, args: null);

            Assert.Null(error);
            Assert.Equal(true, positional[0]);
            Assert.Equal(true, boundArgs["merge"]);
        }

        [Fact]
        public void Bind_ComNullParam_BindsDispatchWrapperOfNull()
        {
            var recipe = new OperationRecipe
            {
                Params = new List<OperationParam> { Param("callout", "comNull") },
            };

            var (positional, boundArgs, error) = OperationRunner.Bind(recipe, args: null);

            Assert.Null(error);
            var wrapper = Assert.IsType<DispatchWrapper>(positional[0]);
            Assert.Null(wrapper.WrappedObject);
            Assert.True(boundArgs.ContainsKey("callout"));
            Assert.Null(boundArgs["callout"]);
        }

        // Code review H3 sidebar: a caller supplying a value for a comNull
        // param has misunderstood something (it can only ever be null) and
        // must be told, not silently ignored.
        [Fact]
        public void Bind_ComNullParam_SuppliedValue_IsRejected()
        {
            var recipe = new OperationRecipe
            {
                Name = "test_op",
                Params = new List<OperationParam> { Param("callout", "comNull") },
            };

            var args = new Dictionary<string, JsonElement> { ["callout"] = Parse("\"whatever\"") };

            var (_, _, error) = OperationRunner.Bind(recipe, args);

            Assert.NotNull(error);
            Assert.Contains("callout", error);
            Assert.Contains("comNull", error);
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

            var (positional, _, error) = OperationRunner.Bind(recipe, args: null);

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

            var (_, _, error) = OperationRunner.Bind(recipe, args);

            Assert.NotNull(error);
            Assert.Contains("radius", error);
            Assert.Contains("Unknown length unit", error);
        }

        // A recipe's own declared default is already canonical SI (the same
        // trust boundary as its target/member) — B1's unit requirement is
        // about what a CALLER sends, not about the recipe author's own data.
        // select_by_ray's real seed default ("radius": 0.0005) is exactly this
        // shape: a non-zero bare-number default that must NOT be rejected.
        [Fact]
        public void Bind_BareNumberLengthDefault_IsUsedAsCanonicalSi_NotRejected()
        {
            var recipe = new OperationRecipe
            {
                Params = new List<OperationParam> { Param("radius", "length", defaultJson: "0.0005") },
            };

            var (positional, boundArgs, error) = OperationRunner.Bind(recipe, args: null);

            Assert.Null(error);
            Assert.Equal(0.0005, (double)positional[0]!, 9);
            Assert.Equal(0.0005, (double)boundArgs["radius"]!, 9);
        }

        [Fact]
        public void Bind_BareNumberLength_IsRejected()
        {
            var recipe = new OperationRecipe
            {
                Name = "extrude_boss",
                Params = new List<OperationParam> { Param("depth1", "length", required: true) },
            };

            // UAT B1's exact repro: a bare number silently meaning meters.
            var args = new Dictionary<string, JsonElement> { ["depth1"] = Parse("40") };

            var (_, _, error) = OperationRunner.Bind(recipe, args);

            Assert.NotNull(error);
            Assert.Contains("depth1", error);
            Assert.Contains("unit", error);
        }

        // H3 / UAT B2: the exact repro — a typo'd param name used to be
        // silently dropped and the real param fell back to its default.
        [Fact]
        public void Bind_UnknownArgumentKey_IsRejected_NamingDeclaredParams()
        {
            var recipe = new OperationRecipe
            {
                Name = "extrude_boss",
                Params = new List<OperationParam>
                {
                    Param("depth1", "length", required: true),
                    Param("reverseDirection", "bool", defaultJson: "false"),
                },
            };

            var args = new Dictionary<string, JsonElement>
            {
                ["depth1"] = Parse("\"6 mm\""),
                ["thickness"] = Parse("\"2 mm\""), // not a declared param — typo/hallucination
            };

            var (positional, _, error) = OperationRunner.Bind(recipe, args);

            Assert.NotNull(error);
            Assert.Contains("thickness", error);
            Assert.Contains("depth1", error); // declared param list is echoed
            Assert.Contains("reverseDirection", error);
            Assert.Empty(positional);
        }

        [Fact]
        public void Bind_UnknownArgumentKey_CaseInsensitiveMatchIsAccepted()
        {
            var recipe = new OperationRecipe
            {
                Params = new List<OperationParam> { Param("mark", "int", defaultJson: "0") },
            };

            var args = new Dictionary<string, JsonElement> { ["Mark"] = Parse("16") };

            var (positional, _, error) = OperationRunner.Bind(recipe, args);

            Assert.Null(error);
            Assert.Equal(16, positional[0]);
        }

        [Fact]
        public void ConvertParam_Angle_DegreesString_ConvertsToRadians()
        {
            var p = Param("draftAngle1", "angle");
            var (value, error) = OperationRunner.ConvertParam(p, Parse("\"5 deg\""));

            Assert.Null(error);
            Assert.Equal(5.0 * Math.PI / 180.0, (double)value!, 9);
        }

        // B5: the new returnEquals verify predicate — status-code returns
        // (e.g. SaveAs3's swFileSaveError_e, 0 = success) can now be verified.
        [Fact]
        public void EvaluateVerify_ReturnEquals_PassesWhenValuesMatch()
        {
            var check = new VerifyCheck { Check = "returnEquals", Expected = Parse("0") };
            var failures = new List<string>();

            OperationRunner.EvaluateVerify(check, doc: null, InvokeOutcome.Ok(0), preFeatureCount: null, preSketchSegCount: null, failures);

            Assert.Empty(failures);
        }

        [Fact]
        public void EvaluateVerify_ReturnEquals_FailsWhenValuesDiffer()
        {
            var check = new VerifyCheck { Check = "returnEquals", Expected = Parse("0") };
            var failures = new List<string>();

            OperationRunner.EvaluateVerify(check, doc: null, InvokeOutcome.Ok(2), preFeatureCount: null, preSketchSegCount: null, failures);

            Assert.Single(failures);
            Assert.Contains("returnEquals", failures[0]);
        }

        [Fact]
        public void EvaluateVerify_ReturnEquals_MissingExpected_Fails()
        {
            var check = new VerifyCheck { Check = "returnEquals" };
            var failures = new List<string>();

            OperationRunner.EvaluateVerify(check, doc: null, InvokeOutcome.Ok(0), preFeatureCount: null, preSketchSegCount: null, failures);

            Assert.Single(failures);
            Assert.Contains("expected", failures[0], StringComparison.OrdinalIgnoreCase);
        }
    }
}
