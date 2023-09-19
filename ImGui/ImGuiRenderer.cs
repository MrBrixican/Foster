using ImGuiNET;
using System.Diagnostics;

namespace Foster.Framework;

/// <summary>
/// ImGui renderer for use with Foster
/// </summary>
public class ImGuiRenderer
{
    private ImGuiBatch _batcher = new();

    // Textures
    private Dictionary<IntPtr, Texture> _loadedTextures = new();

    private int _textureId;
    private IntPtr? _fontTextureId;

    public ImGuiRenderer()
    {
        var context = ImGui.CreateContext();
        ImGui.SetCurrentContext(context);
        ImGui.GetIO().BackendFlags |= ImGuiBackendFlags.RendererHasVtxOffset;

        SetupInput();
    }

    #region ImGuiRenderer

    /// <summary>
    /// Creates a texture and loads the font data from ImGui. Should be called when the <see cref="GraphicsDevice" /> is initialized but before any rendering is done
    /// </summary>
    public virtual unsafe void RebuildFontAtlas()
    {
        // Get font texture from ImGui
        var io = ImGui.GetIO();
        io.Fonts.GetTexDataAsRGBA32(out byte* pixelData, out int width, out int height, out int bytesPerPixel);
        Debug.Assert(bytesPerPixel == 4, $"Expected 4 bytes per pixel, found {bytesPerPixel}");

        // Create and register the texture
        var tex2d = new Texture(width, height, new Span<byte>(pixelData, width * height * bytesPerPixel));

        // Should a texture already have been build previously, unbind it first so it can be deallocated
        if (_fontTextureId.HasValue) UnbindTexture(_fontTextureId.Value);

        // Bind the new texture to an ImGui-friendly id
        _fontTextureId = BindTexture(tex2d);

        // Let ImGui know where to find the texture
        io.Fonts.SetTexID(_fontTextureId.Value);
        io.Fonts.ClearTexData(); // Clears CPU side texture data
    }

    /// <summary>
    /// Creates a pointer to a texture, which can be passed through ImGui calls such as <see cref="ImGui.Image" />. That pointer is then used by ImGui to let us know what texture to draw
    /// </summary>
    public virtual IntPtr BindTexture(Texture texture)
    {
        var id = new IntPtr(_textureId++);

        _loadedTextures.Add(id, texture);

        return id;
    }

    /// <summary>
    /// Removes a previously created texture pointer, releasing its reference and allowing it to be deallocated
    /// </summary>
    public virtual void UnbindTexture(IntPtr textureId)
    {
        _loadedTextures.Remove(textureId);
    }

    /// <summary>
    /// Sets up ImGui for a new frame, should be called at frame start
    /// </summary>
    public virtual void BeforeLayout()
    {
        ImGui.GetIO().DeltaTime = Time.Delta;

        UpdateInput();

        ImGui.NewFrame();
    }

    /// <summary>
    /// Asks ImGui for the generated geometry data and sends it to the graphics pipeline, should be called after the UI is drawn using ImGui.** calls
    /// </summary>
    public virtual void AfterLayout()
    {
        ImGui.Render();

        unsafe { RenderDrawData(ImGui.GetDrawData()); }
    }

    #endregion ImGuiRenderer

    #region Setup & Update

    /// <summary>
    /// Setup key input event handler.
    /// </summary>
    protected virtual void SetupInput()
    {
        var io = ImGui.GetIO();

        Input.OnTextEvent += (a) =>
        {
            if (a == '\t') return;
            io.AddInputCharacter(a);
        };
    }

    /// <summary>
    /// Sends Foster input state to ImGui
    /// </summary>
    protected virtual void UpdateInput()
    {
        if (!App.Focused) return;

        var io = ImGui.GetIO();

        var mouse = Input.Mouse;
        var keyboard = Input.Keyboard;
        io.AddMousePosEvent(mouse.X, mouse.Y);
        io.AddMouseButtonEvent(0, mouse.LeftDown);
        io.AddMouseButtonEvent(1, mouse.RightDown);
        io.AddMouseButtonEvent(2, mouse.MiddleDown);

        io.AddMouseWheelEvent(
            (mouse.Wheel.X),
            (mouse.Wheel.Y));

        for (var i = 0; i < _keyMappings.Length; i++)
        {
            var mapping = _keyMappings[i];
            var lastState = _lastKeyState[i];
            var state = keyboard.Down(mapping.Item2);

            // Reduce interop calls, if possible
            if(lastState != state)
            {
                io.AddKeyEvent(mapping.Item1, state);
            }
            
            _lastKeyState[i] = state;
        }

        io.AddKeyEvent(ImGuiKey.ModShift, keyboard.Shift);
        io.AddKeyEvent(ImGuiKey.ModAlt, keyboard.Alt);
        io.AddKeyEvent(ImGuiKey.ModCtrl, keyboard.Ctrl);
        io.AddKeyEvent(ImGuiKey.ModSuper, keyboard.Down(Keys.LeftOS) || keyboard.Down(Keys.RightOS));

        io.DisplaySize = new (App.WidthInPixels, App.HeightInPixels); //Maybe?
        io.DisplayFramebufferScale = new(1f * App.WidthInPixels / App.Width, 1f * App.HeightInPixels / App.Height);//App.ContentScale; //Maybe?
    }

    private static readonly (ImGuiKey, Keys)[] _keyMappings = new[]
    {
        (ImGuiKey.Tab, Keys.Tab),
        (ImGuiKey.LeftArrow, Keys.Left),
        (ImGuiKey.RightArrow, Keys.Right),
        (ImGuiKey.UpArrow, Keys.Up),
        (ImGuiKey.DownArrow, Keys.Down),
        (ImGuiKey.PageUp, Keys.PageUp),
        (ImGuiKey.PageDown, Keys.PageDown),
        (ImGuiKey.Home, Keys.Home),
        (ImGuiKey.End, Keys.End),
        (ImGuiKey.Insert, Keys.Insert),
        (ImGuiKey.Delete, Keys.Delete),
        (ImGuiKey.Backspace, Keys.Backspace),
        (ImGuiKey.Space, Keys.Space),
        (ImGuiKey.Enter, Keys.Enter),
        (ImGuiKey.Escape, Keys.Escape),
        (ImGuiKey.LeftCtrl, Keys.LeftControl),
        (ImGuiKey.LeftShift, Keys.LeftShift),
        (ImGuiKey.LeftAlt, Keys.LeftAlt),
        (ImGuiKey.LeftSuper, Keys.LeftOS),
        (ImGuiKey.RightCtrl, Keys.RightControl),
        (ImGuiKey.RightShift, Keys.RightShift),
        (ImGuiKey.RightAlt, Keys.RightAlt),
        (ImGuiKey.RightSuper, Keys.RightOS),
        (ImGuiKey.Menu, Keys.Menu),
        (ImGuiKey._0, Keys.D0),
        (ImGuiKey._1, Keys.D1),
        (ImGuiKey._2, Keys.D2),
        (ImGuiKey._3, Keys.D3),
        (ImGuiKey._4, Keys.D4),
        (ImGuiKey._5, Keys.D5),
        (ImGuiKey._6, Keys.D6),
        (ImGuiKey._7, Keys.D7),
        (ImGuiKey._8, Keys.D8),
        (ImGuiKey._9, Keys.D9),
        (ImGuiKey.A, Keys.A),
        (ImGuiKey.B, Keys.B),
        (ImGuiKey.C, Keys.C),
        (ImGuiKey.D, Keys.D),
        (ImGuiKey.E, Keys.E),
        (ImGuiKey.F, Keys.F),
        (ImGuiKey.G, Keys.G),
        (ImGuiKey.H, Keys.H),
        (ImGuiKey.I, Keys.I),
        (ImGuiKey.J, Keys.J),
        (ImGuiKey.K, Keys.K),
        (ImGuiKey.L, Keys.L),
        (ImGuiKey.M, Keys.M),
        (ImGuiKey.N, Keys.N),
        (ImGuiKey.O, Keys.O),
        (ImGuiKey.P, Keys.P),
        (ImGuiKey.Q, Keys.Q),
        (ImGuiKey.R, Keys.R),
        (ImGuiKey.S, Keys.S),
        (ImGuiKey.T, Keys.T),
        (ImGuiKey.U, Keys.U),
        (ImGuiKey.V, Keys.V),
        (ImGuiKey.W, Keys.W),
        (ImGuiKey.X, Keys.X),
        (ImGuiKey.Y, Keys.Y),
        (ImGuiKey.Z, Keys.Z),
        (ImGuiKey.F1, Keys.F1),
        (ImGuiKey.F2, Keys.F2),
        (ImGuiKey.F3, Keys.F3),
        (ImGuiKey.F4, Keys.F4),
        (ImGuiKey.F5, Keys.F5),
        (ImGuiKey.F6, Keys.F6),
        (ImGuiKey.F7, Keys.F7),
        (ImGuiKey.F8, Keys.F8),
        (ImGuiKey.F9, Keys.F9),
        (ImGuiKey.F10, Keys.F10),
        (ImGuiKey.F11, Keys.F11),
        (ImGuiKey.F12, Keys.F12),
        (ImGuiKey.Apostrophe, Keys.Apostrophe),
        (ImGuiKey.Comma, Keys.Comma),
        (ImGuiKey.Minus, Keys.Minus),
        (ImGuiKey.Period, Keys.Period),
        (ImGuiKey.Slash, Keys.Slash),
        (ImGuiKey.Semicolon, Keys.Semicolon),
        (ImGuiKey.Equal, Keys.Equals),
        (ImGuiKey.LeftBracket, Keys.LeftBracket),
        (ImGuiKey.Backslash, Keys.Backslash),
        (ImGuiKey.RightBracket, Keys.RightBracket),
        (ImGuiKey.GraveAccent, Keys.Tilde),
        (ImGuiKey.CapsLock, Keys.Capslock),
        (ImGuiKey.ScrollLock, Keys.ScrollLock),
        (ImGuiKey.NumLock, Keys.Numlock),
        (ImGuiKey.PrintScreen, Keys.PrintScreen),
        (ImGuiKey.Pause, Keys.Pause),
        (ImGuiKey.Keypad0, Keys.Keypad0),
        (ImGuiKey.Keypad1, Keys.Keypad1),
        (ImGuiKey.Keypad2, Keys.Keypad2),
        (ImGuiKey.Keypad3, Keys.Keypad3),
        (ImGuiKey.Keypad4, Keys.Keypad4),
        (ImGuiKey.Keypad5, Keys.Keypad5),
        (ImGuiKey.Keypad6, Keys.Keypad6),
        (ImGuiKey.Keypad7, Keys.Keypad7),
        (ImGuiKey.Keypad8, Keys.Keypad8),
        (ImGuiKey.Keypad9, Keys.Keypad9),
        (ImGuiKey.KeypadDecimal, Keys.KeypadPeroid),
        (ImGuiKey.KeypadDivide, Keys.KeypadDivide),
        (ImGuiKey.KeypadMultiply, Keys.KeypadMultiply),
        (ImGuiKey.KeypadSubtract, Keys.KeypadMinus),
        (ImGuiKey.KeypadAdd, Keys.KeypadPlus),
        (ImGuiKey.KeypadEnter, Keys.KeypadEnter),
        (ImGuiKey.KeypadEqual, Keys.KeypadEquals),
    };

    private readonly bool[] _lastKeyState = new bool[_keyMappings.Length];

    #endregion Setup & Update

    #region Internals

    /// <summary>
    /// Gets the geometry as set up by ImGui and sends it to the graphics device
    /// </summary>
    private void RenderDrawData(ImDrawDataPtr drawData)
    {
        // Handle cases of screen coordinates != from framebuffer coordinates (e.g. retina displays)
        drawData.ScaleClipRects(ImGui.GetIO().DisplayFramebufferScale);

        RenderCommandLists(drawData);
    }

    private unsafe void RenderCommandLists(ImDrawDataPtr drawData)
    {
        if (drawData.TotalVtxCount == 0)
        {
            return;
        }

        _batcher.Clear();

        for (int n = 0; n < drawData.CmdListsCount; n++)
        {
            ImDrawListPtr cmdList = drawData.CmdListsRange[n];

            var offset = _batcher.VertexCount;
            var vertexBuffer = new Span<ImGuiBatch.Vertex>((void*)cmdList.VtxBuffer.Data, cmdList.VtxBuffer.Size);
            var indexBuffer = new Span<ushort>((void*)cmdList.IdxBuffer.Data, cmdList.IdxBuffer.Size);

            _batcher.CopyArray(vertexBuffer, new Span<ushort>(), offset);

            for (int cmdi = 0; cmdi < cmdList.CmdBuffer.Size; cmdi++)
            {
                ImDrawCmdPtr drawCmd = cmdList.CmdBuffer[cmdi];

                if (drawCmd.ElemCount == 0)
                {
                    continue;
                }

                if (!_loadedTextures.ContainsKey(drawCmd.TextureId))
                {
                    throw new InvalidOperationException($"Could not find a texture with id '{drawCmd.TextureId}', please check your bindings");
                }

                _batcher.SetTexture(_loadedTextures[drawCmd.TextureId]);

                _batcher.SetScissor(new RectInt(
                    (int)drawCmd.ClipRect.X,
                    (int)drawCmd.ClipRect.Y,
                    (int)(drawCmd.ClipRect.Z - drawCmd.ClipRect.X),
                    (int)(drawCmd.ClipRect.W - drawCmd.ClipRect.Y)
                    ));

                _batcher.CopyArray(
                    new Span<ImGuiBatch.Vertex>(),
                    indexBuffer.Slice((int)drawCmd.IdxOffset, (int)drawCmd.ElemCount),
                    offset + (int)drawCmd.VtxOffset
                    );
            }
        }

        _batcher.Render();
    }

    #endregion Internals
}
