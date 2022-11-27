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
    
let private maxLenOfTrackName = 24
let unknownTrack = "N\A"

let sliceSpan (str: ReadOnlySpan<Char>) =
    if str.Length > maxLenOfTrackName then
        let cutOff = str.Slice(0, maxLenOfTrackName - 3)
        $"{cutOff.ToString()}..."
    else
        str.ToString()
        
 
let formatTrack (name: string) (artist: string) =
    if String.IsNullOrEmpty(name) && String.IsNullOrEmpty(artist) then
        String.Empty
    else
    let nameSpan = name.AsSpan()
    let trackName = sliceSpan nameSpan 
    $"{trackName}\n{artist}"

