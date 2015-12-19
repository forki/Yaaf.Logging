// Weitere Informationen zu F# unter "http://fsharp.net".
// Weitere Hilfe finden Sie im Projekt "F#-Lernprogramm".
namespace Yaaf.LoggingTest


module TestCode = 
    open Yaaf.Logging
    open Yaaf.Logging.AsyncTracing

    let inline testCode () = 
        Log.Verb (fun () -> "starting testcode...")
        let waits = new System.Threading.CountdownEvent(11)
        for i in 0 .. 10 do
            async {
                Log.Verb (fun () -> "async...")
                printfn "working..."
                waits.Signal() |> ignore
            } |> Log.TraceMeAs (sprintf "Task_%d" i) |> Async.Start
        waits.Wait()
namespace Yaaf.LoggingTest.Test1 
open Yaaf.Logging

module Test1Module = 
    let code () = 
        Yaaf.LoggingTest.TestCode.testCode()

namespace Yaaf.LoggingTest.Test2
open Yaaf.Logging
open Yaaf.Logging.AsyncTracing

module Test2Module = 
    let code () = 
        Yaaf.LoggingTest.TestCode.testCode()

namespace Yaaf.LoggingTest
open System
open System.Diagnostics
open System.IO
open NUnit.Framework
open Yaaf.Logging
open Yaaf.Logging.AsyncTracing
open Swensen.Unquote
[<TestFixture>]
type ``Empty Test``() = 
    [<SetUp>]
    member x.Setup() =
#if PCL
        let toTraceEventType (te:TraceEventType) = 
            enum (int te) : System.Diagnostics.TraceEventType
        let toSourceLevels (sl:SourceLevels) = 
            enum (int sl) : System.Diagnostics.SourceLevels
        let fromSourceLevels (sl:System.Diagnostics.SourceLevels) = 
            enum (int sl) : SourceLevels


        let fromTraceSource (ts: TraceSource) = 
            { new ITraceSource with
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
            }
        
        let fromStackFrame (sf: StackFrame) = 
            { new IStackFrame with
                member x.GetMethod () = sf.GetMethod()
                member x.GetFileName () = sf.GetFileName()
                member x.GetFileLineNumber () = sf.GetFileLineNumber()
            }
        let backend = 
            { new ILoggingBackend with
                member x.CurrentActivityId 
                    with get () = Trace.CorrelationManager.ActivityId 
                    and set id = Trace.CorrelationManager.ActivityId <- id
                member x.GetLogicalData(name) =
                    System.Runtime.Remoting.Messaging.CallContext.LogicalGetData(name)
                member x.SetLogicalData(name, o) =
                    System.Runtime.Remoting.Messaging.CallContext.LogicalSetData(name, o)
                member x.CreateTraceSource (name, instance) = 
                    TraceSource(name)
                    //match instance with
                    //| Some ins -> MyTraceSource(name, ins) :> TraceSource
                    //| None -> TraceSource(name)
                    |> fromTraceSource
                member x.CreateStackFrame (walk:int, t:bool) =
                    new StackFrame(walk + 1, t)
                    |> fromStackFrame
            }
        Log.SetBackend backend
#else
        ()
#endif

    [<Test>]
    member this.``check that we can throw the same exception twice`` () = 
        let exn = new System.InvalidOperationException()
        raises<System.InvalidOperationException> <@ raise (exn) @>
        raises<System.InvalidOperationException> <@ raise (exn) @>
        ()


module EntryPoint =
    open Yaaf.Logging
    open Yaaf.Logging.AsyncTracing
    open System.Diagnostics
      
    //[<EntryPoint>]
    let main argv = 
        printfn "%A" argv
#if NO_PCL
        // First everything goes to Yaaf.Logging
        Log.UnhandledSource.Wrapped.Listeners.Add (Log.ConsoleLogger SourceLevels.Verbose) |> ignore
        Yaaf.LoggingTest.TestCode.testCode()
        Yaaf.LoggingTest.Test1.Test1Module.code()
        Yaaf.LoggingTest.Test2.Test2Module.code()
        
        // We can change this behaviour to our own
        Log.SetUnhandledSource (Log.Source "MyUnhandled")
        Yaaf.LoggingTest.TestCode.testCode()
        Yaaf.LoggingTest.Test1.Test1Module.code()
        Yaaf.LoggingTest.Test2.Test2Module.code()

        // now we set our namespace sources -> now everything goes where it should go
        assert Log.TraceSourceDict.TryAdd("Yaaf.LoggingTest", Log.Source "Yaaf.LoggingTest")
        assert Log.TraceSourceDict.TryAdd("Yaaf.LoggingTest.Test1", Log.Source "Yaaf.LoggingTest.Test1")
        assert Log.TraceSourceDict.TryAdd("Yaaf.LoggingTest.Test2", Log.Source "Yaaf.LoggingTest.Test2")
        
        Yaaf.LoggingTest.TestCode.testCode()
        Yaaf.LoggingTest.Test1.Test1Module.code()
        Yaaf.LoggingTest.Test2.Test2Module.code()
#endif
        0 // Exitcode aus ganzen Zahlen zurückgeben
