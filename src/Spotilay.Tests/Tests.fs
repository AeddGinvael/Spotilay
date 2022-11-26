module Tests

open System
open System.Collections.Generic
open System.Threading.Tasks
open ABI.System
open Spotilay.Lib
open Windows.Foundation
open Xunit
open Windows.Media.Control
open MediaControlApi

open AudioApi

let await<'a> (op: IAsyncOperation<'a>) : Async<'a> =
   op.AsTask() |> Async.AwaitTask
type ControlSession = GlobalSystemMediaTransportControlsSession
[<Fact>]
let ``get media info using transport control`` () = task {
    let managerManager = GlobalSystemMediaTransportControlsSessionManager.RequestAsync().GetResults()
    let sessions = managerManager.GetSessions()
    let spotifySes = sessions |> Seq.find (fun x -> x.SourceAppUserModelId = "Spotify.exe")
    
    let propsChanged1 =
        let handler (ses: ControlSession) args =
            let props = spotifySes.TryGetMediaPropertiesAsync() |> await |> Async.RunSynchronously
            ()
        
        TypedEventHandler<GlobalSystemMediaTransportControlsSession, MediaPropertiesChangedEventArgs>(handler)
    spotifySes.add_MediaPropertiesChanged propsChanged1
    let tryGetMediaPropertiesAsync = spotifySes.TryGetMediaPropertiesAsync()
    let globalSystemMediaTransportControlsSessionPlaybackInfo = spotifySes.GetPlaybackInfo()
    let globalSystemMediaTransportControlsSessionTimelineProperties = spotifySes.GetTimelineProperties()
    let mediaProps = tryGetMediaPropertiesAsync.GetResults()
    do! Task.Delay(TimeSpan.FromMinutes(1))
    Assert.True(true)
}

[<Fact>]
let ``create media control api`` () =
    let mediaSession = createMediaSession "Spotify.exe"
    
    
    Assert.True(mediaSession |> Option.isSome)



[<Fact>]
let ``test`` () = task {
    let getPeriods () =
        let defaultDevice = getDefaultDevice ()
        getRawSessions defaultDevice
        |> Array.filter isFit
        |> Array.map mapSession
    let periods = List<SoundSource>()
    for i in 1 .. 10 do
        periods.AddRange(getPeriods())
        do! Task.Delay(TimeSpan.FromSeconds(1))
    let chromePeriods = periods |> Seq.filter (fun x -> x.Identifier = "chrome.exe")
    let window = chromePeriods |> Seq.windowed 3
    Assert.NotEmpty(chromePeriods)
    Assert.True(true)
}