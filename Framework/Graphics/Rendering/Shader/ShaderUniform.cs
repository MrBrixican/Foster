namespace Foster.Framework
{
    /// <summary>
    /// A Shader Uniform Value
    /// </summary>
    public class ShaderUniform
    {
        /// <summary>
        /// The Name of the Uniform
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// The Location of the Uniform in the Shader
        /// </summary>
        public int Location { get; }

        /// <summary>
        /// The Array length of the uniform
        /// </summary>
        public int Length { get; }

        /// <summary>
        /// The Type of Uniform
        /// </summary>
        public UniformType Type { get; }

        public ShaderUniform(string name, int location, int length, UniformType type)
        {
            Name = name;
            Location = location;
            Length = length;
            Type = type;
        }
    }
}
