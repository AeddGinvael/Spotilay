module SpotifyControls

open System
open System.Reflection
open WinApi

// IntPtr.Zero means spotify does not exist or not found
let getSpotifyHandle () =
    async {
      let! handle = DllExtern.getHandle ()
      if handle = IntPtr.Zero then
          return IntPtr.Zero
      else
          return handle        
    }


let intPtrToBool intPtr =
    if intPtr = IntPtr(0x1) then
        true
    else
        false

let tryPause hwnd =
    async {
//      do! Async.SwitchToThreadPool() 
//      let! handle = getSpotifyHandle ()
      return hwnd |> DllExtern.sendPlayPause |> intPtrToBool
    }
   
let tryNext hwnd =
    async {
//        do! Async.SwitchToThreadPool() 
//        let! handle = getSpotifyHandle ()
        return hwnd |> DllExtern.sendNextTrack |> intPtrToBool
    }


let tryPrev hwnd =
    async {
//        do! Async.SwitchToThreadPool() 
//        let! handle = getSpotifyHandle ()
        return hwnd |> DllExtern.sendPrevTrack |> intPtrToBool
    }
    
let tryVolumeUp () =
    async {
        do! Async.SwitchToThreadPool()
        let! handle = getSpotifyHandle()
        return handle |> DllExtern.sendVolumeUp |> intPtrToBool
    }
    
    
let tryVolumeDown () =
    async {
        do! Async.SwitchToThreadPool()
        let! handle = getSpotifyHandle()
        return handle |> DllExtern.sendVolumeDown |> intPtrToBool
    }


let getType obj =
    obj.GetType ()
    
let getMethods (objType: Type) =
    objType.GetMethods()
