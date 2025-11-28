using System.ComponentModel;
using ModelContextProtocol.Server;
using swmcp.server.Controllers;
using swmcp.server.Services;
using System.Collections.Generic;

namespace swmcp.server.Tools
{
    [McpServerToolType]
    public class SolidWorksTool
    {
        private readonly SolidWorksController _solidWorksController;
        private readonly SchemaManager _schemaManager;

        public SolidWorksTool(SolidWorksController solidWorksController, SchemaManager schemaManager)
        {
            _solidWorksController = solidWorksController;
            _schemaManager = schemaManager;
        }

        [McpServerTool, Description("Gets information about the currently open SolidWorks part.")]
        public object? GetPartInfo()
        {
            var doc = _solidWorksController.GetActiveDocument();
            if (doc == null)
            {
                return new { error = "No active SolidWorks document." };
            }

            return _solidWorksController.GetPartInfo(doc);
        }

        [McpServerTool, Description("Registers a new feature schema for the server to learn.")]
        public string RegisterFeatureSchema(string featureType, string[] propertyNames)
        {
            _schemaManager.RegisterSchema(featureType, new List<string>(propertyNames));
            return $"Schema for '{featureType}' registered successfully.";
        }
    }
}
