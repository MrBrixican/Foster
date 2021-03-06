﻿using System;
using System.Numerics;

namespace Foster.Framework
{
    /// <summary>
    /// A TextureRegion, representing a rectangular region of a Texture
    /// </summary>
    public class TextureRegion
    {
        /// <summary>
        /// The Texture coordinates. These are set automatically based on the Source rectangle
        /// </summary>
        public Vector2[] TexCoords { get; } = new Vector2[4];

        /// <summary>
        /// The draw coordinates. These are set automatically based on the Source and Frame rectangle
        /// </summary>
        public Vector2[] DrawCoords { get; } = new Vector2[4];

        /// <summary>
        /// The Texture this TextureRegion is... a TextureRegion of
        /// </summary>
        public Texture? Texture
        {
            get => _texture;
            set
            {
                if (_texture != value)
                {
                    _texture = value;
                    UpdateCoords();
                }
            }
        }

        /// <summary>
        /// The source rectangle to sample from the Texture
        /// </summary>
        public Rect Source
        {
            get => _source;
            set
            {
                _source = value;
                UpdateCoords();
            }
        }

        /// <summary>
        /// The frame of the TextureRegion. This is useful if you trim transparency and want to store the original size of the image
        /// For example, if the original image was (64, 64), but the trimmed version is (32, 48), the Frame may be (-16, -8, 64, 64)
        /// </summary>
        public Rect Frame
        {
            get => _frame;
            set
            {
                _frame = value;
                UpdateCoords();
            }
        }

        /// <summary>
        /// The Draw Width of the TextureRegion
        /// </summary>
        public float Width => _frame.Width;

        /// <summary>
        /// The Draw Height of the TextureRegion
        /// </summary>
        public float Height => _frame.Height;

        private Texture? _texture;
        private Rect _frame;
        private Rect _source;

        public TextureRegion()
        {

        }

        public TextureRegion(Texture texture)
            : this(texture, new Rect(0, 0, texture.Width, texture.Height))
        {

        }

        public TextureRegion(Texture texture, Rect source)
            : this(texture, source, new Rect(0, 0, source.Width, source.Height))
        {

        }

        public TextureRegion(Texture texture, Rect source, Rect frame)
        {
            this._texture = texture;
            this._source = source;
            this._frame = frame;

            UpdateCoords();
        }

        public void Reset(Texture texture, Rect source, Rect frame)
        {
            this._texture = texture;
            this._source = source;
            this._frame = frame;

            UpdateCoords();
        }

        public (Rect Source, Rect Frame) GetClip(in Rect clip)
        {
            (Rect Source, Rect Frame) result;

            result.Source = (clip + Source.Position + Frame.Position).OverlapRect(Source);

            result.Frame.X = MathF.Min(0, Frame.X + clip.X);
            result.Frame.Y = MathF.Min(0, Frame.Y + clip.Y);
            result.Frame.Width = clip.Width;
            result.Frame.Height = clip.Height;

            return result;
        }

        public (Rect Source, Rect Frame) GetClip(float x, float y, float w, float h)
        {
            return GetClip(new Rect(x, y, w, h));
        }

        public TextureRegion GetClipTextureRegion(Rect clip)
        {
            var (source, frame) = GetClip(clip);
            return new TextureRegion(Texture!, source, frame);
        }

        private void UpdateCoords()
        {
            DrawCoords[0].X = -_frame.X;
            DrawCoords[0].Y = -_frame.Y;
            DrawCoords[1].X = -_frame.X + _source.Width;
            DrawCoords[1].Y = -_frame.Y;
            DrawCoords[2].X = -_frame.X + _source.Width;
            DrawCoords[2].Y = -_frame.Y + _source.Height;
            DrawCoords[3].X = -_frame.X;
            DrawCoords[3].Y = -_frame.Y + _source.Height;

            if (_texture != null)
            {
                var tx0 = _source.X / _texture.Width;
                var ty0 = _source.Y / _texture.Height;
                var tx1 = _source.Right / _texture.Width;
                var ty1 = _source.Bottom / _texture.Height;

                TexCoords[0].X = tx0;
                TexCoords[0].Y = ty0;
                TexCoords[1].X = tx1;
                TexCoords[1].Y = ty0;
                TexCoords[2].X = tx1;
                TexCoords[2].Y = ty1;
                TexCoords[3].X = tx0;
                TexCoords[3].Y = ty1;
            }
        }

    }
}
