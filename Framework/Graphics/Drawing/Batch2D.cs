using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Foster.Framework
{
    /// <summary>
    /// A 2D Sprite Batcher used for drawing images, text, and shapes
    /// </summary>
    public class Batch2D
    {
        public static VertexFormat VertexFormat { get; } = new VertexFormat(
            new VertexAttribute("a_position", VertexAttrib.Position, VertexType.Float, VertexComponents.Two, false),
            new VertexAttribute("a_tex", VertexAttrib.TexCoord0, VertexType.Float, VertexComponents.Two, false),
            new VertexAttribute("a_color", VertexAttrib.Color0, VertexType.Byte, VertexComponents.Four, true),
            new VertexAttribute("a_type", VertexAttrib.TexCoord1, VertexType.Byte, VertexComponents.Three, true));

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public struct Vertex : IVertex
        {
            public Vector2 Pos;
            public Vector2 Tex;
            public Color Col;
            public byte Mult;
            public byte Wash;
            public byte Fill;

            public Vertex(Vector2 position, Vector2 texcoord, Color color, int mult, int wash, int fill)
            {
                Pos = position;
                Tex = texcoord;
                Col = color;
                Mult = (byte)mult;
                Wash = (byte)wash;
                Fill = (byte)fill;
            }

            public VertexFormat Format => VertexFormat;

            public override string ToString()
            {
                return $"{{Pos:{Pos}, Tex:{Tex}, Col:{Col}, Mult:{Mult}, Wash:{Wash}, Fill:{Fill}}}";
            }
        }

        private static Shader? _defaultBatchShader;

        public Graphics Graphics { get; }
        public Shader DefaultShader { get; }
        public Material DefaultMaterial { get; }
        public Mesh Mesh { get; }

        public Matrix3x2 MatrixStack { get; set; } = Matrix3x2.Identity;
        public RectInt? Scissor => _currentBatch.Scissor;

        public string TextureUniformName { get; set; } = "u_texture";
        public string MatrixUniformName { get; set; } = "u_matrix";

        private readonly Stack<Matrix3x2> _matrixStack = new Stack<Matrix3x2>();
        private Vertex[] _vertices;
        private int[] _indices;
        private RenderPass _pass;
        private readonly List<Batch> _batches;
        private Batch _currentBatch;
        private int _currentBatchInsert;
        private bool _dirty;
        private int _vertexCount;
        private int _indexCount;

        public int TriangleCount => _indexCount / 3;
        public int VertexCount => _vertexCount;
        public int IndexCount => _indexCount;
        public int BatchCount => _batches.Count + (_currentBatch.Elements > 0 ? 1 : 0);

        private struct Batch
        {
            public int Layer;
            public Material? Material;
            public BlendMode BlendMode;
            public Matrix3x2 Matrix;
            public Texture? Texture;
            public RectInt? Scissor;
            public uint Offset;
            public uint Elements;

            public Batch(Material? material, BlendMode blend, Texture? texture, Matrix3x2 matrix, uint offset, uint elements)
            {
                Layer = 0;
                Material = material;
                BlendMode = blend;
                Texture = texture;
                Matrix = matrix;
                Scissor = null;
                Offset = offset;
                Elements = elements;
            }
        }

        public Batch2D() : this(App.Graphics)
        {

        }

        public Batch2D(Graphics graphics)
        {
            Graphics = graphics;

            if (_defaultBatchShader == null)
            {
                _defaultBatchShader = new Shader(graphics, graphics.CreateShaderSourceBatch2D());
            }

            DefaultShader = _defaultBatchShader;
            DefaultMaterial = new Material(DefaultShader);

            Mesh = new Mesh(graphics);

            _vertices = new Vertex[64];
            _indices = new int[64];
            _batches = new List<Batch>();

            Clear();
        }

        public void Clear()
        {
            _vertexCount = 0;
            _indexCount = 0;
            _currentBatchInsert = 0;
            _currentBatch = new Batch(null, BlendMode.Normal, null, Matrix3x2.Identity, 0, 0);
            _batches.Clear();
            _matrixStack.Clear();
            MatrixStack = Matrix3x2.Identity;
        }

        #region Rendering

        public void Render(RenderTarget target)
        {
            var matrix = Matrix4x4.CreateOrthographicOffCenter(0, target.RenderWidth, target.RenderHeight, 0, 0, float.MaxValue);
            Render(target, matrix);
        }

        public void Render(RenderTarget target, Color clearColor)
        {
            App.Graphics.Clear(target, clearColor);
            Render(target);
        }

        public void Render(RenderTarget target, Matrix4x4 matrix, RectInt? viewport = null, Color? clearColor = null)
        {
            if (clearColor != null)
            {
                App.Graphics.Clear(target, clearColor.Value);
            }

            _pass = new RenderPass(target, Mesh, DefaultMaterial);
            _pass.Viewport = viewport;

            Debug.Assert(_matrixStack.Count <= 0, "Batch.MatrixStack Pushes more than it Pops");

            if (_batches.Count > 0 || _currentBatch.Elements > 0)
            {
                if (_dirty)
                {
                    Mesh.SetVertices(new ReadOnlyMemory<Vertex>(_vertices, 0, _vertexCount));
                    Mesh.SetIndices(new ReadOnlyMemory<int>(_indices, 0, _indexCount));

                    _dirty = false;
                }

                // render batches
                for (int i = 0; i < _batches.Count; i++)
                {
                    // remaining elements in the current batch
                    if (_currentBatchInsert == i && _currentBatch.Elements > 0)
                    {
                        RenderBatch(_currentBatch, matrix);
                    }

                    // render the batch
                    RenderBatch(_batches[i], matrix);
                }

                // remaining elements in the current batch
                if (_currentBatchInsert == _batches.Count && _currentBatch.Elements > 0)
                {
                    RenderBatch(_currentBatch, matrix);
                }
            }
        }

        private void RenderBatch(in Batch batch, in Matrix4x4 matrix)
        {
            _pass.Scissor = batch.Scissor;
            _pass.BlendMode = batch.BlendMode;

            // Render the Mesh
            // Note we apply the texture and matrix based on the current batch
            // If the user set these on the Material themselves, they will be overwritten here

            _pass.Material = batch.Material ?? DefaultMaterial;
            _pass.Material[TextureUniformName]?.SetTexture(batch.Texture);
            _pass.Material[MatrixUniformName]?.SetMatrix4x4(new Matrix4x4(batch.Matrix) * matrix);

            _pass.MeshIndexStart = batch.Offset * 3;
            _pass.MeshIndexCount = batch.Elements * 3;
            _pass.MeshInstanceCount = 0;

            Graphics.Render(ref _pass);
        }

        #endregion

        #region Modify State

        public void SetMaterial(Material? material)
        {
            if (_currentBatch.Elements == 0)
            {
                _currentBatch.Material = material;
            }
            else if (_currentBatch.Material != material)
            {
                _batches.Insert(_currentBatchInsert, _currentBatch);

                _currentBatch.Material = material;
                _currentBatch.Offset += _currentBatch.Elements;
                _currentBatch.Elements = 0;
                _currentBatchInsert++;
            }
        }

        public void SetBlendMode(in BlendMode blendmode)
        {
            if (_currentBatch.Elements == 0)
            {
                _currentBatch.BlendMode = blendmode;
            }
            else if (_currentBatch.BlendMode != blendmode)
            {
                _batches.Insert(_currentBatchInsert, _currentBatch);

                _currentBatch.BlendMode = blendmode;
                _currentBatch.Offset += _currentBatch.Elements;
                _currentBatch.Elements = 0;
                _currentBatchInsert++;
            }
        }

        public BlendMode GetBlendMode()
        {
            return _currentBatch.BlendMode;
        }

        public void SetMatrix(in Matrix3x2 matrix)
        {
            if (_currentBatch.Elements == 0)
            {
                _currentBatch.Matrix = matrix;
            }
            else if (_currentBatch.Matrix != matrix)
            {
                _batches.Insert(_currentBatchInsert, _currentBatch);

                _currentBatch.Matrix = matrix;
                _currentBatch.Offset += _currentBatch.Elements;
                _currentBatch.Elements = 0;
                _currentBatchInsert++;
            }
        }

        public void SetScissor(RectInt? scissor)
        {
            if (_currentBatch.Elements == 0)
            {
                _currentBatch.Scissor = scissor;
            }
            else if (_currentBatch.Scissor != scissor)
            {
                _batches.Insert(_currentBatchInsert, _currentBatch);

                _currentBatch.Scissor = scissor;
                _currentBatch.Offset += _currentBatch.Elements;
                _currentBatch.Elements = 0;
                _currentBatchInsert++;
            }
        }

        public void SetTexture(Texture? texture)
        {
            if (_currentBatch.Texture == null || _currentBatch.Elements == 0)
            {
                _currentBatch.Texture = texture;
            }
            else if (_currentBatch.Texture != texture)
            {
                _batches.Insert(_currentBatchInsert, _currentBatch);

                _currentBatch.Texture = texture;
                _currentBatch.Offset += _currentBatch.Elements;
                _currentBatch.Elements = 0;
                _currentBatchInsert++;
            }
        }

        public void SetLayer(int layer)
        {
            if (_currentBatch.Layer == layer)
            {
                return;
            }

            // insert last batch
            if (_currentBatch.Elements > 0)
            {
                _batches.Insert(_currentBatchInsert, _currentBatch);
                _currentBatch.Offset += _currentBatch.Elements;
                _currentBatch.Elements = 0;
            }

            // find the point to insert us
            var insert = 0;
            while (insert < _batches.Count && _batches[insert].Layer >= layer)
            {
                insert++;
            }

            _currentBatch.Layer = layer;
            _currentBatchInsert = insert;
        }

        public void SetState(Material? material, in BlendMode blendmode, in Matrix3x2 matrix, RectInt? scissor)
        {
            SetMaterial(material);
            SetBlendMode(blendmode);
            SetMatrix(matrix);
            SetScissor(scissor);
        }

        public Matrix3x2 PushMatrix(in Vector2 position, in Vector2 scale, in Vector2 origin, float rotation, bool relative = true)
        {
            return PushMatrix(Transform2D.CreateMatrix(position, origin, scale, rotation), relative);
        }

        public Matrix3x2 PushMatrix(Transform2D transform, bool relative = true)
        {
            return PushMatrix(transform.WorldMatrix, relative);
        }

        public Matrix3x2 PushMatrix(in Vector2 position, bool relative = true)
        {
            return PushMatrix(Matrix3x2.CreateTranslation(position.X, position.Y), relative);
        }

        public Matrix3x2 PushMatrix(in Matrix3x2 matrix, bool relative = true)
        {
            _matrixStack.Push(MatrixStack);

            if (relative)
            {
                MatrixStack = matrix * MatrixStack;
            }
            else
            {
                MatrixStack = matrix;
            }

            return MatrixStack;
        }

        public Matrix3x2 PopMatrix()
        {
            Debug.Assert(_matrixStack.Count > 0, "Batch.MatrixStack Pops more than it Pushes");

            if (_matrixStack.Count > 0)
            {
                MatrixStack = _matrixStack.Pop();
            }
            else
            {
                MatrixStack = Matrix3x2.Identity;
            }

            return MatrixStack;
        }

        #endregion

        #region Line

        public void Line(Vector2 from, Vector2 to, float thickness, Color color)
        {
            var normal = (to - from).Normalized();
            var perp = new Vector2(-normal.Y, normal.X) * thickness * .5f;
            Quad(from + perp, from - perp, to - perp, to + perp, color);
        }

        public void DashedLine(Vector2 from, Vector2 to, float thickness, Color color, float dashLength, float offsetPercent)
        {
            var diff = to - from;
            var dist = diff.Length();
            var axis = diff.Normalized();
            var perp = axis.TurnLeft() * (thickness * 0.5f);
            offsetPercent = ((offsetPercent % 1f) + 1f) % 1f;

            var startD = dashLength * offsetPercent * 2f;
            if (startD > dashLength)
            {
                startD -= dashLength * 2f;
            }

            for (float d = startD; d < dist; d += dashLength * 2f)
            {
                var a = from + axis * Math.Max(d, 0f);
                var b = from + axis * Math.Min(d + dashLength, dist);
                Quad(a + perp, b + perp, b - perp, a - perp, color);
            }
        }

        #endregion

        #region Quad

        public void Quad(in Quad2D quad, Color color)
        {
            Quad(quad.A, quad.B, quad.C, quad.D, color);
        }

        public void Quad(in Vector2 v0, in Vector2 v1, in Vector2 v2, in Vector2 v3, Color color)
        {
            PushQuad();
            ExpandVertices(_vertexCount + 4);

            // POS
            Transform(ref _vertices[_vertexCount + 0].Pos, v0, MatrixStack);
            Transform(ref _vertices[_vertexCount + 1].Pos, v1, MatrixStack);
            Transform(ref _vertices[_vertexCount + 2].Pos, v2, MatrixStack);
            Transform(ref _vertices[_vertexCount + 3].Pos, v3, MatrixStack);

            // COL
            _vertices[_vertexCount + 0].Col = color;
            _vertices[_vertexCount + 1].Col = color;
            _vertices[_vertexCount + 2].Col = color;
            _vertices[_vertexCount + 3].Col = color;

            // MULT
            _vertices[_vertexCount + 0].Mult = 0;
            _vertices[_vertexCount + 1].Mult = 0;
            _vertices[_vertexCount + 2].Mult = 0;
            _vertices[_vertexCount + 3].Mult = 0;

            // WASH
            _vertices[_vertexCount + 0].Wash = 0;
            _vertices[_vertexCount + 1].Wash = 0;
            _vertices[_vertexCount + 2].Wash = 0;
            _vertices[_vertexCount + 3].Wash = 0;

            // FILL
            _vertices[_vertexCount + 0].Fill = 255;
            _vertices[_vertexCount + 1].Fill = 255;
            _vertices[_vertexCount + 2].Fill = 255;
            _vertices[_vertexCount + 3].Fill = 255;

            _vertexCount += 4;
        }

        public void Quad(in Vector2 v0, in Vector2 v1, in Vector2 v2, in Vector2 v3, in Vector2 t0, in Vector2 t1, in Vector2 t2, in Vector2 t3, Color color, bool washed = false)
        {
            PushQuad();
            ExpandVertices(_vertexCount + 4);

            var mult = (byte)(washed ? 0 : 255);
            var wash = (byte)(washed ? 255 : 0);

            // POS
            Transform(ref _vertices[_vertexCount + 0].Pos, v0, MatrixStack);
            Transform(ref _vertices[_vertexCount + 1].Pos, v1, MatrixStack);
            Transform(ref _vertices[_vertexCount + 2].Pos, v2, MatrixStack);
            Transform(ref _vertices[_vertexCount + 3].Pos, v3, MatrixStack);

            // TEX
            _vertices[_vertexCount + 0].Tex = t0;
            _vertices[_vertexCount + 1].Tex = t1;
            _vertices[_vertexCount + 2].Tex = t2;
            _vertices[_vertexCount + 3].Tex = t3;

            if (Graphics.OriginBottomLeft && (_currentBatch.Texture?.IsFrameBuffer ?? false))
            {
                VerticalFlip(ref _vertices[_vertexCount + 0].Tex, ref _vertices[_vertexCount + 1].Tex, ref _vertices[_vertexCount + 2].Tex, ref _vertices[_vertexCount + 3].Tex);
            }

            // COL
            _vertices[_vertexCount + 0].Col = color;
            _vertices[_vertexCount + 1].Col = color;
            _vertices[_vertexCount + 2].Col = color;
            _vertices[_vertexCount + 3].Col = color;

            // MULT
            _vertices[_vertexCount + 0].Mult = mult;
            _vertices[_vertexCount + 1].Mult = mult;
            _vertices[_vertexCount + 2].Mult = mult;
            _vertices[_vertexCount + 3].Mult = mult;

            // WASH
            _vertices[_vertexCount + 0].Wash = wash;
            _vertices[_vertexCount + 1].Wash = wash;
            _vertices[_vertexCount + 2].Wash = wash;
            _vertices[_vertexCount + 3].Wash = wash;

            // FILL
            _vertices[_vertexCount + 0].Fill = 0;
            _vertices[_vertexCount + 1].Fill = 0;
            _vertices[_vertexCount + 2].Fill = 0;
            _vertices[_vertexCount + 3].Fill = 0;

            _vertexCount += 4;
        }

        public void Quad(in Vector2 v0, in Vector2 v1, in Vector2 v2, in Vector2 v3, Color c0, Color c1, Color c2, Color c3)
        {
            PushQuad();
            ExpandVertices(_vertexCount + 4);

            // POS
            Transform(ref _vertices[_vertexCount + 0].Pos, v0, MatrixStack);
            Transform(ref _vertices[_vertexCount + 1].Pos, v1, MatrixStack);
            Transform(ref _vertices[_vertexCount + 2].Pos, v2, MatrixStack);
            Transform(ref _vertices[_vertexCount + 3].Pos, v3, MatrixStack);

            // COL
            _vertices[_vertexCount + 0].Col = c0;
            _vertices[_vertexCount + 1].Col = c1;
            _vertices[_vertexCount + 2].Col = c2;
            _vertices[_vertexCount + 3].Col = c3;

            // MULT
            _vertices[_vertexCount + 0].Mult = 0;
            _vertices[_vertexCount + 1].Mult = 0;
            _vertices[_vertexCount + 2].Mult = 0;
            _vertices[_vertexCount + 3].Mult = 0;

            // WASH
            _vertices[_vertexCount + 0].Wash = 0;
            _vertices[_vertexCount + 1].Wash = 0;
            _vertices[_vertexCount + 2].Wash = 0;
            _vertices[_vertexCount + 3].Wash = 0;

            // FILL
            _vertices[_vertexCount + 0].Fill = 255;
            _vertices[_vertexCount + 1].Fill = 255;
            _vertices[_vertexCount + 2].Fill = 255;
            _vertices[_vertexCount + 3].Fill = 255;

            _vertexCount += 4;
        }

        public void Quad(in Vector2 v0, in Vector2 v1, in Vector2 v2, in Vector2 v3, in Vector2 t0, in Vector2 t1, in Vector2 t2, in Vector2 t3, Color c0, Color c1, Color c2, Color c3, bool washed = false)
        {
            PushQuad();
            ExpandVertices(_vertexCount + 4);

            var mult = (byte)(washed ? 0 : 255);
            var wash = (byte)(washed ? 255 : 0);

            // POS
            Transform(ref _vertices[_vertexCount + 0].Pos, v0, MatrixStack);
            Transform(ref _vertices[_vertexCount + 1].Pos, v1, MatrixStack);
            Transform(ref _vertices[_vertexCount + 2].Pos, v2, MatrixStack);
            Transform(ref _vertices[_vertexCount + 3].Pos, v3, MatrixStack);

            // TEX
            _vertices[_vertexCount + 0].Tex = t0;
            _vertices[_vertexCount + 1].Tex = t1;
            _vertices[_vertexCount + 2].Tex = t2;
            _vertices[_vertexCount + 3].Tex = t3;

            if (Graphics.OriginBottomLeft && (_currentBatch.Texture?.IsFrameBuffer ?? false))
            {
                VerticalFlip(ref _vertices[_vertexCount + 0].Tex, ref _vertices[_vertexCount + 1].Tex, ref _vertices[_vertexCount + 2].Tex, ref _vertices[_vertexCount + 3].Tex);
            }

            // COL
            _vertices[_vertexCount + 0].Col = c0;
            _vertices[_vertexCount + 1].Col = c1;
            _vertices[_vertexCount + 2].Col = c2;
            _vertices[_vertexCount + 3].Col = c3;

            // MULT
            _vertices[_vertexCount + 0].Mult = mult;
            _vertices[_vertexCount + 1].Mult = mult;
            _vertices[_vertexCount + 2].Mult = mult;
            _vertices[_vertexCount + 3].Mult = mult;

            // WASH
            _vertices[_vertexCount + 0].Wash = wash;
            _vertices[_vertexCount + 1].Wash = wash;
            _vertices[_vertexCount + 2].Wash = wash;
            _vertices[_vertexCount + 3].Wash = wash;

            // FILL
            _vertices[_vertexCount + 0].Fill = 0;
            _vertices[_vertexCount + 1].Fill = 0;
            _vertices[_vertexCount + 2].Fill = 0;
            _vertices[_vertexCount + 3].Fill = 0;

            _vertexCount += 4;
        }

        #endregion

        #region Triangle

        public void Triangle(in Vector2 v0, in Vector2 v1, in Vector2 v2, Color color)
        {
            PushTriangle();
            ExpandVertices(_vertexCount + 3);

            // POS
            Transform(ref _vertices[_vertexCount + 0].Pos, v0, MatrixStack);
            Transform(ref _vertices[_vertexCount + 1].Pos, v1, MatrixStack);
            Transform(ref _vertices[_vertexCount + 2].Pos, v2, MatrixStack);

            // COL
            _vertices[_vertexCount + 0].Col = color;
            _vertices[_vertexCount + 1].Col = color;
            _vertices[_vertexCount + 2].Col = color;

            // MULT
            _vertices[_vertexCount + 0].Mult = 0;
            _vertices[_vertexCount + 1].Mult = 0;
            _vertices[_vertexCount + 2].Mult = 0;
            _vertices[_vertexCount + 3].Mult = 0;

            // WASH
            _vertices[_vertexCount + 0].Wash = 0;
            _vertices[_vertexCount + 1].Wash = 0;
            _vertices[_vertexCount + 2].Wash = 0;
            _vertices[_vertexCount + 3].Wash = 0;

            // FILL
            _vertices[_vertexCount + 0].Fill = 255;
            _vertices[_vertexCount + 1].Fill = 255;
            _vertices[_vertexCount + 2].Fill = 255;
            _vertices[_vertexCount + 3].Fill = 255;

            _vertexCount += 3;
        }

        public void Triangle(in Vector2 v0, in Vector2 v1, in Vector2 v2, Color c0, Color c1, Color c2)
        {
            PushTriangle();
            ExpandVertices(_vertexCount + 3);

            // POS
            Transform(ref _vertices[_vertexCount + 0].Pos, v0, MatrixStack);
            Transform(ref _vertices[_vertexCount + 1].Pos, v1, MatrixStack);
            Transform(ref _vertices[_vertexCount + 2].Pos, v2, MatrixStack);

            // COL
            _vertices[_vertexCount + 0].Col = c0;
            _vertices[_vertexCount + 1].Col = c1;
            _vertices[_vertexCount + 2].Col = c2;

            // MULT
            _vertices[_vertexCount + 0].Mult = 0;
            _vertices[_vertexCount + 1].Mult = 0;
            _vertices[_vertexCount + 2].Mult = 0;
            _vertices[_vertexCount + 3].Mult = 0;

            // WASH
            _vertices[_vertexCount + 0].Wash = 0;
            _vertices[_vertexCount + 1].Wash = 0;
            _vertices[_vertexCount + 2].Wash = 0;
            _vertices[_vertexCount + 3].Wash = 0;

            // FILL
            _vertices[_vertexCount + 0].Fill = 255;
            _vertices[_vertexCount + 1].Fill = 255;
            _vertices[_vertexCount + 2].Fill = 255;
            _vertices[_vertexCount + 3].Fill = 255;

            _vertexCount += 3;
        }

        #endregion

        #region Rect

        public void Rect(in Rect rect, Color color)
        {
            Quad(
                new Vector2(rect.X, rect.Y),
                new Vector2(rect.X + rect.Width, rect.Y),
                new Vector2(rect.X + rect.Width, rect.Y + rect.Height),
                new Vector2(rect.X, rect.Y + rect.Height),
                color);
        }

        public void Rect(in Vector2 position, in Vector2 size, Color color)
        {
            Quad(
                position,
                position + new Vector2(size.X, 0),
                position + new Vector2(size.X, size.Y),
                position + new Vector2(0, size.Y),
                color);
        }

        public void Rect(float x, float y, float width, float height, Color color)
        {
            Quad(
                new Vector2(x, y),
                new Vector2(x + width, y),
                new Vector2(x + width, y + height),
                new Vector2(x, y + height), color);
        }

        public void Rect(in Rect rect, Color c0, Color c1, Color c2, Color c3)
        {
            Quad(
                new Vector2(rect.X, rect.Y),
                new Vector2(rect.X + rect.Width, rect.Y),
                new Vector2(rect.X + rect.Width, rect.Y + rect.Height),
                new Vector2(rect.X, rect.Y + rect.Height),
                c0, c1, c2, c3);
        }

        public void Rect(in Vector2 position, in Vector2 size, Color c0, Color c1, Color c2, Color c3)
        {
            Quad(
                position,
                position + new Vector2(size.X, 0),
                position + new Vector2(size.X, size.Y),
                position + new Vector2(0, size.Y),
                c0, c1, c2, c3);
        }

        public void Rect(float x, float y, float width, float height, Color c0, Color c1, Color c2, Color c3)
        {
            Quad(
                new Vector2(x, y),
                new Vector2(x + width, y),
                new Vector2(x + width, y + height),
                new Vector2(x, y + height),
                c0, c1, c2, c3);
        }

        #endregion

        #region Rounded Rect

        public void RoundedRect(float x, float y, float width, float height, float r0, float r1, float r2, float r3, Color color)
        {
            RoundedRect(new Rect(x, y, width, height), r0, r1, r2, r3, color);
        }

        public void RoundedRect(float x, float y, float width, float height, float radius, Color color)
        {
            RoundedRect(new Rect(x, y, width, height), radius, radius, radius, radius, color);
        }

        public void RoundedRect(in Rect rect, float radius, Color color)
        {
            RoundedRect(rect, radius, radius, radius, radius, color);
        }

        public void RoundedRect(in Rect rect, float r0, float r1, float r2, float r3, Color color)
        {
            // clamp
            r0 = Math.Min(Math.Min(Math.Max(0, r0), rect.Width / 2f), rect.Height / 2f);
            r1 = Math.Min(Math.Min(Math.Max(0, r1), rect.Width / 2f), rect.Height / 2f);
            r2 = Math.Min(Math.Min(Math.Max(0, r2), rect.Width / 2f), rect.Height / 2f);
            r3 = Math.Min(Math.Min(Math.Max(0, r3), rect.Width / 2f), rect.Height / 2f);

            if (r0 <= 0 && r1 <= 0 && r2 <= 0 && r3 <= 0)
            {
                Rect(rect, color);
            }
            else
            {
                // get corners
                var r0_tl = rect.TopLeft;
                var r0_tr = r0_tl + new Vector2(r0, 0);
                var r0_br = r0_tl + new Vector2(r0, r0);
                var r0_bl = r0_tl + new Vector2(0, r0);

                var r1_tl = rect.TopRight + new Vector2(-r1, 0);
                var r1_tr = r1_tl + new Vector2(r1, 0);
                var r1_br = r1_tl + new Vector2(r1, r1);
                var r1_bl = r1_tl + new Vector2(0, r1);

                var r2_tl = rect.BottomRight + new Vector2(-r2, -r2);
                var r2_tr = r2_tl + new Vector2(r2, 0);
                var r2_bl = r2_tl + new Vector2(0, r2);
                var r2_br = r2_tl + new Vector2(r2, r2);

                var r3_tl = rect.BottomLeft + new Vector2(0, -r3);
                var r3_tr = r3_tl + new Vector2(r3, 0);
                var r3_bl = r3_tl + new Vector2(0, r3);
                var r3_br = r3_tl + new Vector2(r3, r3);

                // set tris
                {
                    while (_indexCount + 30 >= _indices.Length)
                    {
                        Array.Resize(ref _indices, _indices.Length * 2);
                    }

                    // top quad
                    {
                        _indices[_indexCount + 00] = _vertexCount + 00; // r0b
                        _indices[_indexCount + 01] = _vertexCount + 03; // r1a
                        _indices[_indexCount + 02] = _vertexCount + 05; // r1d

                        _indices[_indexCount + 03] = _vertexCount + 00; // r0b
                        _indices[_indexCount + 04] = _vertexCount + 05; // r1d
                        _indices[_indexCount + 05] = _vertexCount + 01; // r0c
                    }

                    // left quad
                    {
                        _indices[_indexCount + 06] = _vertexCount + 02; // r0d
                        _indices[_indexCount + 07] = _vertexCount + 01; // r0c
                        _indices[_indexCount + 08] = _vertexCount + 10; // r3b

                        _indices[_indexCount + 09] = _vertexCount + 02; // r0d
                        _indices[_indexCount + 10] = _vertexCount + 10; // r3b
                        _indices[_indexCount + 11] = _vertexCount + 09; // r3a
                    }

                    // right quad
                    {
                        _indices[_indexCount + 12] = _vertexCount + 05; // r1d
                        _indices[_indexCount + 13] = _vertexCount + 04; // r1c
                        _indices[_indexCount + 14] = _vertexCount + 07; // r2b

                        _indices[_indexCount + 15] = _vertexCount + 05; // r1d
                        _indices[_indexCount + 16] = _vertexCount + 07; // r2b
                        _indices[_indexCount + 17] = _vertexCount + 06; // r2a
                    }

                    // bottom quad
                    {
                        _indices[_indexCount + 18] = _vertexCount + 10; // r3b
                        _indices[_indexCount + 19] = _vertexCount + 06; // r2a
                        _indices[_indexCount + 20] = _vertexCount + 08; // r2d

                        _indices[_indexCount + 21] = _vertexCount + 10; // r3b
                        _indices[_indexCount + 22] = _vertexCount + 08; // r2d
                        _indices[_indexCount + 23] = _vertexCount + 11; // r3c
                    }

                    // center quad
                    {
                        _indices[_indexCount + 24] = _vertexCount + 01; // r0c
                        _indices[_indexCount + 25] = _vertexCount + 05; // r1d
                        _indices[_indexCount + 26] = _vertexCount + 06; // r2a

                        _indices[_indexCount + 27] = _vertexCount + 01; // r0c
                        _indices[_indexCount + 28] = _vertexCount + 06; // r2a
                        _indices[_indexCount + 29] = _vertexCount + 10; // r3b
                    }

                    _indexCount += 30;
                    _currentBatch.Elements += 10;
                    _dirty = true;
                }

                // set verts
                {
                    ExpandVertices(_vertexCount + 12);

                    Array.Fill(_vertices, new Vertex(Vector2.Zero, Vector2.Zero, color, 0, 0, 255), _vertexCount, 12);

                    Transform(ref _vertices[_vertexCount + 00].Pos, r0_tr, MatrixStack); // 0
                    Transform(ref _vertices[_vertexCount + 01].Pos, r0_br, MatrixStack); // 1
                    Transform(ref _vertices[_vertexCount + 02].Pos, r0_bl, MatrixStack); // 2

                    Transform(ref _vertices[_vertexCount + 03].Pos, r1_tl, MatrixStack); // 3
                    Transform(ref _vertices[_vertexCount + 04].Pos, r1_br, MatrixStack); // 4
                    Transform(ref _vertices[_vertexCount + 05].Pos, r1_bl, MatrixStack); // 5

                    Transform(ref _vertices[_vertexCount + 06].Pos, r2_tl, MatrixStack); // 6
                    Transform(ref _vertices[_vertexCount + 07].Pos, r2_tr, MatrixStack); // 7
                    Transform(ref _vertices[_vertexCount + 08].Pos, r2_bl, MatrixStack); // 8

                    Transform(ref _vertices[_vertexCount + 09].Pos, r3_tl, MatrixStack); // 9
                    Transform(ref _vertices[_vertexCount + 10].Pos, r3_tr, MatrixStack); // 10
                    Transform(ref _vertices[_vertexCount + 11].Pos, r3_br, MatrixStack); // 11

                    _vertexCount += 12;
                }

                // TODO: replace with hard-coded values
                var left = Calc.Angle(-Vector2.UnitX);
                var right = Calc.Angle(Vector2.UnitX);
                var up = Calc.Angle(-Vector2.UnitY);
                var down = Calc.Angle(Vector2.UnitY);

                // top-left corner
                if (r0 > 0)
                {
                    SemiCircle(r0_br, up, -left, r0, Math.Max(3, (int)(r0 / 4)), color);
                }
                else
                {
                    Quad(r0_tl, r0_tr, r0_br, r0_bl, color);
                }

                // top-right corner
                if (r1 > 0)
                {
                    SemiCircle(r1_bl, up, right, r1, Math.Max(3, (int)(r1 / 4)), color);
                }
                else
                {
                    Quad(r1_tl, r1_tr, r1_br, r1_bl, color);
                }

                // bottom-right corner
                if (r2 > 0)
                {
                    SemiCircle(r2_tl, right, down, r2, Math.Max(3, (int)(r2 / 4)), color);
                }
                else
                {
                    Quad(r2_tl, r2_tr, r2_br, r2_bl, color);
                }

                // bottom-left corner
                if (r3 > 0)
                {
                    SemiCircle(r3_tr, down, left, r3, Math.Max(3, (int)(r3 / 4)), color);
                }
                else
                {
                    Quad(r3_tl, r3_tr, r3_br, r3_bl, color);
                }
            }

        }

        #endregion

        #region Circle

        public void SemiCircle(Vector2 center, float startRadians, float endRadians, float radius, int steps, Color color)
        {
            SemiCircle(center, startRadians, endRadians, radius, steps, color, color);
        }

        public void SemiCircle(Vector2 center, float startRadians, float endRadians, float radius, int steps, Color centerColor, Color edgeColor)
        {
            var last = Calc.AngleToVector(startRadians, radius);

            for (int i = 1; i <= steps; i++)
            {
                var next = Calc.AngleToVector(startRadians + (endRadians - startRadians) * (i / (float)steps), radius);
                Triangle(center + last, center + next, center, edgeColor, edgeColor, centerColor);
                last = next;
            }
        }

        public void Circle(Vector2 center, float radius, int steps, Color color)
        {
            Circle(center, radius, steps, color, color);
        }

        public void Circle(Vector2 center, float radius, int steps, Color centerColor, Color edgeColor)
        {
            var last = Calc.AngleToVector(0, radius);

            for (int i = 1; i <= steps; i++)
            {
                var next = Calc.AngleToVector((i / (float)steps) * Calc.TAU, radius);
                Triangle(center + last, center + next, center, edgeColor, edgeColor, centerColor);
                last = next;
            }
        }

        public void HollowCircle(Vector2 center, float radius, float thickness, int steps, Color color)
        {
            var last = Calc.AngleToVector(0, radius);

            for (int i = 1; i <= steps; i++)
            {
                var next = Calc.AngleToVector((i / (float)steps) * Calc.TAU, radius);
                Line(center + last, center + next, thickness, color);
                last = next;
            }
        }

        #endregion

        #region Hollow Rect

        public void HollowRect(in Rect rect, float t, Color color)
        {
            if (t > 0)
            {
                var tx = Math.Min(t, rect.Width / 2f);
                var ty = Math.Min(t, rect.Height / 2f);

                Rect(rect.X, rect.Y, rect.Width, ty, color);
                Rect(rect.X, rect.Bottom - ty, rect.Width, ty, color);
                Rect(rect.X, rect.Y + ty, tx, rect.Height - ty * 2, color);
                Rect(rect.Right - tx, rect.Y + ty, tx, rect.Height - ty * 2, color);
            }
        }

        #endregion

        #region Image

        public void Image(Texture texture,
            in Vector2 pos0, in Vector2 pos1, in Vector2 pos2, in Vector2 pos3,
            in Vector2 uv0, in Vector2 uv1, in Vector2 uv2, in Vector2 uv3,
            Color col0, Color col1, Color col2, Color col3, bool washed = false)
        {
            SetTexture(texture);
            Quad(pos0, pos1, pos2, pos3, uv0, uv1, uv2, uv3, col0, col1, col2, col3, washed);
        }

        public void Image(Texture texture,
            in Vector2 pos0, in Vector2 pos1, in Vector2 pos2, in Vector2 pos3,
            in Vector2 uv0, in Vector2 uv1, in Vector2 uv2, in Vector2 uv3,
            Color color, bool washed)
        {
            SetTexture(texture);
            Quad(pos0, pos1, pos2, pos3, uv0, uv1, uv2, uv3, color, washed);
        }

        public void Image(Texture texture, Color color, bool washed = false)
        {
            SetTexture(texture);
            Quad(
                new Vector2(0, 0),
                new Vector2(texture.Width, 0),
                new Vector2(texture.Width, texture.Height),
                new Vector2(0, texture.Height),
                new Vector2(0, 0),
                Vector2.UnitX,
                new Vector2(1, 1),
                Vector2.UnitY,
                color, washed);
        }

        public void Image(Texture texture, in Vector2 position, Color color, bool washed = false)
        {
            SetTexture(texture);
            Quad(
                position,
                position + new Vector2(texture.Width, 0),
                position + new Vector2(texture.Width, texture.Height),
                position + new Vector2(0, texture.Height),
                new Vector2(0, 0),
                Vector2.UnitX,
                new Vector2(1, 1),
                Vector2.UnitY,
                color, washed);
        }

        public void Image(Texture texture, in Vector2 position, in Vector2 scale, in Vector2 origin, float rotation, Color color, bool washed = false)
        {
            var was = MatrixStack;

            MatrixStack = Transform2D.CreateMatrix(position, origin, scale, rotation) * MatrixStack;

            SetTexture(texture);
            Quad(
                new Vector2(0, 0),
                new Vector2(texture.Width, 0),
                new Vector2(texture.Width, texture.Height),
                new Vector2(0, texture.Height),
                new Vector2(0, 0),
                Vector2.UnitX,
                new Vector2(1, 1),
                Vector2.UnitY,
                color, washed);

            MatrixStack = was;
        }

        public void Image(Texture texture, in Rect clip, in Vector2 position, Color color, bool washed = false)
        {
            var tx0 = clip.X / texture.Width;
            var ty0 = clip.Y / texture.Height;
            var tx1 = clip.Right / texture.Width;
            var ty1 = clip.Bottom / texture.Height;

            SetTexture(texture);
            Quad(
                position,
                position + new Vector2(clip.Width, 0),
                position + new Vector2(clip.Width, clip.Height),
                position + new Vector2(0, clip.Height),
                new Vector2(tx0, ty0),
                new Vector2(tx1, ty0),
                new Vector2(tx1, ty1),
                new Vector2(tx0, ty1), color, washed);
        }

        public void Image(Texture texture, in Rect clip, in Vector2 position, in Vector2 scale, in Vector2 origin, float rotation, Color color, bool washed = false)
        {
            var was = MatrixStack;

            MatrixStack = Transform2D.CreateMatrix(position, origin, scale, rotation) * MatrixStack;

            var tx0 = clip.X / texture.Width;
            var ty0 = clip.Y / texture.Height;
            var tx1 = clip.Right / texture.Width;
            var ty1 = clip.Bottom / texture.Height;

            SetTexture(texture);
            Quad(
                new Vector2(0, 0),
                new Vector2(clip.Width, 0),
                new Vector2(clip.Width, clip.Height),
                new Vector2(0, clip.Height),
                new Vector2(tx0, ty0),
                new Vector2(tx1, ty0),
                new Vector2(tx1, ty1),
                new Vector2(tx0, ty1),
                color, washed);

            MatrixStack = was;
        }

        public void Image(Subtexture subtex, Color color, bool washed = false)
        {
            SetTexture(subtex.Texture);
            Quad(
                subtex.DrawCoords[0], subtex.DrawCoords[1], subtex.DrawCoords[2], subtex.DrawCoords[3],
                subtex.TexCoords[0], subtex.TexCoords[1], subtex.TexCoords[2], subtex.TexCoords[3],
                color, washed);
        }

        public void Image(Subtexture subtex, in Vector2 position, Color color, bool washed = false)
        {
            SetTexture(subtex.Texture);
            Quad(position + subtex.DrawCoords[0], position + subtex.DrawCoords[1], position + subtex.DrawCoords[2], position + subtex.DrawCoords[3],
                subtex.TexCoords[0], subtex.TexCoords[1], subtex.TexCoords[2], subtex.TexCoords[3],
                color, washed);
        }

        public void Image(Subtexture subtex, in Vector2 position, in Vector2 scale, in Vector2 origin, float rotation, Color color, bool washed = false)
        {
            var was = MatrixStack;

            MatrixStack = Transform2D.CreateMatrix(position, origin, scale, rotation) * MatrixStack;

            SetTexture(subtex.Texture);
            Quad(
                subtex.DrawCoords[0], subtex.DrawCoords[1], subtex.DrawCoords[2], subtex.DrawCoords[3],
                subtex.TexCoords[0], subtex.TexCoords[1], subtex.TexCoords[2], subtex.TexCoords[3],
                color, washed);

            MatrixStack = was;
        }

        public void Image(Subtexture subtex, in Rect clip, in Vector2 position, in Vector2 scale, in Vector2 origin, float rotation, Color color, bool washed = false)
        {
            var (source, frame) = subtex.GetClip(clip);
            var tex = subtex.Texture;
            var was = MatrixStack;

            MatrixStack = Transform2D.CreateMatrix(position, origin, scale, rotation) * MatrixStack;

            var px0 = -frame.X;
            var py0 = -frame.Y;
            var px1 = -frame.X + source.Width;
            var py1 = -frame.Y + source.Height;

            var tx0 = 0f;
            var ty0 = 0f;
            var tx1 = 0f;
            var ty1 = 0f;

            if (tex != null)
            {
                tx0 = source.Left / tex.Width;
                ty0 = source.Top / tex.Height;
                tx1 = source.Right / tex.Width;
                ty1 = source.Bottom / tex.Height;
            }

            SetTexture(subtex.Texture);
            Quad(
                new Vector2(px0, py0), new Vector2(px1, py0), new Vector2(px1, py1), new Vector2(px0, py1),
                new Vector2(tx0, ty0), new Vector2(tx1, ty0), new Vector2(tx1, ty1), new Vector2(tx0, ty1),
                color, washed);

            MatrixStack = was;
        }

        #endregion

        #region Text

        public void Text(SpriteFont font, string text, Color color)
        {
            Text(font, text.AsSpan(), color);
        }

        public void Text(SpriteFont font, ReadOnlySpan<char> text, Color color)
        {
            var position = new Vector2(0, font.Ascent);

            for (int i = 0; i < text.Length; i++)
            {
                if (text[i] == '\n')
                {
                    position.X = 0;
                    position.Y += font.LineHeight;
                    continue;
                }

                if (!font.Charset.TryGetValue(text[i], out var ch))
                {
                    continue;
                }

                if (ch.Image != null)
                {
                    var at = position + ch.Offset;

                    if (i < text.Length - 1 && text[i + 1] != '\n')
                    {
                        if (ch.Kerning.TryGetValue(text[i + 1], out float kerning))
                        {
                            at.X += kerning;
                        }
                    }

                    Image(ch.Image, at, color, true);
                }

                position.X += ch.Advance;
            }
        }

        public void Text(SpriteFont font, string text, Vector2 position, Color color)
        {
            PushMatrix(position);
            Text(font, text.AsSpan(), color);
            PopMatrix();
        }

        public void Text(SpriteFont font, ReadOnlySpan<char> text, Vector2 position, Color color)
        {
            PushMatrix(position);
            Text(font, text, color);
            PopMatrix();
        }

        public void Text(SpriteFont font, string text, Vector2 position, Vector2 scale, Vector2 origin, float rotation, Color color)
        {
            PushMatrix(position, scale, origin, rotation);
            Text(font, text.AsSpan(), color);
            PopMatrix();
        }

        public void Text(SpriteFont font, ReadOnlySpan<char> text, Vector2 position, Vector2 scale, Vector2 origin, float rotation, Color color)
        {
            PushMatrix(position, scale, origin, rotation);
            Text(font, text, color);
            PopMatrix();
        }

        /// <summary>
        /// Draw text on the baseline, scaled to match `size`.
        /// For example: if the font was loaded at 10pt, and you set `size = 20`, the text will be scaled x2.
        /// </summary>
        public void Text(SpriteFont font, ReadOnlySpan<char> text, Vector2 position, int size, float rotation, Color color)
        {
            float s = size / (float)font.Size;
            var scale = new Vector2(s, s);
            var origin = new Vector2(0f, font.Ascent);
            PushMatrix(position, scale, origin, rotation);
            Text(font, text, color);
            PopMatrix();
        }

        /// <summary>
        /// Draw text on the baseline, scaled to match `size`.
        /// For example: if the font was loaded at 10pt, and you set `size = 20`, the text will be scaled x2.
        /// </summary>
        public void Text(SpriteFont font, string text, Vector2 position, int size, float rotation, Color color)
        {
            float s = size / (float)font.Size;
            var scale = new Vector2(s, s);
            var origin = new Vector2(0f, font.Ascent);
            PushMatrix(position, scale, origin, rotation);
            Text(font, text.AsSpan(), color);
            PopMatrix();
        }

        /// <summary>
        /// Draws the text scaled to fit into the provided rectangle, never exceeding the max font size.
        /// </summary>
        public void TextFitted(SpriteFont font, string text, in Rect rect, float maxSize, Color color)
        {
            var textSpan = text.AsSpan();
            var size = font.SizeOf(textSpan);
            var sx = rect.Width / size.X;
            var sy = rect.Height / font.Size;
            var scale = Math.Min(maxSize / font.Size, Math.Min(sx, sy));
            var pos = rect.Size * 0.5f - size * scale * 0.5f;
            PushMatrix(Matrix3x2.CreateScale(scale) * Matrix3x2.CreateTranslation(pos));
            Text(font, textSpan, color);
            PopMatrix();
        }

        /// <summary>
        /// Draws the text scaled to fit into the provided rectangle.
        /// </summary>
        public void TextFitted(SpriteFont font, string text, in Rect rect, Color color)
        {
            var textSpan = text.AsSpan();
            var size = font.SizeOf(textSpan);
            var sx = rect.Width / size.X;
            var sy = rect.Height / font.Size;
            var scale = Math.Min(sx, sy);
            var pos = rect.Size * 0.5f - size * scale * 0.5f;
            PushMatrix(Matrix3x2.CreateScale(scale) * Matrix3x2.CreateTranslation(pos));
            Text(font, textSpan, color);
            PopMatrix();
        }

        #endregion

        #region Copy Arrays

        /// <summary>
        /// Copies the contents of a Vertex and Index array to this Batcher
        /// </summary>
        public void CopyArray(ReadOnlySpan<Vertex> vertexBuffer, ReadOnlySpan<int> indexBuffer)
        {
            // copy vertices over
            ExpandVertices(_vertexCount + vertexBuffer.Length);
            vertexBuffer.CopyTo(_vertices.AsSpan().Slice(_vertexCount));

            // copy indices over
            while (_indexCount + indexBuffer.Length >= _indices.Length)
            {
                Array.Resize(ref _indices, _indices.Length * 2);
            }

            for (int i = 0, n = _indexCount; i < indexBuffer.Length; i++, n++)
            {
                _indices[n] = _vertexCount + indexBuffer[i];
            }

            // increment
            _vertexCount += vertexBuffer.Length;
            _indexCount += indexBuffer.Length;
            _currentBatch.Elements += (uint)(vertexBuffer.Length / 3);
            _dirty = true;
        }

        #endregion

        #region Misc.

        public void CheckeredPattern(in Rect bounds, float cellWidth, float cellHeight, Color a, Color b)
        {
            var odd = false;

            for (float y = bounds.Top; y < bounds.Bottom; y += cellHeight)
            {
                var cells = 0;
                for (float x = bounds.Left; x < bounds.Right; x += cellWidth)
                {
                    var color = (odd ? a : b);
                    if (color.A > 0)
                    {
                        Rect(x, y, Math.Min(bounds.Right - x, cellWidth), Math.Min(bounds.Bottom - y, cellHeight), color);
                    }

                    odd = !odd;
                    cells++;
                }

                if (cells % 2 == 0)
                {
                    odd = !odd;
                }
            }
        }

        #endregion

        #region Internal Utils

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void PushTriangle()
        {
            while (_indexCount + 3 >= _indices.Length)
            {
                Array.Resize(ref _indices, _indices.Length * 2);
            }

            _indices[_indexCount + 0] = _vertexCount + 0;
            _indices[_indexCount + 1] = _vertexCount + 1;
            _indices[_indexCount + 2] = _vertexCount + 2;

            _indexCount += 3;
            _currentBatch.Elements++;
            _dirty = true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void PushQuad()
        {
            int index = _indexCount;
            int vert = _vertexCount;

            while (index + 6 >= _indices.Length)
            {
                Array.Resize(ref _indices, _indices.Length * 2);
            }

            _indices[index + 0] = vert + 0;
            _indices[index + 1] = vert + 1;
            _indices[index + 2] = vert + 2;
            _indices[index + 3] = vert + 0;
            _indices[index + 4] = vert + 2;
            _indices[index + 5] = vert + 3;

            _indexCount += 6;
            _currentBatch.Elements += 2;
            _dirty = true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void ExpandVertices(int index)
        {
            while (index >= _vertices.Length)
            {
                Array.Resize(ref _vertices, _vertices.Length * 2);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void Transform(ref Vector2 to, in Vector2 position, in Matrix3x2 matrix)
        {
            to.X = (position.X * matrix.M11) + (position.Y * matrix.M21) + matrix.M31;
            to.Y = (position.X * matrix.M12) + (position.Y * matrix.M22) + matrix.M32;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void VerticalFlip(ref Vector2 uv0, ref Vector2 uv1, ref Vector2 uv2, ref Vector2 uv3)
        {
            uv0.Y = 1 - uv0.Y;
            uv1.Y = 1 - uv1.Y;
            uv2.Y = 1 - uv2.Y;
            uv3.Y = 1 - uv3.Y;
        }

        #endregion
    }
}
