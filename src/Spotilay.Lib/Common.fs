module Common

open System
open System.Diagnostics
open System.IO
open Newtonsoft.Json
open WinApi

type CacheData = {
    Left: double
    Top: double
}

let private cacheFilePath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData)
let private fileName = "spotilay_cache.json"
let private cacheFileFinalPath = Path.Combine(cacheFilePath, fileName)
let private deviceStateFilePath = Path.Combine(cacheFilePath, "device_state.json")

let createData left top = { Left = left; Top = top }


let createTimer time dispatchers =
    let timer = new System.Timers.Timer(time)
    dispatchers |> Array.iter (fun x -> timer.Elapsed.Add(x))
    timer

let startTimer (timer: System.Timers.Timer) = timer.Start()
    

let saveCache cache =
    let json = JsonConvert.SerializeObject(cache)
    File.WriteAllText(cacheFileFinalPath, json)
    
    
let loadCache () =
    if File.Exists(cacheFileFinalPath) then
        let json = File.ReadAllText(cacheFileFinalPath)
        JsonConvert.DeserializeObject<CacheData>(json)
    else
        { Left = 0.0; Top = 0.0 }
        
let saveDeviceState devices =
    let json = JsonConvert.SerializeObject(devices)
    File.WriteAllText(deviceStateFilePath, json)
    
let loadDeviceState () =
    if File.Exists(deviceStateFilePath) then
        let json = File.ReadAllText(deviceStateFilePath)
        json
    else
        ""
   
let isSpotifyRunning () =
    let processes = Process.GetProcessesByName("Spotify")
    let runningCount = processes.Length = 0 |> not
    processes |> Array.iter DllExtern.disposeProc
    runningCount
    
let getSpotifyProc () =
    let proc = Process.GetProcessesByName("Spotify")
               
    let processWithTitle = proc |> Array.tryFind (fun proc -> proc.MainWindowTitle <> "")
    
    if processWithTitle = None then
        proc |> Array.tryHead
    else
        processWithTitle
 
let iterateProc () =
    let proc = Process.GetProcessesByName("Spotify") |> Array.map (fun proc -> DllExtern.getWindowText <| proc.MainWindowHandle)
    proc
    
let isTrackNameValid text =
    if String.IsNullOrEmpty (text) || text = "Spotify Premium"  then
        false
    else
        true
 
let isTrackPlaying hwnd = async {
//    let! handle = getHandle ()
    match hwnd with
    | h when IntPtr.Zero = h  -> return false
    | h when DllExtern.getWindowText (h) |> isTrackNameValid -> return true
    | _ -> return false
    
//    if handle = IntPtr.Zero then
//        return false
//        
//    let text = getWindowText (handle)
//    if String.IsNullOrEmpty (text) || text = "Spotify Premium"  then
//        return false
//    else
//        return true
    }

    
let private maxLenOfTrackName = 24
let unknownTrack = "Unknown Track"

let cutOffStr (str: String) =
    if str.Length > maxLenOfTrackName then
        let cutOff = str.Substring(0, maxLenOfTrackName - 3)
        sprintf "%s..." cutOff
    else
        str
        
let parseTrackName (str: string) =
    if String.IsNullOrEmpty (str) || str = "Spotify Premium" then
        unknownTrack
    else
        let arr = str.Split '-'
        sprintf "%s\n%s" (arr.[1].Trim() |> cutOffStr) (arr.[0].Trim()) 
let getCurrentTrackName () =
    let unknownTrack = "Unknown Track"
    let p = getSpotifyProc()
    match p with
    | Some proc ->
        using proc <| fun p ->
            if p.MainWindowTitle = "Spotify Premium" || p.MainWindowTitle = "" then
                unknownTrack
            else 
                parseTrackName proc.MainWindowTitle
            
    | None -> unknownTrack
    
let getCurrentTrackNameFromNative hwnd =
    async {
//        let! handle = getHandle ()
        if hwnd <> IntPtr.Zero then
            return DllExtern.getWindowText (hwnd) |> parseTrackName
        else
            return String.Empty
    }

