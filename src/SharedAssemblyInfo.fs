namespace System
open System.Reflection

[<assembly: AssemblyCompanyAttribute("Yaaf.Logging")>]
[<assembly: AssemblyProductAttribute("Yaaf.Logging")>]
[<assembly: AssemblyCopyrightAttribute("Yaaf.Logging Copyright © Matthias Dittrich 2011-2015")>]
[<assembly: AssemblyVersionAttribute("1.0.0.0")>]
[<assembly: AssemblyFileVersionAttribute("1.0.0.0")>]
[<assembly: AssemblyInformationalVersionAttribute("1.0.0")>]
do ()

module internal AssemblyVersionInformation =
    let [<Literal>] Version = "1.0.0.0"
