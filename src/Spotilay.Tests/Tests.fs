module Tests

open System
open System.Threading.Tasks
open ABI.System
open Windows.Foundation
open Xunit
open Windows.Media.Control

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
let ``test`` () = task {
    let getPeriods () =
        let defaultDevice = getDefaultDevice ()
        getRawSessions defaultDevice
        |> List.filter isFit
        |> List.map mapSession
    let mutable periods = []
    for i in 1 .. 10 do
        periods <- periods @ getPeriods()
        do! Task.Delay(TimeSpan.FromSeconds(1))
    let chromePeriods = periods |> List.filter (fun x -> x.Identifier = "chrome.exe")
    let window = chromePeriods |> List.windowed 3
    Assert.NotEmpty(chromePeriods)
    Assert.True(true)
}