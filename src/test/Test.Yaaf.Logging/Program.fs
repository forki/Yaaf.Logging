// Weitere Informationen zu F# unter "http://fsharp.net".
// Weitere Hilfe finden Sie im Projekt "F#-Lernprogramm".
namespace Yaaf.LoggingTest


module TestCode = 
    open Yaaf.Logging
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

module Test2Module = 
    let code () = 
        Yaaf.LoggingTest.TestCode.testCode()

namespace Yaaf.LoggingTest

open NUnit.Framework
[<TestFixture>]
type ``Empty Test``() = 
    [<Test>]
    member this.``just a test to make NUnit happy`` () = ()


module EntryPoint =
    open Yaaf.Logging
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
