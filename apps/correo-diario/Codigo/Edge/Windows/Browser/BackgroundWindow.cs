using System.Runtime.InteropServices;
using System.Text;

namespace CorreoDiario.EdgeApp;

internal static class BackgroundWindow
{
    private delegate bool WindowVisitor(IntPtr hwnd, IntPtr state);
    [DllImport("user32.dll")] private static extern bool EnumWindows(WindowVisitor visitor, IntPtr state);
    [DllImport("user32.dll")] private static extern bool IsWindowVisible(IntPtr hwnd);
    [DllImport("user32.dll")] private static extern bool IsIconic(IntPtr hwnd);
    [DllImport("user32.dll")] private static extern IntPtr GetWindow(IntPtr hwnd, uint command);
    [DllImport("user32.dll")] private static extern bool SetForegroundWindow(IntPtr hwnd);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetClassName(IntPtr hwnd, StringBuilder name, int max);
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr hwnd, out uint pid);
    [DllImport("user32.dll")] private static extern bool ShowWindow(IntPtr hwnd, int command);
    [DllImport("user32.dll")] private static extern bool SetWindowPos(IntPtr hwnd, IntPtr after, int x, int y, int width, int height, uint flags);
    [DllImport("kernel32.dll")] private static extern IntPtr OpenProcess(uint access, bool inherit, uint pid);
    [DllImport("kernel32.dll")] private static extern bool IsProcessInJob(IntPtr process, DriverJob job, out bool belongs);
    [DllImport("kernel32.dll")] private static extern bool CloseHandle(IntPtr handle);

    // Only a top-level Chromium window belonging to this session's private job can be moved.
    private static IntPtr FindWindow(DriverJob job)
    {
        var candidates=new List<IntPtr>();
        EnumWindows((hwnd,_) =>
        {
            if(!IsWindowVisible(hwnd) || GetWindow(hwnd,4)!=IntPtr.Zero) return true; // GW_OWNER
            var name=new StringBuilder(64);
            if(GetClassName(hwnd,name,name.Capacity)==0 || name.ToString()!="Chrome_WidgetWin_1") return true;
            GetWindowThreadProcessId(hwnd,out var pid);
            var process=OpenProcess(0x1000,false,pid); // PROCESS_QUERY_LIMITED_INFORMATION
            if(process==IntPtr.Zero) return true;
            try { if(IsProcessInJob(process,job,out var belongs) && belongs) candidates.Add(hwnd); }
            finally { CloseHandle(process); }
            return true;
        },IntPtr.Zero);
        if(candidates.Count!=1)
            throw new WebExportException("ventana-del-lote","Deja abierta una sola ventana de Edge de CorreoDiario y vuelve a exportar.");
        return candidates[0];
    }

    internal static void ShowForUser(DriverJob job)
    {
        var window=FindWindow(job);
        ShowWindow(window,3); // SW_SHOWMAXIMIZED, only on the user's explicit request
        SetForegroundWindow(window);
    }

    internal static void BehindApplications(DriverJob job)
    {
        var window=FindWindow(job);
        if(IsIconic(window)) ShowWindow(window,4); // SW_SHOWNOACTIVATE
        const uint noSize=0x0001, noMove=0x0002, noActivate=0x0010, noOwnerZOrder=0x0200;
        if(!SetWindowPos(window,new IntPtr(1),0,0,0,0,noSize|noMove|noActivate|noOwnerZOrder) || IsIconic(window))
            throw new WebExportException("ventana-en-segundo-plano","Pulsa Mostrar Outlook web y vuelve a exportar. Windows no ha podido colocar su ventana detrás.");
    }
}
