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

let private cacheFilePath = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)
let private configFileName = "spotilay_config.json"
let private configFilePath = Path.Combine(cacheFilePath, configFileName)
let private deviceStateFilePath = Path.Combine(cacheFilePath, "device_state.json")

[<Literal>]
let spotifyFreeLiteral = "Spotify Free"

[<Literal>]
let spotifyPremiumLiteral = "Spotify Premium"

let createData left top = { Left = left; Top = top }

let createTimer time dispatchers =
    let timer = new System.Timers.Timer(time)
    dispatchers |> Array.iter timer.Elapsed.Add
    timer

let startTimer (timer: System.Timers.Timer) = timer.Start()
    

let loadConfig<'a> emptyConfig =
    if File.Exists(configFilePath) then
        let json = File.ReadAllText(configFilePath)
        JsonConvert.DeserializeObject<'a>(json)
    else
        File.WriteAllText(configFilePath, JsonConvert.SerializeObject(emptyConfig))
        emptyConfig
        
        
let saveDeviceState devices =
    let json = JsonConvert.SerializeObject(devices)
    File.WriteAllText(deviceStateFilePath, json)
    
let loadDeviceState () =
    if File.Exists(deviceStateFilePath) then
        let json = File.ReadAllText(deviceStateFilePath)
        json
    else
        ""

let isSpotifyRunning handle =
    handle = IntPtr.Zero |> not
    
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
    
//TODO:: use GlobalSystemMediaTransportControlsSessionManager for better track handling
let isTrackNameValid text =
    not (String.IsNullOrEmpty text) && text <> spotifyPremiumLiteral && text <> spotifyFreeLiteral
 
let isTrackPlaying hwnd = async {
//    let! handle = getHandle ()
    match hwnd with
    | h when IntPtr.Zero = h  -> return false
    | h when DllExtern.getWindowText h |> isTrackNameValid -> return true
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
let unknownTrack = "N\A"

let sliceSpan (str: ReadOnlySpan<Char>) =
    if str.Length > maxLenOfTrackName then
        let cutOff = str.Slice(0, maxLenOfTrackName - 3)
        cutOff
    else
        str
        
let cutOffStr (str: String) =
    if str.Length > maxLenOfTrackName then
        let cutOff = str.Substring(0, maxLenOfTrackName - 3)
        sprintf "%s..." cutOff
    else
        str
        
let parseTrackName (str: string) =
    
    if String.IsNullOrEmpty str || str = spotifyPremiumLiteral || str = spotifyFreeLiteral then
        unknownTrack
    else
        // let arr = str.Split '-'
        let arrSpan = str.AsSpan()
        let pos = arrSpan.IndexOf("-")
        let trackName = arrSpan.Slice(0, pos - 1)
        let trackName' = sliceSpan trackName
        let artistName = arrSpan.Slice(pos + 2, arrSpan.Length - 2 - pos)
        $"%s{trackName'.ToString()}\n%s{artistName.ToString()}" 
    // if String.IsNullOrEmpty str || str = "Spotify Premium" || str = "Spotify Free" then
    //     unknownTrack
    // else
    //     let arr = str.Split '-'
    //     sprintf "%s\n%s" (arr[1].Trim() |> cutOffStr) (arr[0].Trim()) 
// let getCurrentTrackName () =
//     let unknownTrack = "Unknown Track"
//     let p = getSpotifyProc()
//     match p with
//     | Some proc ->
//         using proc <| fun p ->
//             if p.MainWindowTitle = "Spotify Premium" || p.MainWindowTitle = "" then
//                 unknownTrack
//             else 
//                 parseTrackName proc.MainWindowTitle
//             
//     | None -> unknownTrack
    
let getCurrentTrackNameFromNative hwnd =
    async {
        if hwnd <> IntPtr.Zero then
            return DllExtern.getWindowText hwnd |> parseTrackName
        else
            return String.Empty
    }

