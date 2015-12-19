// ----------------------------------------------------------------------------
// This file is subject to the terms and conditions defined in
// file 'LICENSE.txt', which is part of this source code package.
// ----------------------------------------------------------------------------
namespace Yaaf.Logging

open System
open System.Diagnostics
open System.IO


#if NO_PCL
type IDuplicateListener = 
    abstract member Duplicate : string -> TraceListener

module CopyListenerHelper = 
    let internal copyListener (fromListener:TraceListener) (toListener:TraceListener) = 
        toListener.Attributes.Clear()
        for pair in 
            fromListener.Attributes.Keys
                |> Seq.cast
                |> Seq.map2 (fun k v -> k,v) (fromListener.Attributes.Values |> Seq.cast) do
            toListener.Attributes.Add pair
        toListener.Filter <- fromListener.Filter
        toListener.IndentLevel <- fromListener.IndentLevel
        toListener.Name <- fromListener.Name
        toListener.TraceOutputOptions <- fromListener.TraceOutputOptions
        toListener
    let cleanName (name:string) = 
        let invalid = Path.GetInvalidFileNameChars() 
                        |> Seq.append (Path.GetInvalidPathChars())
        let cleanName = 
            (
            name 
                |> Seq.fold 
                    (fun (builder:Text.StringBuilder) char -> 
                        builder.Append(
                            if invalid |> Seq.exists (fun i -> i = char) 
                            then '_'
                            else char))
                    (new System.Text.StringBuilder(name.Length))
            ).ToString()
        cleanName
    let internal createNewFilename oldRelFilePath name =
        if System.String.IsNullOrEmpty(oldRelFilePath) then null
        else
            let fileName = Path.GetFileNameWithoutExtension(oldRelFilePath)
            let extension = Path.GetExtension(oldRelFilePath)
            Path.Combine(
                Path.GetDirectoryName(oldRelFilePath),
                sprintf "%s.%s%s" fileName name extension)
    let duplicateListener (source:string) (eventCache:TraceEventCache) (cleanName:string) (l:TraceListener) =
        match l:>obj with
        | :? IDuplicateListener as myListener ->
            myListener.Duplicate(cleanName)
        | _ ->
            l.TraceEvent(eventCache, source, TraceEventType.Error, 0, sprintf "Unknown Listener, can't apply name \"%s\"" cleanName)
            l


type DefaultTraceListener(initData:string, name:string) = 
    inherit System.Diagnostics.DefaultTraceListener()
    new() = new DefaultTraceListener(null,null)
    new(s) = new DefaultTraceListener(s,s)
    interface IDuplicateListener with
        member x.Duplicate name =
            CopyListenerHelper.copyListener
                x
                (new DefaultTraceListener() :> TraceListener)

type XmlWriterTraceListener(initData:string, name:string) = 
    inherit System.Diagnostics.XmlWriterTraceListener(initData)
    new(s) = new XmlWriterTraceListener(s,s)
    interface IDuplicateListener with
        member x.Duplicate name =
            let newPath = CopyListenerHelper.createNewFilename initData name
            CopyListenerHelper.copyListener 
                x
                (new XmlWriterTraceListener(newPath) :> TraceListener)
type TextWriterTraceListener(initData:string, name:string) = 
    inherit System.Diagnostics.TextWriterTraceListener(initData)
    new(s) = new TextWriterTraceListener(s,s)
    interface IDuplicateListener with
        member x.Duplicate name =
            let newPath = CopyListenerHelper.createNewFilename initData name
            CopyListenerHelper.copyListener 
                x
                (new TextWriterTraceListener(newPath) :> TraceListener)

type ConsoleTraceListener(initData:string, name:string) = 
    inherit System.Diagnostics.ConsoleTraceListener()
    new(s) = new ConsoleTraceListener(s,s)
    new() = new ConsoleTraceListener(null)
    interface IDuplicateListener with
        member x.Duplicate name =
            let newPath = CopyListenerHelper.createNewFilename initData name
            CopyListenerHelper.copyListener 
                x
                (new ConsoleTraceListener(newPath) :> TraceListener)
(* We have to override TraceListener and delegate everything
type EventLogTraceListener(initData:string, name:string) = 
    inherit System.Diagnostics.TraceListener(initData)
    new(s) = new EventLogTraceListener(s,s)
    interface IDuplicateListener with
        member x.Duplicate name =
            let newPath = CopyListenerHelper.createNewFilename initData name
            CopyListenerHelper.copyListener 
                x
                (new EventLogTraceListener(newPath) :> TraceListener)*)
    
/// To be able to use the given configuration but write in different files.
/// We want to write in different files per default because this class is designed for parallel environments.
/// For example when in the configuration "file.log" is given we log to "file.name.log".
type internal MyTraceSource(traceEntry:string,name:string) as x= 
    inherit TraceSource(traceEntry)
    do 
        // NOTE: Shared Listeners are not supported
        // (currently we create new ones and do not share them the same way)
        let flags = System.Reflection.BindingFlags.Public |||
                    System.Reflection.BindingFlags.NonPublic ||| 
                    System.Reflection.BindingFlags.Instance
                        
        let cleanName = CopyListenerHelper.cleanName name
        let eventCache = new TraceEventCache()
        let newTracers = [|
            for l in x.Listeners do
                yield CopyListenerHelper.duplicateListener x.Name eventCache cleanName l
            |]
        x.Listeners.Clear()
        x.Listeners.AddRange(newTracers)
#endif


/// Identifies the type of event that has caused the trace.
type TraceEventType = 
    /// Fatal error or application crash.
    | Critical = 1
    /// Recoverable error.
    | Error = 2
    ///Noncritical problem.
    | Warning = 4
    ///Informational message.
    | Information = 8
    ///Debugging trace.
    | Verbose = 16
    ///Resumption of a logical operation.
    | Resume = 2048
    ///Starting of a logical operation.
    | Start = 256
    ///Stopping of a logical operation.
    | Stop = 512
    ///Suspension of a logical operation.
    | Suspend = 1024 
    ///Changing of correlation identity.
    | Transfer = 4096

type SourceLevels =
    ///Allows the Stop, Start, Suspend, Transfer, and Resume events through.
    | ActivityTracing = 65280
    ///Allows all events through.
    | All = -1
    ///Allows only Critical events through.
    | Critical = 1
    ///Allows Critical and Error events through.
    | Error = 3
    ///Allows Critical, Error, Warning, and Information events through.
    | Information = 15
    ///Does not allow any events through.
    | Off = 0
    ///Allows Critical, Error, Warning, Information, and Verbose events through.
    | Verbose = 31
    ///Allows Critical, Error, and Warning events through.
    | Warning = 7
type IStackFrame =
    abstract member GetMethod : unit -> System.Reflection.MethodBase
    abstract member GetFileName : unit -> string
    abstract member GetFileLineNumber : unit -> int


type ITraceSource =

    abstract member TraceEvent : eventType:TraceEventType * id:int * format:string * [<ParamArray>] args:Object[] -> unit
    abstract member TraceEvent : eventType:TraceEventType * id:int * message:string -> unit
    abstract member TraceTransfer : id:int * message:string * relatedActivityId:Guid -> unit
    abstract member Flush : unit -> unit
    abstract member SwitchLevel : SourceLevels with get,set

    abstract member ClearListeners : unit -> unit

#if NO_PCL
type IExtendedTraceSource =
    inherit ITraceSource
    abstract member Wrapped : TraceSource
[<AutoOpen>]
module TraceSourceExtensions = 
    type ITraceSource with
        member x.Wrapped =
            match x with
            | :? IExtendedTraceSource as ext -> ext.Wrapped
            | _ -> failwith "no wrapped TraceSource found (possibly a native ITraceSource implementation!)"
#endif


type ILoggingBackend =
    abstract member CurrentActivityId : Guid with set, get
    abstract member SetLogicalData : string * obj -> unit
    abstract member GetLogicalData : string -> obj
    /// Creates a tracesource with the given name, for the given instance.
    abstract member CreateTraceSource : name:string * instance:string option -> ITraceSource

    abstract member CreateStackFrame : walk:int* t:bool-> IStackFrame


module internal Helper =
    let shouldLog (sourceLevels:SourceLevels) (tet:TraceEventType) = 
        (enum (int sourceLevels) &&& tet) <> enum 0

    let emptySource = 
        { new ITraceSource with
            member x.TraceEvent(eventType:TraceEventType, id:int, format:string, [<ParamArray>] args:Object[]) = ()
            member x.TraceEvent (eventType:TraceEventType, id:int, message:string) = ()
            member x.TraceTransfer(id:int, message:string, relatedActivityId:Guid) = ()
            member x.Flush () = ()
            member x.SwitchLevel 
                with get() =
                    SourceLevels.Off
                and set v = ()
            member x.ClearListeners () = ()
        }

#if NO_PCL
    let toTraceEventType (te:TraceEventType) = 
        enum (int te) : System.Diagnostics.TraceEventType
    let toSourceLevels (sl:SourceLevels) = 
        enum (int sl) : System.Diagnostics.SourceLevels
    let fromSourceLevels (sl:System.Diagnostics.SourceLevels) = 
        enum (int sl) : SourceLevels


    let fromTraceSource (ts: TraceSource) = 
        { new IExtendedTraceSource with
            member x.TraceEvent (eventType:TraceEventType, id:int, format:string, [<ParamArray>] args:Object[])=
                ts.TraceEvent(eventType |> toTraceEventType, id, format, args)
            member x.TraceEvent (eventType:TraceEventType, id:int, message:string) =
                ts.TraceEvent(eventType |> toTraceEventType, id, message)
            member x.TraceTransfer (id:int, message:string, relatedActivityId:Guid) =
                ts.TraceTransfer (id, message, relatedActivityId)
            member x.Flush () = ts.Flush()
            member x.SwitchLevel 
                with get () = ts.Switch.Level |> fromSourceLevels
                and set v = ts.Switch.Level <- v |> toSourceLevels
            member x.ClearListeners () = ts.Listeners.Clear()
            member x.Wrapped = ts
        } :> ITraceSource
        
    let fromStackFrame (sf: StackFrame) = 
        { new IStackFrame with
            member x.GetMethod () = sf.GetMethod()
            member x.GetFileName () = sf.GetFileName()
            member x.GetFileLineNumber () = sf.GetFileLineNumber()
        }
    let defaultBackend =
        { new ILoggingBackend with
            member x.CurrentActivityId 
                with get () = Trace.CorrelationManager.ActivityId 
                and set id = Trace.CorrelationManager.ActivityId <- id
            member x.GetLogicalData(name) =
                System.Runtime.Remoting.Messaging.CallContext.LogicalGetData(name)
            member x.SetLogicalData(name, o) =
                System.Runtime.Remoting.Messaging.CallContext.LogicalSetData(name, o)
            member x.CreateTraceSource (name, instance) = 
                match instance with
                | Some ins -> MyTraceSource(name, ins) :> TraceSource
                | None -> TraceSource(name)
                |> fromTraceSource
            member x.CreateStackFrame (walk:int, t:bool) =
                new StackFrame(walk + 1, t)
                |> fromStackFrame
        }

    let mutable currentBackend = defaultBackend
#else
    let myFail () = 
        failwith "please set the Logging backend with Log.SetBackend!"
    let mutable currentBackend = 
        { new ILoggingBackend with
            member x.CurrentActivityId 
                with get () = myFail()
                and set id = myFail()
            member x.GetLogicalData(name) = myFail()
            member x.SetLogicalData(name, o) = myFail()
            member x.CreateTraceSource (name, instance) = myFail()
            member x.CreateStackFrame (walk:int, t:bool) = myFail()
        }
#endif

    let isMono =
        System.Type.GetType ("Mono.Runtime") |> isNull |> not
        
    let doOnActivity activity f =
        let oldId = currentBackend.CurrentActivityId
        try
            currentBackend.CurrentActivityId <- activity
            f()
        finally
            currentBackend.CurrentActivityId <- oldId

open Helper
type ITracer = 
    inherit IDisposable
    abstract member TraceSource : ITraceSource
    abstract member ActivityId : Guid


[<AutoOpen>]
module LogInterfaceExtensions =
    let L = sprintf
    /// executes the given action (a log function) on the given ActivityId

    type ITracer with 
        member x.doInId f = 
            doOnActivity x.ActivityId f
        member private x.logHelper ty (o : string) =
            x.doInId 
                (fun () ->
                    x.TraceSource.TraceEvent(ty, 0, "{0}", o)
                    x.TraceSource.Flush())
        /// Logs a message with the given TraceEventType
        member x.log ty (fmt:unit -> string) =
            // In older version we did the shouldLog check only on mono, read comments below
            if shouldLog x.TraceSource.SwitchLevel ty then
                // Always do the above check, because we have to execute "fmt" anyway because of possible deadlocks
                let stringVal =
                    try
                        // NOTE: We Calculate the value outside of TraceEvent (logHelper)
                        // because TraceEvent uses locks, and because fmt() 
                        // executes user code it is possible to deadlock!
                        fmt ()
                    with
                    | exn -> 
                        // Logging should not fail (as it would also not fail when logging is removed completely)
                        // NOTE: the above reasoning holds as we try to remove side effects as best as we can
                        sprintf "<value generated exception | %A>" exn
                x.logHelper ty stringVal
        /// Logs a TraceEventType.Verbose message
        member x.logVerb fmt = x.log TraceEventType.Verbose fmt
        /// Logs a TraceEventType.Warning message
        member x.logWarn fmt = x.log TraceEventType.Warning fmt
        /// Logs a TraceEventType.Critical message
        member x.logCrit fmt = x.log TraceEventType.Critical fmt
        /// Logs a TraceEventType.Error message
        member x.logErr fmt =  x.log TraceEventType.Error fmt
        /// Logs a TraceEventType.Information message
        member x.logInfo fmt = x.log TraceEventType.Information fmt
    
  
    /// A simple ITracer implementation for a given tracesource
    let internal createDefaultStateTracer (traceSource:ITraceSource) activityName = 
        let activityId = Guid.NewGuid()
        doOnActivity activityId (fun () -> traceSource.TraceEvent(TraceEventType.Start, 0, activityName))
        { new ITracer with
            member x.TraceSource = traceSource
            member x.ActivityId = activityId
        
           interface IDisposable with
            member x.Dispose() = 
                doOnActivity activityId (fun () -> traceSource.TraceEvent(TraceEventType.Stop, 0, activityName)) }
    
    type ITracer with
        /// create a child tracer from the current instance
        /// we add a cross reference to the logfiles and return a new instance where we can start logging the activity
        member x.childTracer traceSource newActivity = 
            let tracer = createDefaultStateTracer traceSource newActivity
            x.doInId 
                (fun () -> 
                    x.TraceSource.TraceTransfer(0, "Switching to " + newActivity, tracer.ActivityId))
            tracer
 
/// Provides a simple layer above the .net logging facilities
module Log =
    let SetBackend log = Helper.currentBackend <- log
        
    let ForActivity source activityId =
        { new ITracer with
            member x.TraceSource = source
            member x.ActivityId = activityId
        
           interface IDisposable with
            member x.Dispose() = () }
    
    /// Provides a more advanced Tracesource which allows to share a configuration (given by traceEntry) for multiple tracesources (distinguished by the name)
    let MySource traceEntry name = Helper.currentBackend.CreateTraceSource (traceEntry, None) 
        // new MyTraceSource(traceEntry, name) :> TraceSource

    /// Provides classical TraceSource logging facilities
    let Source entryName = Helper.currentBackend.CreateTraceSource (entryName, None)
    
    /// Wraps a TraceSource and provides a more F# friendly interface
    let DefaultTracer traceSource id = 
        createDefaultStateTracer traceSource id
        
    /// A default empty tracer.
    let EmptyTracer = 
        { new ITracer with
            member x.Dispose() = ()
            member x.TraceSource = Helper.emptySource
            member x.ActivityId = Guid.Empty }

#if NO_PCL
    /// Returns a Console-Logger for debugging purposes
    let ConsoleLogger level =
        new ConsoleTraceListener(
            Filter = new EventTypeFilter(level), 
            TraceOutputOptions = TraceOptions.DateTime)
    /// Sets the source to print into the given logger
    let SetSource logger level (* level for mono... *) (source:TraceSource) =
        source.Listeners.Clear()
        source.Switch.Level <- level
        source.Listeners.Add logger |> ignore
#endif

    let mutable internal globalUnhandledSource = lazy Source "Yaaf.Logging"

    /// <summary>
    /// Sets the traceSource for all unhandled namespaces.
    /// </summary>
    let SetUnhandledSource source = globalUnhandledSource <- lazy source

    /// <summary>
    /// Gets the traceSource for all unhandled namespaces.
    /// </summary>
    let GetUnhandledSource () = globalUnhandledSource.Value

            
            
            