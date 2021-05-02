using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Foster.Framework
{
    /// <summary>
    /// A Shader used for Rendering
    /// </summary>
    public class Shader : IDisposable
    {

        public abstract class Platform
        {
            protected internal Dictionary<string, ShaderAttribute> Attributes { get; } = new Dictionary<string, ShaderAttribute>();
            protected internal Dictionary<string, ShaderUniform> Uniforms { get; } = new Dictionary<string, ShaderUniform>();
            protected internal abstract void Dispose();
        }

        /// <summary>
        /// A reference to the internal platform implementation of the Shader
        /// </summary>
        public Platform Implementation { get; }

        /// <summary>
        /// List of all Vertex Attributes, by Name
        /// </summary>
        public ReadOnlyDictionary<string, ShaderAttribute> Attributes { get; }

        /// <summary>
        /// List of all Uniforms, by Name
        /// </summary>
        public ReadOnlyDictionary<string, ShaderUniform> Uniforms { get; }

        public Shader(Graphics graphics, ShaderSource source)
        {
            Implementation = graphics.CreateShader(source);
            Uniforms = new ReadOnlyDictionary<string, ShaderUniform>(Implementation.Uniforms);
            Attributes = new ReadOnlyDictionary<string, ShaderAttribute>(Implementation.Attributes);
        }

        public Shader(ShaderSource source) : this(App.Graphics, source)
        {
        }

        public void Dispose()
        {
            Implementation.Dispose();
        }

    }
}
