using ImGuiNET;
using System;
using System.Numerics;

namespace xivclone.Windows
{
    internal class UiExtensions
    {
        public static void Spinner(string label, float radius, float thickness, uint color)
        {
            var style = ImGui.GetStyle();
            var pos = ImGui.GetCursorScreenPos();
            var size = new Vector2(radius * 2, radius * 2);

            ImGui.Dummy(size); // Reserve space for the spinner

            var drawList = ImGui.GetWindowDrawList();
            drawList.PathClear();

            int numSegments = 30;
            float start = MathF.Abs(MathF.Sin((float)ImGui.GetTime() * 1.8f) * (numSegments - 5));

            float aMin = MathF.PI * 2.0f * start / numSegments;
            float aMax = MathF.PI * 2.0f * (numSegments - 3) / numSegments;

            var center = new Vector2(pos.X + size.X * 0.5f, pos.Y + size.Y * 0.5f);

            for (int i = 0; i <= numSegments; i++)
            {
                float a = aMin + (i / (float)numSegments) * (aMax - aMin);
                float time = (float)ImGui.GetTime();
                drawList.PathLineTo(new Vector2(
                    center.X + MathF.Cos(a + time * 8f) * radius,
                    center.Y + MathF.Sin(a + time * 8f) * radius));
            }

            drawList.PathStroke(color, ImDrawFlags.None, thickness);
        }

    }
}
