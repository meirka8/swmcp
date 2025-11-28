using System;
using System.Reflection;
using System.Runtime.InteropServices;

namespace swmcp.server.Utilities
{
    public static class ComReflectionHelper
    {
        public static object? GetProperty(object comObject, string propertyName)
        {
            if (comObject == null) return null;

            try
            {
                var type = comObject.GetType();
                return type.InvokeMember(
                    propertyName,
                    BindingFlags.GetProperty | BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase,
                    null,
                    comObject,
                    null);
            }
            catch (Exception ex)
            {
                // Log or handle exception? For now, return null or error string
                // In a real scenario, we might want to distinguish between "Property not found" and "COM Error"
                Console.Error.WriteLine($"Error fetching property '{propertyName}': {ex.Message}");
                return null;
            }
        }
    }
}
