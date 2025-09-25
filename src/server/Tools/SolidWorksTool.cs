using System.ComponentModel;
using ModelContextProtocol.Server;
using swmcp.server.Controllers;

namespace swmcp.server.Tools
{
    [McpServerToolType]
    public class SolidWorksTool
    {
        private readonly SolidWorksController _solidWorksController;

        public SolidWorksTool(SolidWorksController solidWorksController)
        {
            _solidWorksController = solidWorksController;
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
    }
}
