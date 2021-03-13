module AudioApi

    open System.Collections.Generic
    open NAudio.CoreAudioApi
    open System
    
    [<Measure>] type seconds
    [<Measure>] type volume
    
    [<Struct>]
    type SoundSource = {
        Identifier      : string
        VolumePeakValue : float<volume>
        RawVolume       : float
        Pid             : uint
        StartedTime     : DateTime
    }
    
    [<Struct>]
    type Period = {
        Started : DateTime
        Ended   : DateTime
        Total   : float<seconds>
        Average : float<volume> // avg sound volume
    }
    
    [<Struct>]
    type PeriodType =
    | Sound of sound: Period
    | Silent of silence: Period
    | Empty
    
    // DateTime is UtcNow
    [<Struct>]
    type SoundEvent =
    | Playing of play: (string * DateTime) 
    | Stopping of stop: (string * DateTime)
    
    let private getDefaultDevice () =
        use enumerator = new MMDeviceEnumerator()
        enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia)
        
    let private getRawSessions (deviceEnum: MMDevice) =
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

    let filterSessions lst = lst |> List.filter (fun x -> x.RawVolume > 0.000001)
    
    
    let sliceManyTill predicate lst =
        let rec aux acc state l =
            match l with
            | [] when acc = [] -> [lst]
            | [] -> state :: acc
            | fst::rest when predicate fst -> aux (state :: acc) [] rest
            | fst::rest -> aux acc (fst :: state) rest
        aux [] [] lst |> List.filter (fun x -> x <> []) |> List.map List.rev |> List.rev
        
    let sliceInclude predicate lst =
        let rec aux acc state l =
            match l with
            | [] when acc = [] -> [lst]
            | [] -> state :: acc
            | fst::rest when predicate fst ->
                let list = fst :: rest |> List.takeWhile predicate
                let skip = rest |> List.skipWhile predicate
                aux ([list] @ (state :: acc)) [] skip
            | fst::rest -> aux acc (state @ [fst]) rest
        aux [] [] lst |> List.filter (fun x -> x <> []) |> List.rev
        
        
    type SoundTracker () =
        
        let mutable sessionsMap = Map.empty
        let mutable playingSource = List<string * bool> ()
        
        let onSoundPlaying = Event<_> ()
        let onSoundStopping = Event<_> ()
        
        [<CLIEvent>] member this.eventSoundPlaying = onSoundPlaying.Publish
        [<CLIEvent>] member this.eventSoundStopping = onSoundStopping.Publish
        
        member private this.updateMap (session: SoundSource) =
            match sessionsMap.ContainsKey(session.Identifier) with
            | true ->
                let lst = sessionsMap.[session.Identifier] @ [session]
                sessionsMap <- sessionsMap |> Map.add session.Identifier lst
                
            | false ->
                sessionsMap <- sessionsMap |> Map.add session.Identifier [session]
                
        member private this.clearKey key =
            sessionsMap <- sessionsMap |> Map.remove key
                
        member private this.createActiveSes (audioCtrl : AudioSessionControl) =
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
            
        member private this.toPeriod (lst: SoundSource list) =
            if lst.IsEmpty then
                Empty
            else
            
            let createPeriod soundSrcLst =
                let head = soundSrcLst |> List.minBy (fun x -> x.StartedTime)
                let tail = soundSrcLst |> List.maxBy (fun x -> x.StartedTime)
                let diff = tail.StartedTime - head.StartedTime
                let total = (diff.TotalSeconds |> Math.Abs) * 1.0<seconds>
                { Started = head.StartedTime
                  Ended = tail.StartedTime
                  Total = total 
                  Average = soundSrcLst |> List.averageBy (fun x -> x.RawVolume * 1.<volume>) }
                
            let isSilent = lst |> List.forall (fun x -> x.RawVolume = 0.0)
            let period = createPeriod lst
            match isSilent with
            | true -> period |> Silent
            | false -> period |> Sound
            

        member private this.getRawSessions () =
            use device = getDefaultDevice ()
            let audioManager = device.AudioSessionManager
            let mutable sessions = []
            for i in 0..audioManager.Sessions.Count - 1 do
                let ses = audioManager.Sessions.[i]
                sessions <- [ses] |> List.append sessions 
            sessions
        member private this.getPeriodMap sessionsMap =
            let mapSes (key: string, lst: SoundSource list) =
                let period = lst |> sliceInclude (fun x -> x.VolumePeakValue < 0.01<volume>)
                let temp = period |> List.map this.toPeriod          
                (key, temp)
            
            sessionsMap
            |> Map.toList
            |> List.map mapSes
            |> Map.ofList
            

        member private this.getSessions () =
            let ses = getRawSessions (getDefaultDevice())
            ses
            |> List.filter isFit
            |> List.map this.createActiveSes
            |> List.map this.updateMap

        member this.getPlaying periodMap =
            let mapper (key, periods: PeriodType list) =
 
                let matchPeriod p =
                    match p with
                    | Sound period -> true
                    | Silent period -> false
                    | Empty -> false
                    
                let isPlaying lst = 
                    match lst with
                    | [] -> false
                    | lst -> matchPeriod (lst |> List.last)

                (key, isPlaying periods, periods |> List.last)
            periodMap |> Map.toList |> List.map mapper
        
        member this.ApplySettings (periods: PeriodType list) =
            let defaultSilentDelay = 1.<seconds>
            let apply p =
                match p with
                | Sound _ -> true
                | Silent s ->
                    s.Total >= defaultSilentDelay
                | Empty -> false

            match periods with
            | [] -> []
            | lst when lst.Length = 1 -> lst
            | lst -> lst |> List.filter apply
            
//            periods |> List.filter apply
        
        member private this.update () =
            let _ = this.getSessions ()
            let periodMap = this.getPeriodMap sessionsMap
                            |> Map.filter (fun _ periods -> periods <> [])
                            |> Map.remove "Spotify.exe"
                            |> Map.map (fun _ periods -> periods |> this.ApplySettings)
            let playing = this.getPlaying periodMap
            playing
            
        member this.runDispatcher () =
            let playing = this.update ()
            let playingIter (key, flag, period) =
                match flag with
                | true ->
                    match playingSource.FindIndex((fun (x, _) -> x = key)) with
                    | -1 ->
                        playingSource.Add((key, flag))
                        let event = SoundEvent.Playing (key, DateTime.UtcNow)
                        onSoundPlaying.Trigger(event)
                    | _ -> ()
                    
                | false ->
                    match playingSource.FindIndex((fun (x, _) -> x = key)) with
                    | -1 -> ()
                    | idx ->
                        this.clearKey key
                        let event = SoundEvent.Stopping (key, DateTime.UtcNow)
                        onSoundStopping.Trigger(event)
                        playingSource.RemoveAll((fun (x, _) -> x = key)) |> ignore
                    
            playing |> List.iter playingIter
            ()
