// ----------------------------------------------------------------------------
// This file is subject to the terms and conditions defined in
// file 'LICENSE.txt', which is part of this source code package.
// ----------------------------------------------------------------------------
namespace Yaaf.Logging

#if NO_PCL
open System
open System.Diagnostics
open System.IO
open System.Xml
open System.Xml.XPath
open System.Threading

module XmlWriterHelper =
    let backupSource = lazy Log.GetUnhandledSource()
    let backupTracer = lazy Log.DefaultTracer backupSource.Value "backup"
    type Context = {
        Writer : XmlWriter
        Namespace : string }
    type Writer = Context -> unit
    let combine f1 f2 = (fun context ->
        f1 context
        f2 context)
    let Empty = ignore
    let Elem name f = (fun context ->
        let w = context.Writer
        w.WriteStartElement(name, context.Namespace)
        f context
        w.WriteEndElement()
        )
    let String value = (fun context ->
        let w = context.Writer
        w.WriteString(value)
        )
        
    let rec And (l:Writer list) = 
        match l with
        | [] -> ignore
        | x::xs -> combine x (And xs)
        
    let Attribute name value = (fun context ->
        let w = context.Writer
        w.WriteAttributeString (name, value);
        )
    let Namespace ns f = (fun context ->
        f { context with Namespace = ns })
       
    let Raw text = 
        (fun context -> context.Writer.WriteRaw text) 
        
open XmlWriterHelper
type TracingData = 
    | Message of string
    | TraceData of obj array
type SimpleXmlWriterTraceListener(initData:string, w: XmlWriter, name:string) as x = 
    inherit System.Diagnostics.TextWriterTraceListener()
    
    static let defaultName = "SimpleXmlWriter"
    static let defaultInitData = "_rawStream"

    static let xmlwritersettings = 
        new XmlWriterSettings( 
            CloseOutput = true,
            OmitXmlDeclaration = true, 
            ConformanceLevel = ConformanceLevel.Auto)
    
    static let createWriter (stream:StreamWriter) = XmlWriter.Create (stream, xmlwritersettings)
    static let createFileStream (file:String) = 
        new FileStream (file, FileMode.Append, FileAccess.Write, FileShare.ReadWrite)
    static let createStreamWriter (stream:Stream) = 
        new StreamWriter (stream)
    
    static let createStream initData = 
        match initData with
        | _ when initData = defaultInitData -> failwith "internal use only!"
        | _ ->
            createFileStream initData
            
    static let lockObjects = new System.Collections.Concurrent.ConcurrentDictionary<obj,obj>()
    static let currentProcess = Process.GetCurrentProcess()
    let getLockKey () = 
        match initData with
        | _ when initData = defaultInitData -> w :> obj
        | _ ->
            initData :> obj
    let getLockObj() = 
           lockObjects.GetOrAdd(getLockKey(), fun key -> new obj())
    let nsE2E = "http://schemas.microsoft.com/2004/06/E2ETraceEvent"
    let nsSystem = "http://schemas.microsoft.com/2004/06/windows/eventlog/system"
    let xpathNavToString (nav:XPathNavigator) = 
        let sw = new StringWriter()
        use xw = XmlWriter.Create(sw, xmlwritersettings)
        nav.WriteSubtree(xw)
        sw.ToString()
        
    let traceCore (eventCache:TraceEventCache) (source:string) (eventType:TraceEventType)
        (id:int) (relatedActivity:Guid option) (wrapData:bool) (data:TracingData) =
        let p = 
            (*if eventCache <> null 
            then Process.GetProcessById(eventCache.ProcessId)
            else*) currentProcess
        
        let date = 
            XmlConvert.ToString (((*if eventCache <> null then eventCache.DateTime else*) DateTime.Now), XmlDateTimeSerializationMode.Unspecified)
        
        let level =
            let num = int eventType
            if (num > int System.Byte.MaxValue) then
                System.Byte.MaxValue
            elif (num < 0) then
                0uy
            else byte num
        let xmlWriter = 
            Elem "E2ETraceEvent" 
                (And [
                    Namespace nsSystem (Elem "System" 
                        (And [
                            Elem "EventID" (String (XmlConvert.ToString(id)))
                            Elem "Type" (String "3")
                            Elem "SubType" 
                                (And [
                                    Attribute "Name" (eventType.ToString())
                                    String "0"
                                ])
                            Elem "Level" (String (level.ToString()))
                            Elem "TimeCreated" (Attribute "SystemTime" date)
                            Elem "Source" (Attribute "Name" source)
                            Elem "Correlation" 
                                (And [
                                    yield Attribute "ActivityID" (sprintf "{%O}" Trace.CorrelationManager.ActivityId)
                                    match relatedActivity with
                                    | Some id -> 
                                        yield Attribute "RelatedActivityID" (sprintf "{%O}" id)
                                    | None -> ()
                                ])
                            Elem "Execution" 
                                (And [
                                    Attribute "ProcessName" p.MainModule.ModuleName
                                    Attribute "ProcessID" (p.Id.ToString())
                                    Attribute "ThreadID" (Thread.CurrentThread.ManagedThreadId.ToString ())                        
                                ])
                            Elem "Channel" Empty
                            Elem "Computer" (String System.Environment.MachineName)
                        ]))
                    Elem "ApplicationData" 
                        (match data with
                         | Message s ->
                           String s
                         | TraceData data ->
                           Elem "TraceData"
                            (And [
                                 for o in data do
                                    let rawWriter =
                                        match o with
                                        | :? XPathNavigator as nav -> 
                                            // the output ignores xmlns difference between the parent (E2ETraceEvent and the content node).
                                            // To clone such behavior, I took this approach.
                                            Raw (xpathNavToString nav)
                                        | _ -> String (o.ToString())
                                    yield
                                        if wrapData
                                        then Elem "DataItem" rawWriter
                                        else rawWriter
                            ]))
                ])
        
        // write it
        try
            lock (getLockObj()) (fun () ->
                xmlWriter { Namespace = nsE2E; Writer = w }
                w.Flush()
                x.Flush())
        with
        | exn ->
            backupTracer.Value.logErr(fun () -> L "Couldn't log file: %O" exn)
        ()

    new(w:XmlWriter, name:string) = 
        new SimpleXmlWriterTraceListener(defaultInitData, w, name)
    new(w:XmlWriter) = 
        new SimpleXmlWriterTraceListener(w, defaultName)
    new(stream:System.IO.StreamWriter, name:string) = 
        new SimpleXmlWriterTraceListener(defaultInitData, createWriter stream, name)
    new(stream:System.IO.StreamWriter) = 
        new SimpleXmlWriterTraceListener(stream, defaultName)
    new(stream:System.IO.Stream, name: string) = 
        new SimpleXmlWriterTraceListener(createStreamWriter stream, name)
    new(stream:System.IO.Stream) = 
        new SimpleXmlWriterTraceListener(stream, defaultName)
        
    new(initData:string, name:string) = 
        new SimpleXmlWriterTraceListener(initData, createStream initData |> createStreamWriter |> createWriter, name)
    new(initData:string) = 
        new SimpleXmlWriterTraceListener(initData, defaultName)
    interface IDuplicateListener with
        member x.Duplicate name =
            if initData <> defaultInitData then
                let newPath = CopyListenerHelper.createNewFilename initData name
                CopyListenerHelper.copyListener 
                    x
                    (new SimpleXmlWriterTraceListener(newPath) :> TraceListener)
            else // we can't duplicate
                new SimpleXmlWriterTraceListener(w, name) :> TraceListener
    override x.Dispose(isDisposing) =
        base.Dispose(isDisposing) 
        if isDisposing then
#if NET40
            () // dispose not accessible
#else
            w.Dispose()
#endif
    override x.Close () = w.Close()
    override x.Fail (msg, detail) =
         x.TraceEvent(null, null, TraceEventType.Error, 0, System.String.Concat(msg, " ", detail))
     
    override x.TraceData(cache, source, eventType, id, data:obj) = 
        traceCore cache source eventType id None true (TraceData [|data|])
    override x.TraceData(cache, source, eventType, id, [<ParamArray>] data:obj array) =
        traceCore cache source eventType id None true (TraceData data)

    override x.TraceEvent(cache, source, eventType, id, message:string) =
        traceCore cache source eventType id None true (Message message)
    override x.TraceEvent(cache, source, eventType, id, format:string, [<ParamArray>] args:obj array) =
        traceCore cache source eventType id None true (Message (System.String.Format(format, args)))
    
    
    override x.TraceTransfer(cache, source, id, message, relatedId) =
        traceCore cache source TraceEventType.Transfer id (Some relatedId) true (Message message)
     
    override x.Write(message) =
        x.WriteLine(message)
    
    override x.WriteLine(message:string) =
        traceCore null "Trace" TraceEventType.Information 0 None false (Message message)

#endif
    
    
    
    
    