module Spotilay

open System
open System.Threading
open System.Windows
open AudioApi
open Elmish
open Elmish.WPF
open Common
open SpotifyControls
open WinApi

[<AutoOpen>]
module Types =
  type OverlayMsg =
  | Nothing
  | Stop
  | NextTrack
  | PrevTrack
  | SetProcTrackPlaying of bool
  | SetTrackName of string
  | SetTrack
  | SetTrackPlaying
  | SetSpotifyHandle of IntPtr
  | CallSpotifyGetHandle 
  | Fail of exn 
 
  type MainMsg =
  | ShowOverlay
  | HideOverlay
  | CloseOverlay
  | ExitApp
  | ClosedOption of bool
  | SetTracker of SoundTracker
  | SettingsDispatch
  | UpdateAudioSource of SoundEvent
  | SetMuteOnAudioSource of bool
  | Fail of exn
  | Next
   
  [<Struct>]
  type OverlayModel =  {
      IsTrackPlaying  : bool
      CurrTrackName   : string
  }
  
  [<Struct>]
  type Settings = {
      SpotifyClosedOption : bool
      IsSpotifyMuted      : bool
  }
  
  [<Struct>]
  type Model =
    {
      OverlayWindow : WindowState<string>
      OverlayState  : OverlayModel
      AppSettings   : Settings
      SpotifyHandle : IntPtr
      Tracker       : SoundTracker option
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
      | SetTrackCmd of Model
      | SetTrackPlayingCmd of Model
      | SpotifyHandleCmd of Model
  let init () =
      {
        IsTrackPlaying = false
        CurrTrackName = "Unknown Track"
      }
  
  let update (msg: OverlayMsg) (m: Model) =
    match msg with
    | OverlayMsg.Nothing -> m, []
    | Stop -> m, [PauseCmd m]
    | PrevTrack -> m, [PrevCmd m]
    | NextTrack -> m, [NextCmd m]
    | SetProcTrackPlaying flag -> { m with OverlayState = { m.OverlayState with IsTrackPlaying = flag } }, []
    | SetTrackName name -> { m with OverlayState = { m.OverlayState with CurrTrackName = name} }, []
    | SetTrack -> m, [SetTrackCmd m]
    | SetSpotifyHandle hwnd -> { m with SpotifyHandle = hwnd }, []
    | CallSpotifyGetHandle -> m, [SpotifyHandleCmd m]
    | OverlayMsg.SetTrackPlaying -> m, [SetTrackPlayingCmd m]
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
    "StopBtnKind" |> Binding.oneWay (fun m -> bindStopBtn m)
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
    
  let setTrack (model: Model) =
    Application.Current.Dispatcher.Invoke(fun () ->
      let ctx = SynchronizationContext.Current
      async {
        do! Async.SwitchToContext ctx
        if model.OverlayState.IsTrackPlaying then
          let! name = getCurrentTrackNameFromNative model.SpotifyHandle
          let setTrackName = AllMsg.Overlay <| OverlayMsg.SetTrackName name
          return setTrackName
        else
        return AllMsg.Nothing
      }
    )

  let setTrackPlaying (model: Model) =
    Application.Current.Dispatcher.Invoke(fun () ->
      let ctx = SynchronizationContext.Current
      async {
        do! Async.SwitchToContext ctx
        let! isPlaying = isTrackPlaying model.SpotifyHandle
        let setProcTrack = AllMsg.Overlay <| OverlayMsg.SetProcTrackPlaying isPlaying
        return setProcTrack
      }
    )
    
  let muteSpotify (model: Model) status = 
      let playing = model.OverlayState.IsTrackPlaying
      let muted = model.AppSettings.IsSpotifyMuted
      
      match status with
      | true ->
         match (playing, muted) with
         | (true, false) -> async {
             let! _ = tryPause model.SpotifyHandle
             return AllMsg.Main <| MainMsg.SetMuteOnAudioSource true
           }
         | (false, true) -> async {
             let! _ = tryPause model.SpotifyHandle
             return AllMsg.Main <| MainMsg.SetMuteOnAudioSource false
           }
         | (false, false) -> async {return AllMsg.Nothing}
         | (_, true) -> async {return AllMsg.Nothing}
      | false ->
        match (playing, muted) with
        | (false, true) -> async {
            let! _ = tryPause model.SpotifyHandle
            return AllMsg.Main <| MainMsg.SetMuteOnAudioSource false         
          }
        | (false, false) -> async {return AllMsg.Nothing}
        | (_, true) -> async {return AllMsg.Nothing}
        | (true, false) -> async {return AllMsg.Nothing}


  let audioSource (state: (Model * SoundEvent)) =
    Application.Current.Dispatcher.Invoke(fun () ->
      let ctx = SynchronizationContext.Current
      async {
       let (model, event) = state
       printfn "%O" event
       return! match event with
               | Playing e -> muteSpotify model true
               | Stopping e -> muteSpotify model false
       }
      )

  let settingsDispatch (model: Model) =
    async {
      let isSpotifyRunnin = isSpotifyRunning()
      let modelClosedOption =
        match (isSpotifyRunnin, model.AppSettings.SpotifyClosedOption) with
        | (true, true) ->  { model with OverlayWindow = WindowState.Visible "" }
        | (false, true) -> { model with OverlayWindow = WindowState.Hidden "" }
        | (false, false) -> { model with OverlayWindow = WindowState.Visible "" }
        | (_, _) -> model
        
      let update = AllMsg.Update modelClosedOption
      return update
    }
   
  let spotifyHandleHwnd (model: Model) =
    Application.Current.Dispatcher.Invoke(fun () ->
      let ctx = SynchronizationContext.Current
      async {
        do! Async.SwitchToContext ctx
        let! handle = DllExtern.getHandle ()
        if handle <> model.SpotifyHandle then
          return AllMsg.Overlay <| OverlayMsg.SetSpotifyHandle handle
        else
          return AllMsg.Nothing
      }
    )

module App =
  open OverlayWindow

  let init () =
    { OverlayWindow = WindowState.Closed
      OverlayState = init()
      SpotifyHandle = IntPtr.Zero
      AppSettings = {
        SpotifyClosedOption = false
        IsSpotifyMuted = false
      }
      Tracker = None
       },
    []
        
  let updateMain msg m =
    match msg with
    | Next -> m, []
    | ShowOverlay -> { m with OverlayWindow = WindowState.Visible "" }, []
    | HideOverlay -> { m with OverlayWindow = WindowState.Hidden "" }, []
    | CloseOverlay -> { m with OverlayWindow = WindowState.Closed }, []
    | ClosedOption b -> { m with AppSettings =
                                 { m.AppSettings with SpotifyClosedOption = b}}, []
    | ExitApp ->
      Application.Current.Shutdown()
      m, []
    | SettingsDispatch -> m, [ OverlayCmd.SettingsDispatchCmd m]
    | SetTracker s -> { m with Tracker = Some s }, []
    | MainMsg.UpdateAudioSource soundEvent -> m, [ OverlayCmd.UpdateAudioSource (m, soundEvent)]
    | MainMsg.SetMuteOnAudioSource flag -> { m with AppSettings =
                                                     { m.AppSettings with IsSpotifyMuted = flag }}, []
    | MainMsg.Fail exn -> m, []
  
  let update (msg: AllMsg) m =
    match msg with
    | AllMsg.Overlay msg' -> update msg' m
    | AllMsg.Main msg' -> updateMain msg' m
    | AllMsg.Nothing -> m, []
    | AllMsg.Fail exn -> m, []
    | AllMsg.Update model -> model, []
  
  let closedMapf = fun a -> (AllMsg.Main (MainMsg.ClosedOption a))
  let mainBindings (createOverlay: unit -> #Window) () : Binding<Model, AllMsg> list = [
    "ShowOverlay"  |> Binding.cmd (AllMsg.Main MainMsg.ShowOverlay)
    "HideOverlay"  |> Binding.cmd (AllMsg.Main MainMsg.HideOverlay)
    "CloseOverlay" |> Binding.cmd (AllMsg.Main MainMsg.CloseOverlay)
    "Exit"         |> Binding.cmd (AllMsg.Main MainMsg.ExitApp)
    "Overlay"      |> Binding.subModelWin( (fun m -> m.OverlayWindow), fst, id, bindings, createOverlay)
    
    "SetOverlayHideOnClosedSpotify" |> Binding.twoWay ( (fun m -> m.AppSettings.SpotifyClosedOption), closedMapf)
  ]
  

  let exit () =
    Application.Current.Shutdown()
    
  let asFunc f = Cmd.OfFunc.either f () id AllMsg.Fail
  let asAsync f a = Cmd.OfAsync.either f a id AllMsg.Fail

  let toCmd = function
  | OverlayCmd.NextCmd             model -> asAsync next model 
  | OverlayCmd.PrevCmd             model -> asAsync prev model
  | OverlayCmd.PauseCmd            model -> asAsync pause model
  | OverlayCmd.SettingsDispatchCmd model -> asAsync settingsDispatch model
  | OverlayCmd.UpdateAudioSource   event -> Cmd.OfAsync.either audioSource event id AllMsg.Fail
  | OverlayCmd.SetTrackCmd         model -> asAsync setTrack model
  | OverlayCmd.SetTrackPlayingCmd  model -> asAsync setTrackPlaying model
  | OverlayCmd.SpotifyHandleCmd    model -> asAsync spotifyHandleHwnd model
  
  
  let settingDispatcher (dispatch : Dispatch<AllMsg>) =
    let f _ = dispatch (AllMsg.Main SettingsDispatch)
    createTimer 1000. [| f |] |> startTimer
  
  let spotifyStateDispatcher (dispatch : Dispatch<AllMsg>) =

    let fProcState _ =
        let setProcTrack = AllMsg.Overlay OverlayMsg.SetTrackPlaying
        dispatch setProcTrack
    let fTrack _ =
        let setTrack = AllMsg.Overlay OverlayMsg.SetTrack
        dispatch setTrack
      
    let call = AllMsg.Overlay OverlayMsg.CallSpotifyGetHandle
    dispatch call
    ProcessEvents.watchEventProcess (fun () -> dispatch (AllMsg.Overlay OverlayMsg.CallSpotifyGetHandle))
    let showOverlay = AllMsg.Main MainMsg.ShowOverlay
    dispatch showOverlay
    createTimer 1000. [| fProcState; fTrack;  |] |> startTimer
    
  

  let soundSourceDetectDispatcher (dispatch : Dispatch<AllMsg>) =
    let dispatchSoundEvent event =
      let updateAudioState = AllMsg.Main <| MainMsg.UpdateAudioSource event
      dispatch updateAudioState
      
    let tracker = SoundTracker ()
    tracker.eventSoundPlaying.Add(fun e -> dispatchSoundEvent e)
    tracker.eventSoundStopping.Add(fun e -> dispatchSoundEvent e)
    let fSourceDetect _ =
      tracker.runDispatcher ()
    let setTracker = AllMsg.Main <| MainMsg.SetTracker tracker
    dispatch setTracker
    createTimer 1000. [| fSourceDetect |] |> startTimer
  
let createOverlayWindow () =
  Spotilay.Views.Overlay(Owner = Application.Current.MainWindow)

let binding = App.mainBindings createOverlayWindow ()
let mainDesignVm = ViewModel.designInstance (App.init () |> fst) binding
let overlayDesignVm = ViewModel.designInstance (App.init () |> fst) (OverlayWindow.bindings ())

let dispatchers _ =
  Cmd.batch [
    Cmd.ofSub App.spotifyStateDispatcher
    Cmd.ofSub App.settingDispatcher
    Cmd.ofSub App.soundSourceDetectDispatcher
    ] 


// Same as Program.runWindowWithConfig but not showing window
let runApp config window program =
  Application () |> ignore
  Application.Current.MainWindow <- window
  Program.startElmishLoop config window program
  Application.Current.Run window

[<EntryPoint; STAThread>]
let main _ =
  let win = Spotilay.Views.MainWindow()
  let bindings = App.mainBindings createOverlayWindow
  Program.mkProgramWpfWithCmdMsg App.init App.update bindings App.toCmd
  |> Program.withSubscription dispatchers
  |> Program.withDebugTrace
  |> Program.runWindowWithConfig ElmConfig.Default win