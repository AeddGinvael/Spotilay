module Spotilay

open System
open System.Threading
open System.Windows
open AudioApi
open Elmish
open Elmish.WPF
open Common
open SpotifyControls
open Spotilay.Lib
open WinApi

[<AutoOpen>]
module Types =
  
  open MediaControlApi
  type OverlayMsg =
  | Nothing
  | Stop
  | NextTrack
  | PrevTrack
  | SetProcTrackPlaying of bool
  | SetTrackName of string
  | SetSpotifyHandle of IntPtr
  | CallSpotifyGetHandle of IntPtr
  | Fail of exn 
 
  type MainMsg =
  | ShowOverlay
  | HideOverlay
  | CloseOverlay
  | ExitApp
  | SetTracker of SoundTracker
  | SettingsDispatch
  | UpdateAudioSource of SoundEvent
  | Fail of exn
  | Next
   
  [<Struct>]
  type OverlayModel =  {
      IsTrackPlaying  : bool
      CurrTrackName   : string
  }
  
  type Config = {
    MuteOnSoundSource: string []
  }
  
  let defaultConfig = {
    MuteOnSoundSource = [||]
  }
  
  [<Struct>]
  type Model =
    {
      OverlayWindow : WindowState<string>
      OverlayState  : OverlayModel
      SpotifyHandle : IntPtr
      Tracker       : SoundTracker option
      Config        : Config
      Controller   : MediaSession
    }
    
    
  type AllMsg =
  | Main of MainMsg
  | Overlay of OverlayMsg
  | Fail of exn
  | Nothing
  | Update of Model
    
module OverlayWindow =  
      
  type OverlayCmd =
      | NextCmd of Model
      | PrevCmd of Model
      | PauseCmd of Model
      | SettingsDispatchCmd of Model
      | UpdateAudioSource of (Model * SoundEvent)
      | SpotifyHandleCmd of Model * IntPtr
  let init () =
      {
        IsTrackPlaying = false
        CurrTrackName = unknownTrack
      }
  
  let update (msg: OverlayMsg) (m: Model) =
    match msg with
    | OverlayMsg.Nothing -> m, []
    | Stop -> m, [PauseCmd m]
    | PrevTrack -> m, [PrevCmd m]
    | NextTrack -> m, [NextCmd m]
    | SetProcTrackPlaying flag -> { m with OverlayState = { m.OverlayState with IsTrackPlaying = flag } }, []
    | SetTrackName name -> { m with OverlayState = { m.OverlayState with CurrTrackName = name} }, []
    | SetSpotifyHandle hwnd -> { m with SpotifyHandle = hwnd }, []
    | CallSpotifyGetHandle handle -> m, [SpotifyHandleCmd (m, handle)]
    | OverlayMsg.Fail _ -> m, []
    
  let bindStopBtn state =
    match state.OverlayState.IsTrackPlaying with
    | true -> "Stop"
    | false -> "Play"
    
  let wrapAsMsg a = AllMsg.Overlay a
  let bindings (): Binding<Model, AllMsg> list = [
    "Stop"        |> Binding.cmd (Stop |> wrapAsMsg)
    "Prev"        |> Binding.cmd (PrevTrack |> wrapAsMsg)
    "Next"        |> Binding.cmd (NextTrack |> wrapAsMsg)
    "StopBtnKind" |> Binding.oneWay bindStopBtn
    "TrackName"   |> Binding.oneWay (fun m -> m.OverlayState.CurrTrackName)
  ]
  
  let next (model: Model) =
    Application.Current.Dispatcher.Invoke(fun () ->
      let ctx = SynchronizationContext.Current
      async {
        do! Async.SwitchToContext ctx
        let! success = tryNext model.SpotifyHandle
        return AllMsg.Main MainMsg.Next
      }
    )
    
  let prev (model: Model) =
    Application.Current.Dispatcher.Invoke(fun () ->
      let ctx = SynchronizationContext.Current
      async {
        do! Async.SwitchToContext ctx
        let! success = tryPrev model.SpotifyHandle
        return AllMsg.Nothing
      }
    )
    
  let pause (model: Model) =
    Application.Current.Dispatcher.Invoke(fun () ->
      let ctx = SynchronizationContext.Current
      async {
        do! Async.SwitchToContext ctx
        let! success = tryPause model.SpotifyHandle
        return AllMsg.Nothing
      }
    )
    
  let muteSpotify = 
      let mutable muted = false
      
      let cached (model: Model) isSounding  =
          let trackPlaying = model.OverlayState.IsTrackPlaying
          match muted, isSounding with
          | true, true  -> async { return AllMsg.Nothing }
          | false, true -> async {
              if trackPlaying then
                let! _ = tryPause model.SpotifyHandle
                muted <- true
              return AllMsg.Nothing
            }
          | false, false -> async { return AllMsg.Nothing }
          | true, false -> async {
              if not trackPlaying then
                let! _ = tryPause model.SpotifyHandle
                muted <- false
              return AllMsg.Nothing
            }
          
      cached

  let audioSource (state: (Model * SoundEvent)) =
    Application.Current.Dispatcher.Invoke(fun () ->
      let ctx = SynchronizationContext.Current
      async {
       let model, event = state
       return! match event with
               | Sound e -> muteSpotify model true
               | Silence e -> muteSpotify model false
       }
      )

  //TODO:: broken
  let settingsDispatch (model: Model) =
    let isSpotifyRunnin = isSpotifyRunning model.SpotifyHandle
    let modelClosedOption =
      match isSpotifyRunnin with
      | true ->  { model with OverlayWindow = WindowState.Visible "" }
      | false -> { model with OverlayWindow = WindowState.Hidden "" }
      
    let update = AllMsg.Update modelClosedOption
    update
   
  let spotifyHandleHwnd (handle: IntPtr) (model: Model) =
    Application.Current.Dispatcher.Invoke(fun () ->
      let ctx = SynchronizationContext.Current
      async {
        do! Async.SwitchToContext ctx
        model.Controller.updateState ()
        if handle <> model.SpotifyHandle then
          return AllMsg.Overlay <| OverlayMsg.SetSpotifyHandle handle
        else
          return AllMsg.Nothing
      }
    )

module App =
  open OverlayWindow
  open MediaControlApi

  let init () =
    { OverlayWindow = WindowState.Closed
      OverlayState = init()
      SpotifyHandle = getSpotifyHandle ()
      Tracker = None
      Config = { MuteOnSoundSource = [||] }
      Controller = MediaSession("Spotify.exe") },
    []
  let updateMain msg m =
    match msg with
    | Next -> m, []
    | ShowOverlay -> { m with OverlayWindow = WindowState.Visible "" }, []
    | HideOverlay -> { m with OverlayWindow = WindowState.Hidden "" }, []
    | CloseOverlay -> { m with OverlayWindow = WindowState.Closed }, []
    | ExitApp ->
      Application.Current.Shutdown()
      m, []
    | SettingsDispatch -> m, [ OverlayCmd.SettingsDispatchCmd m]
    | SetTracker s -> { m with Tracker = Some s }, []
    | MainMsg.UpdateAudioSource soundEvent -> m, [ OverlayCmd.UpdateAudioSource (m, soundEvent)]
    | MainMsg.Fail exn -> m, []
  
  let update (msg: AllMsg) m =
    match msg with
    | AllMsg.Overlay msg' -> update msg' m
    | AllMsg.Main msg' -> updateMain msg' m
    | AllMsg.Nothing -> m, []
    | AllMsg.Fail exn -> m, []
    | AllMsg.Update model -> model, []
  

  let exit () =
    Application.Current.Shutdown()
    
  let updateFunc f m = Cmd.OfFunc.either f m id AllMsg.Fail
  let asFunc f = Cmd.OfFunc.either f () id AllMsg.Fail
  let asAsync f a = Cmd.OfAsync.either f a id AllMsg.Fail

  let toCmd = function
  | OverlayCmd.NextCmd             model -> asAsync next model 
  | OverlayCmd.PrevCmd             model -> asAsync prev model
  | OverlayCmd.PauseCmd            model -> asAsync pause model
  | OverlayCmd.SettingsDispatchCmd model -> updateFunc settingsDispatch model
  | OverlayCmd.UpdateAudioSource   event -> Cmd.OfAsync.either audioSource event id AllMsg.Fail
  | OverlayCmd.SpotifyHandleCmd    (model, handle) -> asAsync (spotifyHandleHwnd handle) model
  
  
  let settingDispatcher (dispatch : Dispatch<AllMsg>) =
    let settingsDispatch _ = dispatch (AllMsg.Main SettingsDispatch)
    createTimer 2000. [| settingsDispatch |] |> startTimer
  
  let spotifyStateDispatcher (model: Model) (dispatch : Dispatch<AllMsg>) =

    let propChanged props =
      let trackName = formatTrack props.Name props.Artist
      let setTrackName =
        match trackName with
        | "" -> AllMsg.Overlay <| OverlayMsg.SetTrackName unknownTrack
        | _ -> AllMsg.Overlay <| OverlayMsg.SetTrackName trackName
      dispatch setTrackName
      ()
      
    let timelineChanged props =
      ()
      
    let playbackChanged props =
      let setProcTrack = AllMsg.Overlay <| OverlayMsg.SetProcTrackPlaying props.IsPlaying
      dispatch setProcTrack
      ()

    model.Controller.eventMediaProps.Add propChanged
    model.Controller.eventMediaTimeline.Add timelineChanged
    model.Controller.eventPlayback.Add playbackChanged
    model.Controller.updateState ()
    ProcessEvents.watchEventProcess (fun handle -> dispatch (AllMsg.Overlay <| OverlayMsg.CallSpotifyGetHandle handle))
    dispatch (AllMsg.Main MainMsg.ShowOverlay)
    
  

  let soundSourceDetectDispatcher model (dispatch : Dispatch<AllMsg>) =
    let dispatchSoundEvent event =
      let updateAudioState = AllMsg.Main <| MainMsg.UpdateAudioSource event
      dispatch updateAudioState
    let tracker = SoundTracker model.Config.MuteOnSoundSource
    tracker.eventSoundPlaying.Add(dispatchSoundEvent)
    tracker.eventSoundStopping.Add(dispatchSoundEvent)
    let fSourceDetect _ =
      tracker.updateState ()
    let setTracker = AllMsg.Main <| MainMsg.SetTracker tracker
    dispatch setTracker
    createTimer 1000. [| fSourceDetect |] |> startTimer
    ()
  


let dispatchers model _ =
  Cmd.batch [
    Cmd.ofSub (App.spotifyStateDispatcher model)
    Cmd.ofSub App.settingDispatcher
    Cmd.ofSub (App.soundSourceDetectDispatcher model)
    ] 


let mutexId = "Local\\0D993C99-AD24-4BBC-9D5E-66B8BA608119"

[<EntryPoint; STAThread>]
let main _ =
  
  //TODO::
  //logging
  use mutex = new Mutex(false, mutexId)
  if not (mutex.WaitOne(TimeSpan.Zero)) then
    failwith "Spotilay already running"
  
  let config = loadConfig<Config> defaultConfig
  let model, lst = App.init ()
  let initialModel = { model with Config = config } 
  let win = Spotilay.Views.Overlay()
  Program.mkProgramWpfWithCmdMsg (fun () -> initialModel, lst) App.update OverlayWindow.bindings App.toCmd
  |> Program.withSubscription (dispatchers initialModel)
  |> Program.runWindowWithConfig ElmConfig.Default win