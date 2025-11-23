using System.Collections.Generic;

namespace swmcp.server.Models
{
    public class Feature
    {
        public string Name { get; set; }
        public string TypeName { get; set; }
        public Dictionary<string, object>? Data { get; set; }
        public bool Known { get; set; } = true;

        public Feature(string name, string typeName)
        {
            Name = name;
            TypeName = typeName;
        }
    }
}
