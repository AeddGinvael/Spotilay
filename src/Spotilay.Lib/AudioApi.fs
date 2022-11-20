module AudioApi

    open System.Collections.Generic
    open NAudio.CoreAudioApi
    open System
    open System.Linq
    
    [<Measure>] type volume
    
    [<Struct>]
    type SoundSource = {
        Identifier      : string
        VolumePeakValue : float<volume>
        RawVolume       : float
        Pid             : uint
        StartedTime     : DateTime
    }
    // DateTime is UtcNow
    [<Struct>]
    type SoundEvent =
    | Sound of sound: (string * DateTime) 
    | Silence of silence: DateTime
    
    let getDefaultDevice () =
        use enumerator = new MMDeviceEnumerator()
        enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia)
        
    let getRawSessions (deviceEnum: MMDevice) =
        use device = deviceEnum
        let audioManager = device.AudioSessionManager
        let mutable sessions = []
        for i in 0..audioManager.Sessions.Count - 1 do
            let ses = audioManager.Sessions.[i]
            sessions <- ses :: sessions 
        sessions
        
    let isFit (ctlr : AudioSessionControl) = ctlr.IsSystemSoundsSession <> true && ctlr.GetProcessID <> 0u

    let parseIdentifier (str: string) =
        if String.IsNullOrEmpty(str) then
            ""
        else
        let splited = str.Split("%")
        splited.[0].Split("\\") |> Array.last
    
    let mapSession (audioCtrl : AudioSessionControl) =
            let id = audioCtrl.GetSessionIdentifier |> parseIdentifier;
            let scaled = audioCtrl.AudioMeterInformation.MasterPeakValue * 100.0f |> float
            let session = {
                Identifier = id
                VolumePeakValue = Math.Round(scaled, 2) * 1.<volume>
                RawVolume = audioCtrl.AudioMeterInformation.MasterPeakValue |> float
                Pid = audioCtrl.GetProcessID
                StartedTime = DateTime.UtcNow
            }
            session

    
    let filterSessions lst = lst |> List.filter (fun x -> x.RawVolume > 0.000001)
    
    type SoundTracker () =
        
        let mutable sessionsMap = Dictionary<string, List<SoundSource>>()
        let onSoundPlaying = Event<_> ()
        let onSoundStopping = Event<_> ()
        
        [<CLIEvent>] member this.eventSoundPlaying = onSoundPlaying.Publish
        [<CLIEvent>] member this.eventSoundStopping = onSoundStopping.Publish

        member private this.getSessions () =
            let ses = getRawSessions (getDefaultDevice())
            ses
            |> List.filter isFit
            |> List.map mapSession
            |> List.filter (fun x -> x.Identifier <> "Spotify.exe")

        member private this.updateSession (key, lst: SoundSource list) =
            match sessionsMap.TryGetValue key with
            | true, data -> data.AddRange(lst.ToArray()) 
            | false, _ -> sessionsMap.Add (key, lst.ToList())

        member private this.update () =
            let sessions = this.getSessions ()
            let groupBySource = sessions |> List.groupBy (fun x -> x.Identifier)
            groupBySource |> List.iter this.updateSession
            
        member private this.isThereActiveSession () =
            let isActiveSession (lst: List<SoundSource>) =
                match lst.Count with
                | 0 -> false
                | _ -> lst.Last().VolumePeakValue > 0.0<volume>
            sessionsMap
            |> Seq.tryFind (fun i -> isActiveSession i.Value)
            
        member this.runDispatcher () =
            this.update()
            let flag = this.isThereActiveSession()
            match flag with
            | Some f ->
                sessionsMap.Clear()
                onSoundPlaying.Trigger(SoundEvent.Sound (f.Key, DateTime.UtcNow))
            | _ -> onSoundStopping.Trigger(SoundEvent.Silence DateTime.UtcNow)
            
