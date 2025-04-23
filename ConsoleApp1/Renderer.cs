using Bhop;
using ClickableTransparentOverlay;
using ConsoleApp1;
using ImGuiNET;
using System;
using System.Collections.Concurrent;
using System.Numerics;
using System.Reflection.Metadata;
using System.Runtime.InteropServices;
using static Bhop.Bhop;

namespace FreeBhopEz
{
    public class Renderer : Overlay
    {
        public bool EnableESP;
        public bool AntiFlash;
        public bool EnableTrigger;
        public bool EnableBhop;
        public bool EnableAimBot;
        public bool WIP;

        public float AimFOV = 300f;
        public float AimSmooth = 0.5f;

        public Vector2 screenSize = GetScreenSize();
        private ImDrawListPtr drawList;
        private readonly Vector4 enemyColor = new(1, 0, 2, 1);
        private readonly Vector4 teamColor = new(1, 1, 0, 1);
        private bool StyleReady = false;
        private bool _showGui = true;
        private float _hintStart = -1f;

        protected override void Render()
        {
            if (_hintStart < 0f)
                _hintStart = (float)ImGui.GetTime();

            if (ImGui.IsKeyPressed(ImGuiKey.Insert))
                _showGui = !_showGui;

            if (ImGui.GetTime() - _hintStart < 5f)
            {
                ImGui.SetNextWindowPos(new Vector2(screenSize.X - 240, 10));
                ImGui.SetNextWindowBgAlpha(0.4f);
                ImGui.Begin("##ToggleHint", ImGuiWindowFlags.NoDecoration | ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoInputs);
                ImGui.Text("Press INSERT to toggle GUI");
                ImGui.End();
            }

            if (_showGui)
            {
                if (!StyleReady)
                {
                    Style();
                    StyleReady = true;
                }

                IntPtr hWnd = Helpers.FindWindow(null, "Overlay");
                SetWindowDisplayAffinity(hWnd, 0x00000014);

                ImGui.Begin("Harmless", ImGuiWindowFlags.AlwaysAutoResize);

                if (ImGui.CollapsingHeader("Visuals"))
                {
                    ImGui.Checkbox("Enable ESP", ref EnableESP);
                    ImGui.Checkbox("Anti Flash", ref AntiFlash);
                }

                if (ImGui.CollapsingHeader("Combat"))
                {
                    ImGui.Checkbox("TriggerBot", ref EnableTrigger);
                    ImGui.Checkbox("AimBot", ref EnableAimBot);
                    ImGui.SliderFloat("Aimbot FOV", ref AimFOV, 0f, MathF.Min(screenSize.X, screenSize.Y) / 2f);
                    ImGui.SliderFloat("Aimbot Smoothness", ref AimSmooth, 0.01f, 1f);
                }

                if (ImGui.CollapsingHeader("Movement"))
                    ImGui.Checkbox("Bhop Helper", ref EnableBhop);

                if (ImGui.CollapsingHeader("Misc"))
                    ImGui.Checkbox("WIP", ref WIP);

                ImGui.Separator();
                ImGui.TextColored(new Vector4(0.6f, 0.6f, 0.6f, 1), "Made By Blobfish & Subzero");
                ImGui.End();
            }

            DrawOverlay(screenSize);
            drawList = ImGui.GetWindowDrawList();

            if (EnableAimBot)
            {
                var center = new Vector2(screenSize.X / 2f, screenSize.Y / 2f);
                uint circleColor = ImGui.GetColorU32(new Vector4(1f, 1f, 1f, 0.5f));
                drawList.AddCircle(center, AimFOV, circleColor, 64, 1f);
            }

            if (!EnableESP)
                return;

            foreach (var entity in Bhop.Bhop.edntities)
            {
                if (entity.position == Vector3.Zero || entity.position == Bhop.Bhop.localPdlayer.position)
                    continue;
                if (EntityOnScreen(entity))
                    DrawBox(entity);
            }
        }

        private static Vector2 GetScreenSize() =>
            new(GetSystemMetrics(0), GetSystemMetrics(1));

        private bool EntityOnScreen(EntityVariables e) =>
            e.position2D.X > 0 && e.position2D.X < screenSize.X &&
            e.position2D.Y > 0 && e.position2D.Y < screenSize.Y;

        private void DrawBox(EntityVariables e)
        {
            float h = e.position2D.Y - e.viewPosition2D.Y, w = h / 3f, bw = 4f;
            Vector2 t = new(e.viewPosition2D.X - w, e.viewPosition2D.Y);
            Vector2 b = new(e.position2D.X + w, e.position2D.Y);
            Vector4 c = localPdlayer.team == e.team ? teamColor : enemyColor;
            drawList.AddRect(t, b, ImGui.ColorConvertFloat4ToU32(c), 0f, ImDrawFlags.None, 1.5f);
            float hp = Math.Clamp(e.health / 100f, 0f, 1f);
            Vector2 bgT = new(t.X - bw - 2, t.Y);
            Vector2 bgB = new(bgT.X + bw, b.Y);
            drawList.AddRectFilled(bgT, bgB, ImGui.GetColorU32(new Vector4(0, 0, 0, 0.6f)));
            Vector2 fillT = new(bgB.X - bw, b.Y - h * hp);
            Vector2 fillB = new(bgB.X, b.Y);
            Vector4 hpC = new(1 - hp, hp, 0, 1);
            drawList.AddRectFilled(fillT, fillB, ImGui.GetColorU32(hpC));
            string name = e.Name ?? "";
            name = System.Text.RegularExpressions.Regex.Replace(name, @"[^\x20-\x7E]", "");
            Vector2 namePosition = new(t.X + (b.X - t.X) / 2f, b.Y + 5f);
            ImGui.GetWindowDrawList().AddText(namePosition, ImGui.GetColorU32(Vector4.One), name);
        }

        private void DrawOverlay(Vector2 size)
        {
            ImGui.SetNextWindowSize(size);
            ImGui.SetNextWindowPos(Vector2.Zero);
            ImGui.Begin("overlay", ImGuiWindowFlags.NoDecoration |
                                    ImGuiWindowFlags.NoBackground |
                                    ImGuiWindowFlags.NoBringToFrontOnFocus |
                                    ImGuiWindowFlags.NoMove |
                                    ImGuiWindowFlags.NoInputs |
                                    ImGuiWindowFlags.NoCollapse |
                                    ImGuiWindowFlags.NoScrollbar |
                                    ImGuiWindowFlags.NoScrollWithMouse);
        }

        [DllImport("user32.dll")]
        private static extern int GetSystemMetrics(int nIndex);
        [DllImport("user32.dll", SetLastError = true)]
        public static extern uint SetWindowDisplayAffinity(IntPtr hWnd, uint dwAffinity);

        private void Style()
        {
            var style = ImGui.GetStyle();
            style.WindowRounding = 5.0f;
            style.FrameRounding = 3.0f;
            style.GrabRounding = 2.0f;
            style.ScrollbarRounding = 3.0f;
            style.WindowPadding = new Vector2(10, 10);

            var colors = style.Colors;

            Vector4 Black = new(0.05f, 0.05f, 0.05f, 1.00f);
            Vector4 DarkPurple = new(0.25f, 0.0f, 0.5f, 1.00f);
            Vector4 LightPurple = new(0.5f, 0.2f, 1.0f, 1.00f);
            Vector4 White = new(1f, 1f, 1f, 1f);

            colors[(int)ImGuiCol.Text] = White;
            colors[(int)ImGuiCol.WindowBg] = Black;
            colors[(int)ImGuiCol.ChildBg] = Black;
            colors[(int)ImGuiCol.PopupBg] = Black;
            colors[(int)ImGuiCol.Border] = DarkPurple;
            colors[(int)ImGuiCol.FrameBg] = DarkPurple;
            colors[(int)ImGuiCol.FrameBgHovered] = LightPurple;
            colors[(int)ImGuiCol.FrameBgActive] = LightPurple;
            colors[(int)ImGuiCol.TitleBg] = DarkPurple;
            colors[(int)ImGuiCol.TitleBgActive] = LightPurple;
            colors[(int)ImGuiCol.TitleBgCollapsed] = Black;
            colors[(int)ImGuiCol.Button] = DarkPurple;
            colors[(int)ImGuiCol.ButtonHovered] = LightPurple;
            colors[(int)ImGuiCol.ButtonActive] = LightPurple;
            colors[(int)ImGuiCol.Header] = DarkPurple;
            colors[(int)ImGuiCol.HeaderHovered] = LightPurple;
            colors[(int)ImGuiCol.HeaderActive] = LightPurple;
            colors[(int)ImGuiCol.CheckMark] = White;
            colors[(int)ImGuiCol.SliderGrab] = LightPurple;
            colors[(int)ImGuiCol.SliderGrabActive] = White;
            colors[(int)ImGuiCol.Separator] = DarkPurple;
            colors[(int)ImGuiCol.ResizeGrip] = LightPurple;
            colors[(int)ImGuiCol.ResizeGripHovered] = White;
            colors[(int)ImGuiCol.ResizeGripActive] = White;
        }
    }
}