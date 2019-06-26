#!/usr/bin/env fsharpi

open System
open System.IO
open System.Linq
open System.Diagnostics

#r "System.Configuration"
#load "InfraLib/Misc.fs"
#load "InfraLib/Process.fs"
#load "InfraLib/Git.fs"
open FSX.Infrastructure
open Process

type BinaryConfig =
    | Debug
    | Release
    override self.ToString() =
        sprintf "%A" self

let rec private GatherTarget (args: string list, targetSet: Option<string>): Option<string> =
    match args with
    | [] -> targetSet
    | head::tail ->
        if (targetSet.IsSome) then
            failwith "only one target can be passed to make"
        GatherTarget (tail, Some (head))

let buildConfigContents =
    let buildConfig = FileInfo (Path.Combine (__SOURCE_DIRECTORY__, "build.config"))
    if not (buildConfig.Exists) then
        Console.Error.WriteLine "ERROR: configure hasn't been run yet, run ./configure.sh first"
        Environment.Exit 1

    let skipBlankLines line = not <| String.IsNullOrWhiteSpace line
    let splitLineIntoKeyValueTuple (line:string) =
        let pair = line.Split([|'='|], StringSplitOptions.RemoveEmptyEntries)
        if pair.Length <> 2 then
            failwith "All lines in build.config must conform to format:\n\tkey=value"
        pair.[0], pair.[1]

    let buildConfigContents =
        File.ReadAllLines buildConfig.FullName
        |> Array.filter skipBlankLines
        |> Array.map splitLineIntoKeyValueTuple
        |> Map.ofArray
    buildConfigContents

let GetOrExplain key map =
    match map |> Map.tryFind key with
    | Some k -> k
    | None   -> failwithf "No entry exists in build.config with a key '%s'." key

let prefix = buildConfigContents |> GetOrExplain "Prefix"
let libInstallPath = DirectoryInfo (Path.Combine (prefix, "lib", "tcpecho"))
let binInstallPath = DirectoryInfo (Path.Combine (prefix, "bin"))

let launcherScriptPath = FileInfo (Path.Combine (__SOURCE_DIRECTORY__, "bin", "tcpecho"))

let wrapperScript = """#!/bin/sh
set -e
exec mono "$TARGET_DIR/$TCPECHO_PROJECT.exe" "$@"
"""

let rootDir = DirectoryInfo(Path.Combine(__SOURCE_DIRECTORY__, ".."))

let PrintNugetVersion () =
    let nugetExe = Path.Combine(rootDir.FullName, ".nuget", "nuget.exe") |> FileInfo
    if not (nugetExe.Exists) then
        false
    else
        let nugetProc = Process.Execute ({ Command = "mono"; Arguments = nugetExe.FullName }, Echo.Off)
        Console.WriteLine nugetProc.Output.StdOut
        if nugetProc.ExitCode = 0 then
            true
        else
            Console.Error.WriteLine nugetProc.Output.StdErr
            Console.WriteLine()
            failwith "nuget process' output contained errors ^"

let JustBuild binaryConfig =
    let buildTool = Map.tryFind "BuildTool" buildConfigContents
    if buildTool.IsNone then
        failwith "A BuildTool should have been chosen by the configure script, please report this bug"

    Console.WriteLine "Compiling tcpecho..."
    let configOption = sprintf "/p:Configuration=%s" (binaryConfig.ToString())
    let configOptions =
        match buildConfigContents |> Map.tryFind "DefineConstants" with
        | Some constants -> sprintf "%s;DefineConstants=%s" configOption constants
        | None   -> configOption
    let buildProcess = Process.Execute ({ Command = buildTool.Value; Arguments = configOptions }, Echo.All)
    if (buildProcess.ExitCode <> 0) then
        Console.Error.WriteLine (sprintf "%s build failed" buildTool.Value)
        PrintNugetVersion() |> ignore
        Environment.Exit 1

    Directory.CreateDirectory(launcherScriptPath.Directory.FullName) |> ignore
    let wrapperScriptWithPaths =
        wrapperScript.Replace("$TARGET_DIR", libInstallPath.FullName)
    File.WriteAllText (launcherScriptPath.FullName, wrapperScriptWithPaths)

let MakeCheckCommand (commandName: string) =
    if not (Process.CommandWorksInShell commandName) then
        Console.Error.WriteLine (sprintf "%s not found, please install it first" commandName)
        Environment.Exit 1

let maybeTarget = GatherTarget (Misc.FsxArguments(), None)
match maybeTarget with
| None ->
    Console.WriteLine "Building make in DEBUG mode..."
    JustBuild BinaryConfig.Debug

| Some("release") ->
    JustBuild BinaryConfig.Release

| Some "nuget" ->
    Console.WriteLine "This target is for debugging purposes."

    if not (PrintNugetVersion()) then
        Console.Error.WriteLine "Nuget executable has not been downloaded yet, try `make` alone first"
        Environment.Exit 1

| Some("restore") ->
    let buildTool = Map.tryFind "BuildTool" buildConfigContents
    if buildTool.IsNone then
       failwith "A BuildTool should have been chosen by the configure script, please report this bug"

    Console.WriteLine "Restoring Packages..."
    let restoreProcess = Process.Execute ({ Command = buildTool.Value; Arguments = "-t:restore TcpEcho.sln" }, Echo.All)
    if (restoreProcess.ExitCode <> 0) then
        Console.Error.WriteLine (sprintf "%s restore failed" buildTool.Value)
        PrintNugetVersion() |> ignore
        Environment.Exit 1

| Some("check") ->
    Console.WriteLine "Running tests for net461..."
    Console.WriteLine ()

    let nunitCommand = "nunit-console"
    MakeCheckCommand nunitCommand
    let testAssembly = "Client.Tests"
    let testAssemblyPath = Path.Combine(__SOURCE_DIRECTORY__, "src", testAssembly, "bin", "Debug", "net461",
                                        "ClientTests" + ".dll")
    if not (File.Exists(testAssemblyPath)) then
        failwithf "File not found: %s" testAssemblyPath
    let nunitRun = Process.Execute({ Command = nunitCommand; Arguments = testAssemblyPath },
                                   Echo.All)
    if (nunitRun.ExitCode <> 0) then
        Console.Error.WriteLine "Tests failed"
        Environment.Exit 1

| Some(someOtherTarget) ->
    Console.Error.WriteLine("Unrecognized target: " + someOtherTarget)
    Environment.Exit 2
