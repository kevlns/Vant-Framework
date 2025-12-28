using System;

namespace Vant.System.GM
{
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    public class GMCommandAttribute : Attribute
    {
        public string Name { get; private set; }
        public string Description { get; private set; }

        public GMCommandAttribute(string name, string description = "")
        {
            Name = name;
            Description = description;
        }
    }
}
