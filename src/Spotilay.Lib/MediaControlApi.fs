namespace Spotilay.Lib

open Windows.Foundation
open Windows.Media.Control
open System

module MediaControlApi =
    
    type ControlSession = GlobalSystemMediaTransportControlsSession
    type MediaProps = GlobalSystemMediaTransportControlsSessionMediaProperties
    type TimelineProps = GlobalSystemMediaTransportControlsSessionTimelineProperties
    type PlaybackProps = GlobalSystemMediaTransportControlsSessionPlaybackInfo
    type PlaybackStatus = GlobalSystemMediaTransportControlsSessionPlaybackStatus
    
    type TrackInfo = {
        Artist : string
        Name   : string
    }
    
    type TimelineInfo = {
        Position  : TimeSpan
        Start     : TimeSpan
        End       : TimeSpan
        MaxSeek   : TimeSpan
        UpdatedAt : DateTimeOffset
    }
    
    type PlaybackInfo = {
        IsPlaying : bool
    }
    
    let mapTimelineInfo (prop: TimelineProps) =
        { Position = prop.Position; Start = prop.StartTime; End = prop.EndTime; MaxSeek = prop.MaxSeekTime; UpdatedAt = prop.LastUpdatedTime }
    let mapTrackInfo (prop: MediaProps) =
        { Artist = prop.Artist; Name = prop.Title }
        
    let mapPlaybackInfo (prop: PlaybackProps) =
        { IsPlaying = prop.PlaybackStatus = PlaybackStatus.Playing}
    
    let tryComplete<'a> (op: IAsyncOperation<'a>) : 'a =
        op.AsTask() |> Async.AwaitTask |> Async.RunSynchronously
        
        
        
    let createMediaSession name =
        let sessionManager = GlobalSystemMediaTransportControlsSessionManager.RequestAsync() |> tryComplete
        let session = sessionManager.GetSessions() |> Seq.tryFind (fun x -> x.SourceAppUserModelId = name)
        match session with
        | Some session -> Some session
        | None -> None
    

    
    type MediaSession (name: String) =
        
        let mutable controlSession: ControlSession option = None
        let mediaPropsChanged = Event<_> ()
        let mediaTimelineChanged = Event<_> ()
        let mediaPlaybackChanged = Event<_> ()
        let triggerMediaPropsChanged =
            let mutable prevProps = Unchecked.defaultof<TrackInfo>
            
            let inner (ses: ControlSession) = 
                let props = ses.TryGetMediaPropertiesAsync() |> tryComplete |> mapTrackInfo
                if props <> prevProps then
                    prevProps <- props
                    mediaPropsChanged.Trigger props
            inner
        
        let triggerTimelineChanged (ses: ControlSession) =
                let props = ses.GetTimelineProperties() |> mapTimelineInfo
                mediaTimelineChanged.Trigger props
                
        let triggerPlaybackChanged (ses: ControlSession) =
                let props = ses.GetPlaybackInfo() |> mapPlaybackInfo
                mediaPlaybackChanged.Trigger props
                
        let propsChanged () =
            let handler (ses: ControlSession) _ =
                triggerMediaPropsChanged ses
            TypedEventHandler<GlobalSystemMediaTransportControlsSession, MediaPropertiesChangedEventArgs>(handler)
            
        let timelineChanged () =
            let handler (ses: ControlSession) _ =
                triggerTimelineChanged ses
            TypedEventHandler<GlobalSystemMediaTransportControlsSession, TimelinePropertiesChangedEventArgs>(handler)
            
        let playbackChanged () =
            let handler (ses: ControlSession) _ =
                triggerPlaybackChanged ses
                
            TypedEventHandler<GlobalSystemMediaTransportControlsSession, PlaybackInfoChangedEventArgs>(handler)

        let unsubscribe (ses: ControlSession) =
             ses.remove_TimelinePropertiesChanged <| timelineChanged ()
             ses.remove_MediaPropertiesChanged <| propsChanged ()
             ses.remove_PlaybackInfoChanged <| playbackChanged ()
             controlSession <- None
             
        let subscribe (ses: ControlSession) =
            ses.add_MediaPropertiesChanged <| propsChanged ()
            ses.add_TimelinePropertiesChanged <| timelineChanged ()
            ses.add_PlaybackInfoChanged <| playbackChanged ()
            controlSession <- Some ses
            triggerMediaPropsChanged ses
            triggerTimelineChanged ses
            triggerPlaybackChanged ses


        [<CLIEvent>] member this.eventMediaProps = mediaPropsChanged.Publish
        [<CLIEvent>] member this.eventMediaTimeline = mediaTimelineChanged.Publish
        [<CLIEvent>] member this.eventPlayback = mediaPlaybackChanged.Publish
        
        
        member this.updateState() =
            let session = createMediaSession name
            match session, controlSession with
            | None, Some s -> unsubscribe s
            | Some s, None -> subscribe s
            | _ -> ()
        