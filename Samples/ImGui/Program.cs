using Foster.Framework;
using ImGuiNET;
using System.Diagnostics;
using System.Numerics;

class Program
{
    public static void Main()
    {
        App.Register<Game>();
        App.Run("ImGui", 1280, 720);
    }
}

class Game : Module
{
    private ImGuiRenderer imGuiRenderer = new();

    private Stopwatch timer = new();
    private double average = 0;

    private Stopwatch timerAfterLayout = new();
    private double averageAfterLayout = 0;

    private int vtxCount = 0;

    public override void Startup()
    {
        App.VSync = true;
        Time.FixedStep = false;

        imGuiRenderer.RebuildFontAtlas(); // Call once at startup
    }

    public override void Render()
    {
        timer.Restart();

        imGuiRenderer.BeforeLayout(); // Call before all ImGui calls for a frame

        Graphics.Clear(Color.White);

        ImGui.ShowDemoWindow();

        ShowStressTestWindow();

        timerAfterLayout.Restart();

        imGuiRenderer.AfterLayout(); // Call after all ImGui calls for a frame

        timerAfterLayout.Stop();

        timer.Stop();

        // Exponential smoothing
        average = .95 * average + .05 * timer.Elapsed.TotalMilliseconds;
        averageAfterLayout = .95 * averageAfterLayout + .05 * timerAfterLayout.Elapsed.TotalMilliseconds;
    }

    // This is a slight modification of ocornut's large mesh support test code:
    // https://github.com/ocornut/imgui/issues/2591#issuecomment-496954460
    void ShowStressTestWindow()
    {
        if (!ImGui.Begin("Dear ImGui Stress Test", ImGuiWindowFlags.AlwaysAutoResize))
        {
            ImGui.End();
            return;
        }

        ImGui.SliderInt("VtxCount", ref vtxCount, 0, 400000);
        vtxCount = vtxCount / 80 * 80;

        var draw_list = ImGui.GetWindowDrawList();
        {
            var rectCount = vtxCount / (4 * 2);
            ImGui.Text($"Rects: {rectCount}");
            var p = ImGui.GetCursorScreenPos();
            for (int n = 0; n < rectCount; n++)
            {
                float off_x = (n % 100) * 3.0f;
                float off_y = (n % 100) * 1.0f;
                var col = new Color(
                    (byte)((n * 17) & 255),
                    (byte)((n * 83) & 255),
                    (byte)((n * 59) & 255),
                    255);
                draw_list.AddRectFilled(new Vector2(p.X + off_x, p.Y + off_y), new Vector2(p.X + off_x + 50, p.Y + off_y + 50), col.ABGR);
            }
            ImGui.Dummy(new Vector2(300 + 50, 100 + 50));
        }
        {
            var textCount = vtxCount / (10 * 4 * 2);
            ImGui.Text($"Characters: {textCount*10}");
            var p = ImGui.GetCursorScreenPos();
            for (int n = 0; n < textCount; n++)
            {
                float off_x = (n % 100) * 3.0f;
                float off_y = (n % 100) * 1.0f;
                var col = new Color(
                    (byte)((n * 17) & 255),
                    (byte)((n * 83) & 255),
                    (byte)((n * 59) & 255),
                    255);
                draw_list.AddText(new Vector2(p.X + off_x, p.Y + off_y), col.ABGR, "ABCDEFGHIJ");
            }
            ImGui.Dummy(new Vector2(300 + 50, 100 + 20));
        }
        ImGui.Text($"ImGuiRenderer.AfterLayout (ms): {averageAfterLayout:0.00}");
        ImGui.Text($"Total render (ms): {average:0.00}");

        ImGui.End();
    }
}