// ----------------------------------------------------------------------------
// This file is subject to the terms and conditions defined in
// file 'LICENSE.txt', which is part of this source code package.
// ----------------------------------------------------------------------------

(*
    This file handles the complete build process of Yaaf.Logging

    The first step is handled in build.sh and build.cmd by bootstrapping a NuGet.exe and 
    executing NuGet to resolve all build dependencies (dependencies required for the build to work, for example FAKE)

    The secound step is executing this file which resolves all dependencies, builds the solution and executes all unit tests
*)

open BuildConfigDef
open System.Collections.Generic
open System.IO

open Fake
open Fake.Git
open Fake.FSharpFormatting
open AssemblyInfoFile

if isMono then
    monoArguments <- "--runtime=v4.0 --debug"
let findProjects (buildParams:BuildParams) =
    !! (sprintf "src/source/**/*.%s.fsproj" buildParams.CustomBuildName)
    :> _ seq
let findTestProjects (buildParams:BuildParams) =
    !! (sprintf "src/test/**/*.%s.fsproj" buildParams.CustomBuildName)
    :> _ seq
let buildConfig =
 // Read release notes document
 let release = ReleaseNotesHelper.parseReleaseNotes (File.ReadLines "doc/ReleaseNotes.md")
 { BuildConfiguration.Defaults with
    ProjectName = "Yaaf.Logging"
    CopyrightNotice = "Yaaf.Logging Copyright © Matthias Dittrich 2011-2015"
    ProjectSummary = "Yaaf.Logging is a simple abstraction layer over a (simple) dependency injection library."
    ProjectDescription = "Yaaf.Logging is a simple abstraction layer over a (simple) dependency injection library."
    ProjectAuthors = ["Matthias Dittrich"]
    NugetTags =  "logging tracesource C# F# dotnet .net"
    PageAuthor = "Matthias Dittrich"
    GithubUser = "matthid"
    Version = release.NugetVersion
    NugetPackages =
      [ "Yaaf.Logging.nuspec", (fun config p ->
          { p with
              Version = config.Version
              ReleaseNotes = toLines release.Notes
              Dependencies = [ ] }) ]
    UseNuget = true
    SetAssemblyFileVersions = (fun config ->
      let info =
        [ Attribute.Company config.ProjectName
          Attribute.Product config.ProjectName
          Attribute.Copyright config.CopyrightNotice
          Attribute.Version config.Version
          Attribute.FileVersion config.Version
          Attribute.InformationalVersion config.Version]
      CreateFSharpAssemblyInfo "./src/SharedAssemblyInfo.fs" info)
    EnableProjectFileCreation = false
    BuildTargets =
     [ { BuildParams.Empty with
          // The default build
          CustomBuildName = "net40"
          SimpleBuildName = "net40"
          FindProjectFiles = findProjects
          FindTestFiles = findTestProjects }
       { BuildParams.Empty with
          // The generated templates
          CustomBuildName = "portable-net45+netcore45+wpa81+MonoAndroid1+MonoTouch1"
          SimpleBuildName = "profile111"
          FindProjectFiles = findProjects
          FindTestFiles = findTestProjects
          FindUnitTestDlls =
            // Don't run on mono.
            if isMono then (fun _ -> Seq.empty) else BuildParams.Empty.FindUnitTestDlls }
       { BuildParams.Empty with
          // The generated templates
          CustomBuildName = "net45"
          SimpleBuildName = "net45"
          FindProjectFiles = findProjects
          FindTestFiles = findTestProjects } ]
  }

