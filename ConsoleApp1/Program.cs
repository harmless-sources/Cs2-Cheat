using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.Net;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using ConsoleApp1;
using FreeBhopEz;
using static FreeBhopEz.EntityVariables;
namespace Bhop
{
    class Bhop
    {
        public static ConcurrentQueue<EntityVariables> edntities = new();
        public static EntityVariables localPdlayer = new();
        public static EntityVariables InstanceEntity = new EntityVariables();
        private static bool CanJump = false;
        public static Vector3 CalcAngle(Vector3 src, Vector3 dst)
        {
            Vector3 delta = dst - src;
            float hyp = (float)Math.Sqrt(delta.X * delta.X + delta.Y * delta.Y);
            float pitch = (float)(Math.Atan2(-delta.Z, hyp) * (180.0 / Math.PI));
            float yaw = (float)(Math.Atan2(delta.Y, delta.X) * (180.0 / Math.PI));
            return new Vector3(pitch, yaw, 0f);
        }
        static void Main()
        {
            Helpers Proc = new Helpers("cs2");
            IntPtr client = Proc.GetModuleBase("client.dll");
            Console.WriteLine($"Client Base: 0x{client.ToInt64():X}");

            // offsets
            int dwEntityList = 0x1A1F670;
            int m_iszPlayerName = 0x660;
            int dwViewMatrix = 0x1A89070;
            int dwLocalPlayerPawn = 0x1874040;
            int m_iIDEntIndex = 0x1458;
            int m_vOldOrigin = 0x1324;
            int m_iTeamNum = 0x3E3;
            int m_lifeState = 0x348;
            int m_hPlayerPawn = 0x814;
            int m_vecViewOffset = 0xCB0;
            int m_iHealth = 0x344;
            int dwViewAngles = 0x1A93300;
            int m_entitySpottedState = 0x23D0;
            var m_flFlashBangTime = 0x13F8;

            var entityList = Proc.ReadPointer(client, dwEntityList);
            var localPawn = Proc.ReadPointer(client, dwLocalPlayerPawn);

            int team = Proc.ReadInt(localPawn, m_iTeamNum);
            int entIndex = Proc.ReadInt(localPawn, m_iIDEntIndex);
            var renderer = new Renderer();
            var screenSize = renderer.screenSize;

            new Thread(() => renderer.Start().Wait()).Start();

            var entities = new List<EntityVariables>();
            var localPlayer = new EntityVariables();

            Thread triggerThread = new Thread(() =>
            {
                const int hotkey = 0x46, clickdown = 0x02, clickup = 0x04;

                while (true)
                {
                    if (!renderer.EnableTrigger) continue;

                    var entitylist = Proc.ReadPointer(client, dwEntityList);
                    var local = Proc.ReadPointer(client, dwLocalPlayerPawn);
                    if (local == IntPtr.Zero) continue;

                    int localteam = Proc.ReadInt(local, m_iTeamNum);
                    int index = Proc.ReadInt(local, m_iIDEntIndex);
                    if (index == -1) continue;

                    var listentry = Proc.ReadPointer(entitylist, 0x8 * ((index & 0x7FFF) >> 9) + 0x10);
                    var target = Proc.ReadPointer(listentry, 0x78 * (index & 0x1FF));
                    if (target == IntPtr.Zero) continue;

                    int targetteam = Proc.ReadInt(target, m_iTeamNum);
                    int health = Proc.ReadInt(target, m_iHealth);

                    if (localteam == targetteam || health <= 0 || GetAsyncKeyState(hotkey) >= 0 || !InstanceEntity.IsSpotted) continue;

                    var localpos = Proc.ReadVec(local, m_vOldOrigin) + Proc.ReadVec(local, m_vecViewOffset);
                    var enemypos = Proc.ReadVec(target, m_vOldOrigin); enemypos.Z += 52f;

                    var angle = CalcAngle(localpos, enemypos);
                    Thread.Sleep(3);
                    mouse_event(clickdown, 0, 0, 0, 0);
                    mouse_event(clickup, 0, 0, 0, 0);
                    Thread.Sleep(3);
                }
            });
            triggerThread.IsBackground = true;
            triggerThread.Start();

            Thread AntiFlashThread = new Thread(() =>
            {
                while (true)
                {
                    if (renderer.AntiFlash)
                    {
                        Thread.Sleep(34);
                        var MyPawn = Proc.ReadPointer(client, dwLocalPlayerPawn);
                        if (Proc.ReadFloat(MyPawn, m_flFlashBangTime) > 0)
                            Proc.WriteFloat(MyPawn, m_flFlashBangTime, 0);
                        Thread.Sleep(34);
                    }
                }
            });
            AntiFlashThread.IsBackground = true;
            AntiFlashThread.Start(); 

            Thread BhopThread = new Thread(() =>
            {
                while (true)
                {
                    if (renderer.EnableBhop && ((GetAsyncKeyState(0x20) & 0x8000) != 0))
                        if (!CanJump)
                        {
                            Thread.Sleep(5);
                            Proc.WriteMemory(new IntPtr(client.ToInt64() + 0x186CD50), 65537);
                            CanJump = true;
                        }
                        else
                        {
                            Thread.Sleep(5);
                            Proc.WriteMemory(new IntPtr(client.ToInt64() + 0x186CD50), 256);
                            CanJump = false;
                        }
                    }
            });
            BhopThread.IsBackground = true;
            BhopThread.Start();

            Thread AimBotThread = new Thread(() =>
            {
                const int aimkey = 0x02, shootkey = 0x01;
                while (true)
                {
                    if (renderer.EnableAimBot && GetAsyncKeyState(aimkey) < 0)
                    {
                        var entitylist = Proc.ReadPointer(client, dwEntityList);
                        var listbase = Proc.ReadPointer(entitylist, 0x10);
                        var local = Proc.ReadPointer(client, dwLocalPlayerPawn);
                        var localorigin = Proc.ReadVec(local, m_vOldOrigin);
                        var localoffset = Proc.ReadVec(local, m_vecViewOffset);
                        var eyepos = localorigin + localoffset;

                        float fovlimit = renderer.AimFOV, smooth = renderer.AimSmooth;
                        var viewmatrix = Proc.ReadMatrix(client + dwViewMatrix);
                        var screencenter = new Vector2(renderer.screenSize.X / 2f, renderer.screenSize.Y / 2f);

                        IntPtr target = IntPtr.Zero;
                        Vector3 bestangle = Vector3.Zero;

                        for (int i = 0; i < 64; i++)
                        {
                            var controller = Proc.ReadPointer(listbase, i * 0x78);
                            if (controller == IntPtr.Zero) continue;

                            var handle = Proc.ReadInt(controller, m_hPlayerPawn);
                            if (handle == 0) continue;

                            var sublist = Proc.ReadPointer(entitylist, 0x8 * ((handle & 0x7FFF) >> 9) + 0x10);
                            var pawn = Proc.ReadPointer(sublist, 0x78 * (handle & 0x1FF));
                            if (pawn == IntPtr.Zero || Proc.ReadInt(pawn, m_lifeState) != 256) continue;

                            int team = Proc.ReadInt(pawn, m_iTeamNum);
                            int health = Proc.ReadInt(pawn, m_iHealth);
                            bool visible = Proc.ReadBool(pawn + m_entitySpottedState + 0x8);
                            if (team == Proc.ReadInt(local, m_iTeamNum) || health <= 0 || !visible) continue;

                            var pos = Proc.ReadVec(pawn, m_vOldOrigin);
                            var offset = Proc.ReadVec(pawn, m_vecViewOffset);
                            var aimpos = new Vector3(pos.X, pos.Y, pos.Z + offset.Z * 0.8f);

                            var screenpos = WorldScreen.WorldToScreen(viewmatrix, aimpos, renderer.screenSize);
                            if (screenpos.X <= 0 || screenpos.X >= renderer.screenSize.X || screenpos.Y <= 0 || screenpos.Y >= renderer.screenSize.Y) continue;

                            float fov = Vector2.Distance(screencenter, screenpos);
                            if (fov < fovlimit)
                            {
                                fovlimit = fov;
                                target = pawn;
                                bestangle = CalcAngle(eyepos, aimpos);
                            }
                        }

                        if (target != IntPtr.Zero)
                        {
                            var current = Proc.ReadVec(client + dwViewAngles);
                            var smoothed = new Vector3(
                                current.X + (bestangle.X - current.X) * smooth,
                                current.Y + (bestangle.Y - current.Y) * smooth,
                                0
                            );
                            Proc.WriteVec(client + dwViewAngles, smoothed);

                            if (GetAsyncKeyState(shootkey) < 0)
                                Helpers.mouse_event(0x0001, 0, 8, 0, 0);
                        }
                    }
                    Thread.Sleep(1);
                }
            });
            AimBotThread.IsBackground = true;
            AimBotThread.Start();

            while (true)
            {
                InstanceEntity.IsSpotted = Proc.ReadBool(client + m_entitySpottedState + 0x8);
                entities.Clear();
                var GetList = Proc.ReadPointer(client, dwEntityList);
                var listEntry = Proc.ReadPointer(GetList, 0x10);
                var myPawn = Proc.ReadPointer(client, dwLocalPlayerPawn);

                localPlayer.team = Proc.ReadInt(myPawn, m_iTeamNum);
                localPlayer.position = Proc.ReadVec(myPawn, m_vOldOrigin);

                var viewMatrix = Proc.ReadMatrix(client + dwViewMatrix);

                for (int i = 0; i < 64; i++)
                {
                    var controller = Proc.ReadPointer(listEntry, i * 0x78);
                    if (controller == IntPtr.Zero) continue;

                    int handle = Proc.ReadInt(controller, m_hPlayerPawn);
                    if (handle == 0) continue;

                    var entry2 = Proc.ReadPointer(GetList, 0x8 * ((handle & 0x7FFF) >> 9) + 0x10);
                    var pawn = Proc.ReadPointer(entry2, 0x78 * (handle & 0x1FF));
                    if (pawn == IntPtr.Zero || Proc.ReadInt(pawn, m_lifeState) != 256) continue;

                    var pos = Proc.ReadVec(pawn, m_vOldOrigin);
                    var offset = Proc.ReadVec(pawn, m_vecViewOffset);

                    entities.Add(new EntityVariables
                    {
                        team = Proc.ReadInt(pawn, m_iTeamNum),
                        health = Proc.ReadInt(pawn, m_iHealth),
                        Name = Proc.ReadString(controller, m_iszPlayerName, 16),
                        position = pos,
                        viewOffset = offset,
                        position2D = WorldScreen.WorldToScreen(viewMatrix, pos, screenSize),
                        viewPosition2D = WorldScreen.WorldToScreen(viewMatrix, pos + offset, screenSize)
                    });
                }

                
                localPdlayer = localPlayer;
                edntities = new ConcurrentQueue<EntityVariables>(entities);
            }

            [DllImport("user32.dll")]
            static extern short GetAsyncKeyState(int vKey);

            [DllImport("user32.dll", CharSet = CharSet.Auto, CallingConvention = CallingConvention.StdCall)]
            static extern void mouse_event(long dwFlags, long dx, long dy, long cButtons, long dwExtraInfo);


        }
    }
}