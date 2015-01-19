namespace System
open System.Reflection

[<assembly: AssemblyVersionAttribute("2014.11.1")>]
[<assembly: AssemblyFileVersionAttribute("2014.11.1")>]
do ()

module internal AssemblyVersionInformation =
    let [<Literal>] Version = "2014.11.1"
