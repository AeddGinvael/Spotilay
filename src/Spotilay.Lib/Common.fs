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
    
let isTrackNameValid text =
    not (String.IsNullOrEmpty text) && text <> spotifyPremiumLiteral && text <> spotifyFreeLiteral
 
let isTrackPlaying hwnd = async {
    match hwnd with
    | h when IntPtr.Zero = h  -> return false
    | h when DllExtern.getWindowText h |> isTrackNameValid -> return true
    | _ -> return false

    }

    
let private maxLenOfTrackName = 24
let unknownTrack = "N\A"

let sliceSpan (str: ReadOnlySpan<Char>) =
    if str.Length > maxLenOfTrackName then
        let cutOff = str.Slice(0, maxLenOfTrackName - 3)
        $"{cutOff.ToString()}..."
    else
        str.ToString()
        
let parseTrackName (str: string) =
    
    if String.IsNullOrEmpty str || str = spotifyPremiumLiteral || str = spotifyFreeLiteral then
        unknownTrack
    else
        let arrSpan = str.AsSpan()
        let pos = arrSpan.IndexOf("-")
        let trackName = arrSpan.Slice(0, pos - 1)
        let trackName' = sliceSpan trackName
        let artistName = arrSpan.Slice(pos + 2, arrSpan.Length - 2 - pos)
        $"%s{trackName'.ToString()}\n%s{artistName.ToString()}" 
    
let getCurrentTrackNameFromNative hwnd =
    async {
        if hwnd <> IntPtr.Zero then
            return DllExtern.getWindowText hwnd |> parseTrackName
        else
            return String.Empty
    }
    
let formatTrack (name: string) (artist: string) =
    if String.IsNullOrEmpty(name) && String.IsNullOrEmpty(artist) then
        String.Empty
    else
    let nameSpan = name.AsSpan()
    let trackName = sliceSpan nameSpan 
    $"{trackName}\n{artist}"

