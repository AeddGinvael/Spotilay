namespace WinApi

open System
open System.Diagnostics
open System.Drawing
open System.Management
open System.Runtime.InteropServices
open System.Security
open System.Text

module DllExtern =
    

    
    let swpNosize = 0x0001 |> uint32
    let swpNomove = 0x0002 |> uint32
    let wsExTransparent = 0x00000020
    
    let wsMaximizebox = 0x10000
    let wsMinimizebox = 0x20000
    let gwlExstyle = -20
    
    let gwlStyle = -16
    
    let hwndBottom = IntPtr(1)
    
    type ShowWindowEnum =
    | Hide = 0
    | ShowNormal = 1
    | ShowMinimized = 2
    | ShowMaximized = 3
    | Maximize = 3
    | ShowNormalNoActive = 4
    | Show = 5
    | Minimize = 6
    | ShowMinNoActivate = 7
    | ShowNoActivate = 8
    | Restore = 9
    | ShowDefault = 10
    | ForceMinimized = 11
    
    [<Struct>]
    type WindowPlacement = {
        lenght : int
        flag : int
        showCmd : int
        minPos : Point
        maxPos : Point
        normalPos : Point
    }
    
    [<SuppressUnmanagedCodeSecurity>]
    [<DllImport("user32.dll")>]
    extern int GetWindowLong (IntPtr hwnd, int index)
    
    [<SuppressUnmanagedCodeSecurity>]
    [<DllImport("user32.dll")>]
    extern int SetWindowLong (IntPtr hwnd, int index, int newStyle)
    
    [<DllImport("gdi32.dll", SetLastError=true)>]
    extern bool DeleteObject(IntPtr hObj)
    
    [<SuppressUnmanagedCodeSecurity>]
    [<DllImport("user32.dll")>]
    extern bool SetWindowPos (IntPtr hwnd, IntPtr hwndInsertAfter, int x, int y, int cx, int cy, uint32 flags)
    
    
    [<StructLayout(LayoutKind.Sequential)>]
    type Win32Point = {
        X : int
        Y : int
    }
    
    [<SuppressUnmanagedCodeSecurity>]
    [<DllImport("user32.dll")>]
    [<MarshalAs(UnmanagedType.Bool)>]
    extern bool GetCursorPos([<In>][<Out>] Win32Point pt)
    
    let HwndBroadcast = 0xffff
    
    [<SuppressUnmanagedCodeSecurity>]
    [<DllImport("user32.dll")>]
    extern int RegisterWindowMessage(string msg)
    
    let wmShowMe = RegisterWindowMessage("WM_SHOWME")
    
    [<SuppressUnmanagedCodeSecurity>]
    [<DllImport("user32.dll")>]
    extern bool PostMessage (IntPtr hwnd, int msg, IntPtr wparam, IntPtr lparam)
    
    [<SuppressUnmanagedCodeSecurity>]
    [<DllImport("user32.dll", EntryPoint="SendMessage")>]
    extern IntPtr SendMessage(IntPtr hWnd, int Msg, IntPtr wParam, IntPtr lParam)
    
    [<SuppressUnmanagedCodeSecurity>]
    [<DllImport("user32.dll", EntryPoint="SendMessage", CharSet=CharSet.Auto)>]
    extern bool SendMessageWithBuilder(IntPtr hWnd, uint32 Msg, IntPtr wParam, [<Out>] StringBuilder lParam)
    
    [<SuppressUnmanagedCodeSecurity>]
    [<DllImport("wininet.dll")>]
    extern bool InternetGetConnectedState ([<Out>]int description, int reservedValue)
    
    [<SuppressUnmanagedCodeSecurity>]
    [<DllImport("user32.dll", CharSet=CharSet.Unicode, SetLastError=true)>]
    extern int GetWindowText(IntPtr hwnd, [<Out>]StringBuilder builder, int maxCount)
    
    [<SuppressUnmanagedCodeSecurity>]
    [<DllImport("user32.dll")>]
    extern IntPtr FindWindow(string className, string windowTitle)
    
    [<SuppressUnmanagedCodeSecurity>]
    [<DllImport("user32.dll")>]
    extern IntPtr FindWindowEx(IntPtr parentHandle, IntPtr child, string className)
    
    [<SuppressUnmanagedCodeSecurity>]
    [<DllImport("user32.dll")>]
    [<MarshalAs(UnmanagedType.Bool)>]
    extern bool ShowWindow(IntPtr hwnd, ShowWindowEnum flag)
    
    [<SuppressUnmanagedCodeSecurity>]
    [<DllImport("user32.dll")>]
    extern int SetForegroundWindow(IntPtr hwnd)
    
    [<SuppressUnmanagedCodeSecurity>]
    [<DllImport("user32.dll")>]    
    extern bool GetWindowPlacement(IntPtr hwnd, int& placement)
    
    [<SuppressUnmanagedCodeSecurity>]
    [<DllImport("user32.dll")>]    
    extern int GetClassName (IntPtr hwnd, StringBuilder className, int maxCount)

    [<SuppressUnmanagedCodeSecurity>]
    [<DllImport("kernel32.dll")>]    
    extern uint GetProcessId(IntPtr hwnd)
    
    [<SuppressUnmanagedCodeSecurity>]
    [<DllImport("user32.dll")>]    
    extern uint GetWindowThreadProcessId (IntPtr hwnd, uint& processId)
    
    type EnumWindowsProc = delegate of IntPtr * IntPtr -> bool

    [<SuppressUnmanagedCodeSecurity>]
    [<DllImport("user32.dll")>]    
    extern bool EnumWindows (EnumWindowsProc enumF, IntPtr lparam)     

    let deleteObject obj = DeleteObject(obj)
    let getWindowLong (hwnd, index) = GetWindowLong(hwnd, index)
    let setWindowLong (hwnd, index, newStyle) = SetWindowLong (hwnd, index, newStyle)
    let getInternetStatus () =
        InternetGetConnectedState(0, 0)

    let getMousePosition () =
        let mutable point = { X = 0; Y = 0 }
        GetCursorPos(point) |> ignore
        (point.X |> float, point.Y |> float)
        
    let sendWpfWindowBack(hwnd) =
        let b = swpNosize ||| swpNomove
        SetWindowPos(hwnd, hwndBottom, 0, 0, 0, 0, b)
        
    let setWindowExTransparent(hwnd) =
        let extendedStyle = GetWindowLong(hwnd, gwlExstyle)
        SetWindowLong(hwnd, gwlExstyle, extendedStyle ||| wsExTransparent)
    
    let unsetWindowExTransparent(hwnd) =
        let extendedStyle = GetWindowLong(hwnd, gwlExstyle)
        SetWindowLong(hwnd, gwlExstyle, extendedStyle &&& ~~~wsExTransparent)
        
    let getClassName hwnd =
        let size = SendMessage(hwnd, 0x000E, IntPtr.Zero, IntPtr.Zero) |> int
        if size = 0 then
            String.Empty
        else
            let b = StringBuilder(size + 1)
            GetClassName(hwnd, b, b.Capacity) |> ignore
            b.ToString()
            
    let getWindowText hwnd =
        let titleSize = SendMessage(hwnd, 0x000E, IntPtr.Zero, IntPtr.Zero) |> int
        if titleSize = 0 then
            String.Empty
        else
            let b = StringBuilder(titleSize + 1)
            SendMessageWithBuilder(hwnd, 0x000D |> uint32, b.Capacity |> IntPtr, b) |> ignore
            b.ToString()
    let isSpotifyHandle (p: Process) = p.MainWindowTitle <> ""        
    let disposeProc (proc: Process) =
        if isSpotifyHandle proc |> not then
            proc.Close()
            proc.Dispose()
        ()
    let getProcCount name =
        let spotifyProcess = Process.GetProcessesByName(name)
        let count = spotifyProcess.Length
        spotifyProcess |> Array.iter disposeProc
        count
    
    let tryFindProc name =
        let procs = Process.GetProcessesByName(name)
        let proc = procs |> Array.tryFind isSpotifyHandle
        procs |> Array.iter disposeProc
        proc
   
   
    let getWindowProcessId hwnd =
        let mutable pId = 0u
        GetWindowThreadProcessId(hwnd, &pId) |> ignore
        pId |> int
    let enumWindowsForSpotify () = async {
       let mutable spotifyHwnd = IntPtr.Zero
       let callback = EnumWindowsProc(fun hwnd lparam ->
            let pId = getWindowProcessId hwnd
            use spotifyProc = Process.GetProcessById(pId)
            
            let isProcSpotify (proc: Process) hwnd =
                if getWindowText hwnd <> String.Empty && proc.ProcessName = "Spotify" then
                    true
                else
                    false

            if spotifyProc = null then
                true
            else if isProcSpotify spotifyProc hwnd then
                spotifyHwnd <- hwnd
                false
                
            else if getProcCount "Spotify" = 0 then
                false
            else
                true
            )
       EnumWindows(callback, IntPtr.Zero) |> ignore
       return spotifyHwnd
    }

       
    let getHandle () = async {
        do! Async.SwitchToThreadPool ()
        if getProcCount "Spotify" > 0 then
            let windowHandle = tryFindProc "Spotify"
            let res = match windowHandle with
                        | Some p -> async { return p.MainWindowHandle } 
                        | None -> enumWindowsForSpotify ()
            return! res
        else
            return IntPtr.Zero
    }

                        
    let wmAppcommand = 793
    
    //Virtual-Key Codes
    type MediaCodes =
    | MediaPause = 917504
    | MediaNext = 720896
    | MediaPrev = 786432
    | VolumeDown = 589824
    | VolumeUp = 655360
    
//    PlayPause = 917504,
//    Mute = 524288,
//    VolumeDown = 589824,
//    VolumeUp = 655360,
//    Stop = 851968,
//    PreviousTrack = 786432,
//    NextTrack = 720896

    let sendPlayPause target =
        if target = IntPtr.Zero then
            IntPtr.Zero
        else
            let pause = (int)MediaCodes.MediaPause
            SendMessage(target, wmAppcommand, IntPtr.Zero, IntPtr(pause))
    let sendNextTrack target =
        if target = IntPtr.Zero then
            IntPtr.Zero
        else
            let next = (int)MediaCodes.MediaNext
            SendMessage(target, wmAppcommand, IntPtr.Zero, IntPtr(next))
     
    let sendPrevTrack target =
        if target = IntPtr.Zero then
            IntPtr.Zero
        else
            let prev = (int)MediaCodes.MediaPrev
            SendMessage(target, wmAppcommand, IntPtr.Zero, IntPtr(prev))
            
    let sendVolumeUp target =
        if target = IntPtr.Zero then
            IntPtr.Zero
        else
            let up = MediaCodes.VolumeUp |> int
            SendMessage(target, wmAppcommand, IntPtr.Zero, IntPtr(up))
            
    let sendVolumeDown target =
        if target = IntPtr.Zero then
            IntPtr.Zero
        else
            let up = MediaCodes.VolumeDown |> int
            SendMessage(target, wmAppcommand, IntPtr.Zero, IntPtr(up))
            
module ProcessEvents =
    
    let watchEventProcess dispatcherOnSpotify =
        let startProcesses = WqlEventQuery("SELECT * FROM Win32_ProcessStartTrace")
        let endProcesses = WqlEventQuery("SELECT * FROM Win32_ProcessStopTrace")
        let startWatcher = new ManagementEventWatcher(startProcesses)
        let stopWatcher = new ManagementEventWatcher(endProcesses)
        let f (args: EventArrivedEventArgs) =
            let procName = args.NewEvent.Properties.["ProcessName"].Value.ToString()
            if procName = "Spotify.exe" then
                dispatcherOnSpotify()
            
        let f2 (args: EventArrivedEventArgs) =
            let procName = args.NewEvent.Properties.["ProcessName"].Value.ToString()
            if procName = "Spotify.exe" then
                dispatcherOnSpotify()
        
        startWatcher.EventArrived.Add(f)
        stopWatcher.EventArrived.Add(f2)
        stopWatcher.Start()
        startWatcher.Start()
    
    