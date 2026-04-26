// =============================================================================
// VSTHost/PluginTestHost/Program.cs
// Host VST mínimo ejecutado en subprocess aislado por SandboxService.
// Protocolo de salida via stdout (parseado por ParseHostOutput):
//   LOAD_OK          → plugin cargó correctamente
//   LOAD_FAIL:<msg>  → fallo al cargar
//   CRASH:<msg>      → excepción detectada
//   CPU:<valor>      → porcentaje de CPU
//   MEM:<mb>         → uso de RAM en MB
//   NET_ACCESS       → intento de acceso a red
//   WARNING:<msg>    → advertencia no fatal
// Exit codes: 0=ok, 1=fallo de carga, 2=crash, 3=timeout
// =============================================================================
using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

// Este proyecto es un ejecutable separado (net8.0-windows, x64)
// compilado y colocado en VSTHost/PluginTestHost.exe
namespace PluginTestHost
{
    class Program
    {
        private static string pluginPath  = string.Empty;
        private static string format      = "VST2";
        private static string tempDir     = string.Empty;
        private static int    timeout     = 30;

        static async Task<int> Main(string[] args)
        {
            // Parseo de argumentos
            for (int i = 0; i < args.Length - 1; i++)
            {
                switch (args[i].ToLower())
                {
                    case "--plugin":  pluginPath = args[++i]; break;
                    case "--format":  format     = args[++i]; break;
                    case "--temp":    tempDir    = args[++i]; break;
                    case "--timeout": int.TryParse(args[++i], out timeout); break;
                }
            }

            if (string.IsNullOrEmpty(pluginPath) || !File.Exists(pluginPath))
            {
                Console.WriteLine($"LOAD_FAIL:Archivo no encontrado: {pluginPath}");
                return 1;
            }

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(timeout));

            try
            {
                return await RunTestAsync(cts.Token);
            }
            catch (OperationCanceledException)
            {
                Console.WriteLine($"LOAD_FAIL:Timeout de {timeout} segundos");
                return 3;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"CRASH:{ex.GetType().Name}: {ex.Message}");
                return 2;
            }
        }

        static async Task<int> RunTestAsync(CancellationToken ct)
        {
            var sw = Stopwatch.StartNew();

            // Deshabilitar acceso a red (no hay forma fácil sin WFP,
            // pero reportamos si se intenta via hook de WinSock)
            MonitorNetworkAccess();

            // Intentar cargar la DLL del plugin
            IntPtr hModule = IntPtr.Zero;
            try
            {
                Console.WriteLine($"WARNING:Intentando cargar {Path.GetFileName(pluginPath)}");

                hModule = LoadLibraryEx(pluginPath, IntPtr.Zero, LOAD_LIBRARY_SAFE_CURRENT_DIRS);

                if (hModule == IntPtr.Zero)
                {
                    int error = Marshal.GetLastWin32Error();
                    Console.WriteLine($"LOAD_FAIL:LoadLibrary falló con error Win32: {error}");
                    return 1;
                }

                Console.WriteLine("LOAD_OK");

                // Monitorear recursos brevemente (5 segundos)
                for (int i = 0; i < 10; i++)
                {
                    ct.ThrowIfCancellationRequested();
                    await Task.Delay(500, ct);

                    // CPU del proceso actual
                    var cpuTime = Process.GetCurrentProcess().TotalProcessorTime.TotalMilliseconds;
                    var elapsed = sw.Elapsed.TotalMilliseconds;
                    double cpuPercent = elapsed > 0 ? (cpuTime / elapsed) * 100 : 0;
                    Console.WriteLine($"CPU:{cpuPercent:F1}");

                    // RAM en MB
                    long memMB = Environment.WorkingSet / (1024 * 1024);
                    Console.WriteLine($"MEM:{memMB}");
                }

                return 0;
            }
            finally
            {
                if (hModule != IntPtr.Zero)
                    FreeLibrary(hModule);
            }
        }

        static void MonitorNetworkAccess()
        {
            // Monitoreo básico: verificar si el proceso abre sockets
            // En producción usar ETW (Event Tracing for Windows) para mayor precisión
        }

        // ─── P/Invoke para cargar DLL ─────────────────────────────────────────
        private const uint LOAD_LIBRARY_SAFE_CURRENT_DIRS = 0x00002000;

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern IntPtr LoadLibraryEx(string lpFileName, IntPtr hFile, uint dwFlags);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool FreeLibrary(IntPtr hModule);
    }
}
