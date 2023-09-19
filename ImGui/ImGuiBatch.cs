using Foster.Framework;
using System.Diagnostics;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Foster.Framework;

internal class ImGuiBatch
{
    /// <summary>
    /// Vertex Format of Batcher.Vertex
    /// </summary>
    private static readonly VertexFormat VertexFormat = VertexFormat.Create<Vertex>(
        new VertexFormat.Element(0, VertexType.Float2, false),
        new VertexFormat.Element(1, VertexType.Float2, false),
        new VertexFormat.Element(2, VertexType.UByte4, true)
    );

    /// <summary>
    /// The Vertex Layout used for Sprite Batching
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct Vertex : IVertex
    {
        public Vector2 Pos;
        public Vector2 Tex;
        public Color Col;

        public readonly VertexFormat Format => VertexFormat;
    }

    public static Dictionary<Renderers, ShaderCreateInfo> ShaderDefinitions = new()
    {
        [Renderers.OpenGL] = new()
        {
            VertexShader =
                "#version 330\n" +
                "uniform mat4 u_matrix;\n" +
                "layout(location=0) in vec2 a_position;\n" +
                "layout(location=1) in vec2 a_tex;\n" +
                "layout(location=2) in vec4 a_color;\n" +
                "out vec2 v_tex;\n" +
                "out vec4 v_col;\n" +
                "void main(void)\n" +
                "{\n" +
                "	gl_Position = u_matrix * vec4(a_position.xy, 0, 1);\n" +
                "	v_tex = a_tex;\n" +
                "	v_col = a_color;\n" +
                "}",
            FragmentShader =
                "#version 330\n" +
                "uniform sampler2D u_texture;\n" +
                "in vec2 v_tex;\n" +
                "in vec4 v_col;\n" +
                "out vec4 o_color;\n" +
                "void main(void)\n" +
                "{\n" +
                "	vec4 color = texture(u_texture, v_tex);\n" +
                "	o_color = color * v_col;\n" +
                "}"
        }
    };

    /// <summary>
    /// The Default shader used by the Batcher.
    /// </summary>
    private static Shader? DefaultShader;

    /// <summary>
    /// The current Scissor Value of the Batcher
    /// </summary>
    public RectInt? Scissor => currentBatch.Scissor;

    /// <summary>
    /// The number of Triangles in the Batcher to be drawn
    /// </summary>
    public int TriangleCount => indexCount / 3;

    /// <summary>
    /// The number of Vertices in the Batcher to be drawn
    /// </summary>
    public int VertexCount => vertexCount;

    /// <summary>
    /// The number of vertex indices in the Batcher to be drawn
    /// </summary>
    public int IndexCount => indexCount;

    /// <summary>
    /// The number of individual batches (draw calls).
    /// </summary>
    public int BatchCount => batches.Count + (currentBatch.Elements > 0 ? 1 : 0);

    private readonly ShaderState defaultShaderState = new();
    private readonly List<Batch> batches = new();
    private readonly Mesh mesh = new();
    private Batch currentBatch;
    private int currentBatchInsert;
    private bool dirty;
    private Vertex[] vertexArray = new Vertex[64];
    private int vertexCount;
    private int[] indexArray = new int[64];
    private int indexCount;

    private readonly struct ShaderState
    {
        public readonly Shader Shader;
        public readonly Shader.Uniform MatrixUniform;
        public readonly Shader.Uniform TextureUniform;
        public readonly Shader.Uniform SamplerUniform;

        public ShaderState(Shader shader, string matrixUniformName, string textureUniformName, string samplerUniformName)
        {
            Shader = shader;
            MatrixUniform = shader[matrixUniformName];
            TextureUniform = shader[textureUniformName];
            SamplerUniform = shader[samplerUniformName];
        }
    }

    private struct Batch
    {
        public int Layer;
        public ShaderState ShaderState;
        public BlendMode Blend;
        public Texture? Texture;
        public RectInt? Scissor;
        public TextureSampler Sampler;
        public int Offset;
        public int Elements;

        public Batch(ShaderState shaderState, BlendMode blend, Texture? texture, TextureSampler sampler, int offset, int elements)
        {
            Layer = 0;
            ShaderState = shaderState;
            Blend = blend;
            Texture = texture;
            Sampler = sampler;
            Scissor = null;
            Offset = offset;
            Elements = elements;
        }
    }

    public ImGuiBatch()
    {
        DefaultShader ??= new Shader(ShaderDefinitions[Graphics.Renderer]);
        defaultShaderState = new(DefaultShader, "u_matrix", "u_texture", "u_texture_sampler");
        Clear();
    }

    /// <summary>
    /// Clears the Batcher.
    /// </summary>
    public void Clear()
    {
        vertexCount = 0;
        indexCount = 0;
        currentBatchInsert = 0;
        currentBatch = new Batch(defaultShaderState, new(BlendOp.Add, BlendFactor.SrcAlpha, BlendFactor.OneMinusSrcAlpha), null, new(), 0, 0);
        batches.Clear();
    }

    #region Rendering

    /// <summary>
    /// Draws the Batcher to the given Target
    /// </summary>
    /// <param name="target">What Target to Draw to, or null for the Window's backbuffer</param>
    /// <param name="viewport">Optional Viewport Rectangle</param>
    /// <param name="scissor">Optional Scissor Rectangle, which will clip any Scissor rectangles pushed to the Batcher.</param>
    public void Render(Target? target = null, RectInt? viewport = null, RectInt? scissor = null)
    {
        Matrix4x4 matrix = target != null
            ? Matrix4x4.CreateOrthographicOffCenter(0, target.Width, target.Height, 0, 0, float.MaxValue)
            : Matrix4x4.CreateOrthographicOffCenter(0, App.WidthInPixels, App.HeightInPixels, 0, 0, float.MaxValue);
        Render(target, matrix, viewport, scissor);
    }

    /// <summary>
    /// Draws the Batcher to the given Target with the given Matrix Transformation
    /// </summary>
    /// <param name="target">What Target to Draw to, or null for the Window's backbuffer</param>
    /// <param name="matrix">Transforms the entire Batch</param>
    /// <param name="viewport">Optional Viewport Rectangle</param>
    /// <param name="scissor">Optional Scissor Rectangle, which will clip any Scissor rectangles pushed to the Batcher.</param>
    public void Render(Target? target, Matrix4x4 matrix, RectInt? viewport = null, RectInt? scissor = null)
    {
        Debug.Assert(target == null || !target.IsDisposed, "Target is disposed");

        if (batches.Count <= 0 && currentBatch.Elements <= 0)
            return;

        // upload our data if we've been modified since the last time we rendered
        if (dirty)
        {
            mesh.SetIndices<int>(indexArray.AsSpan(0, indexCount));
            mesh.SetVertices<Vertex>(vertexArray.AsSpan(0, vertexCount));
            dirty = false;
        }

        // render batches
        for (int i = 0; i < batches.Count; i++)
        {
            // remaining elements in the current batch
            if (currentBatchInsert == i && currentBatch.Elements > 0)
                RenderBatch(target, currentBatch, matrix, viewport, scissor);

            // render the batch
            RenderBatch(target, batches[i], matrix, viewport, scissor);
        }

        // remaining elements in the current batch
        if (currentBatchInsert == batches.Count && currentBatch.Elements > 0)
            RenderBatch(target, currentBatch, matrix, viewport, scissor);
    }

    private void RenderBatch(Target? target, in Batch batch, in Matrix4x4 matrix, in RectInt? viewport, in RectInt? scissor)
    {
        var trimmed = scissor;
        if (batch.Scissor.HasValue && trimmed.HasValue)
            trimmed = batch.Scissor.Value.OverlapRect(trimmed.Value);
        else if (batch.Scissor.HasValue)
            trimmed = batch.Scissor;

        var texture = batch.Texture != null && !batch.Texture.IsDisposed ? batch.Texture : null;
        batch.ShaderState.MatrixUniform.Set(matrix);
        batch.ShaderState.TextureUniform.Set(texture);
        batch.ShaderState.SamplerUniform.Set(batch.Sampler);

        DrawCommand command = new(target, mesh, batch.ShaderState.Shader)
        {
            Viewport = viewport,
            Scissor = trimmed,
            BlendMode = batch.Blend,
            MeshIndexStart = batch.Offset * 3,
            MeshIndexCount = batch.Elements * 3
        };
        command.Submit();
    }

    #endregion

    #region Modify State

    /// <summary>
    /// Sets the Current Texture being drawn
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void SetTexture(Texture? texture)
    {
        if (currentBatch.Texture == null || currentBatch.Elements == 0)
        {
            currentBatch.Texture = texture;
        }
        else if (currentBatch.Texture != texture)
        {
            batches.Insert(currentBatchInsert, currentBatch);

            currentBatch.Texture = texture;
            currentBatch.Offset += currentBatch.Elements;
            currentBatch.Elements = 0;
            currentBatchInsert++;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void SetScissor(RectInt? scissor)
    {
        if (currentBatch.Elements == 0)
        {
            currentBatch.Scissor = scissor;
        }
        else if (currentBatch.Scissor != scissor)
        {
            batches.Insert(currentBatchInsert, currentBatch);

            currentBatch.Scissor = scissor;
            currentBatch.Offset += currentBatch.Elements;
            currentBatch.Elements = 0;
            currentBatchInsert++;
        }
    }

    #endregion

    #region Copy Arrays

    public void CopyArray(ReadOnlySpan<Vertex> vertexBuffer, ReadOnlySpan<ushort> indexBuffer, int indexOffset)
    {
        // copy vertices over
        ExpandvertexArray(vertexCount + vertexBuffer.Length);
        vertexBuffer.CopyTo(vertexArray.AsSpan().Slice(vertexCount));

        // copy indices over
        while (indexCount + indexBuffer.Length >= indexArray.Length)
            Array.Resize(ref indexArray, indexArray.Length * 2);

        if(indexBuffer.Length > 0)
        {
            var count = indexBuffer.Length;
            var indexSpan = indexArray.AsSpan(indexCount, count);
            for (int i = 0; i < indexBuffer.Length; i++)
                indexSpan[i] = indexOffset + indexBuffer[i];
        }

        // increment
        vertexCount += vertexBuffer.Length;
        indexCount += indexBuffer.Length;
        currentBatch.Elements += (indexBuffer.Length / 3);
        dirty = true;
    }

    #endregion

    #region Internal Utils

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void ExpandvertexArray(int index)
    {
        while (index >= vertexArray.Length)
        {
            Array.Resize(ref vertexArray, vertexArray.Length * 2);
        }
    }

    #endregion
}
