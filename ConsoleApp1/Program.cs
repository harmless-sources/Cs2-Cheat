using System.Diagnostics;
using System.Net;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using ConsoleApp1;

namespace Bhop
{
    class Bhop
    {
        /* SPACE */
        [DllImport("user32.dll")]
        private static extern short GetAsyncKeyState(int vKey);
        private const int VK_SPACE = 0x20;

        private static bool CanJump = false;

        private static Memory cs2;

        static void Main(string[] args)
        {
            if (Offsets.dwForceJump == 0)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("No Offset");
                Console.ResetColor();
                Console.WriteLine("Press enter to exit");
                Console.ReadLine();
                return;
            }
            while (true)
            {
                cs2 = new Memory("cs2.exe");
                if (cs2.IsValid)
                {
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine("Got cs2 process");
                    if (Offsets.dwForceJump != null)
                    {
                        Console.WriteLine("Got offset");
                    }
                    Console.ResetColor();
                    cs2.ClientBase = cs2.ModuleFinder("client.dll");
                    if (cs2.ClientBase == IntPtr.Zero)
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine("No Client");
                        Console.ResetColor();
                        return;
                    }
                    break;
                }
                else
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("no cs2");
                    Console.ResetColor();
                }
            }

            while (true)
            {
                if ((GetAsyncKeyState(VK_SPACE) & 0x8000) != 0)
                {
                    if (!CanJump)
                    {
                        Thread.Sleep(10);
                        cs2.WriteMemory(cs2.ClientBase + Offsets.dwForceJump, 65537);
                        CanJump = true;
                    }
                    else
                    {
                        Thread.Sleep(10);
                        cs2.WriteMemory(cs2.ClientBase + Offsets.dwForceJump, 256);
                        CanJump = false;
                    }
                }
            }
        }
        static class Offsets
        {
            public static int dwForceJump;

            static Offsets()
            {
                try
                {
                    WebClient webClient = new WebClient();
                    string AutoOffset = "https://raw.githubusercontent.com/a2x/cs2-dumper/refs/heads/main/output/buttons.hpp";
                    string content = webClient.DownloadString(AutoOffset);
                    string Offset = "0x186CD60";

                    if (content.Contains(Offset))
                    {
                        dwForceJump = Convert.ToInt32(Offset, 16);
                    }
                    else
                    {
                        Console.WriteLine("Offset not found.");
                    }
                }
                catch (Exception ex)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("Something went wrong: " + ex.Message);
                    Console.ResetColor();
                    dwForceJump = 0;
                }
            }
        }

        class Memory
        {
            public IntPtr ProcessHandle { get; private set; }
            public IntPtr ClientBase { get; set; }
            private Process process;
            public bool IsValid { get; private set; }

            [DllImport("kernel32.dll", SetLastError = true)]
            static extern bool WriteProcessMemory(IntPtr hProc, IntPtr addr, byte[] buf, int size, out int written);

            public Memory(string name)
            {
                var procs = Process.GetProcessesByName(System.IO.Path.GetFileNameWithoutExtension(name));
                if (IsValid = procs.Length > 0)
                {
                    process = procs[0];
                    ProcessHandle = process.Handle;
                }
            }

            public IntPtr ModuleFinder(string module) =>
                Array.Find(process.Modules.Cast<ProcessModule>().ToArray(), m => m.ModuleName.Equals(module, StringComparison.OrdinalIgnoreCase))?.BaseAddress ?? IntPtr.Zero;

            public void WriteMemory<T>(IntPtr addr, T val)
            {
                int size = Marshal.SizeOf<T>();
                byte[] buffer = new byte[size];
                IntPtr ptr = Marshal.AllocHGlobal(size);
                Marshal.StructureToPtr(val, ptr, false);
                Marshal.Copy(ptr, buffer, 0, size);
                Marshal.FreeHGlobal(ptr);
                WriteProcessMemory(ProcessHandle, addr, buffer, size, out _);
            }
        }
    }
}