namespace Foster.Framework
{
    /// <summary>
    /// A Shader Attribute
    /// </summary>
    public class ShaderAttribute
    {
        /// <summary>
        /// The name of the Attribute
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// The Location of the Attribute in the Shader
        /// </summary>
        public uint Location { get; }

        public ShaderAttribute(string name, uint location)
        {
            Name = name;
            Location = location;
        }
    }
}
