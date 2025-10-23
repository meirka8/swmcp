namespace swmcp.server.Models
{
    public class Feature
    {
        public string Name { get; set; }
        public string TypeName { get; set; }
        public FeatureData? Data { get; set; }

        public Feature(string name, string typeName)
        {
            Name = name;
            TypeName = typeName;
        }
    }
}
