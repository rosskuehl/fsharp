﻿// Copyright (c) Microsoft Corporation.  All Rights Reserved.  See License.txt in the project root for license information.

namespace FSharp.Compiler.Scripting

open System
open System.Threading
open FSharp.Compiler.Interactive.Shell

type FSharpScript(?captureInput: bool, ?captureOutput: bool, ?additionalArgs: string[]) as this =
    let outputProduced = Event<string>()
    let errorProduced = Event<string>()

    // handle stdin/stdout
    let stdin = new CapturedTextReader()
    let stdout = new EventedTextWriter()
    let stderr = new EventedTextWriter()
    do stdout.LineWritten.Add outputProduced.Trigger
    do stderr.LineWritten.Add errorProduced.Trigger
    let captureInput = defaultArg captureInput false
    let captureOutput = defaultArg captureOutput false
    let additionalArgs = defaultArg additionalArgs [||]
    let savedInput = Console.In
    let savedOutput = Console.Out
    let savedError = Console.Error
    do (fun () ->
        if captureInput then
            Console.SetIn(stdin)
        if captureOutput then
            Console.SetOut(stdout)
            Console.SetError(stderr)
        ())()

    let config = FsiEvaluationSession.GetDefaultConfiguration()
#if NETSTANDARD
    let baseArgs = [| this.GetType().Assembly.Location; "--noninteractive"; "--targetprofile:netcore"; "--quiet" |]
#else
    let baseArgs = [| this.GetType().Assembly.Location; "--noninteractive"; "--quiet" |]
#endif
    let argv = Array.append baseArgs additionalArgs
    let fsi = FsiEvaluationSession.Create (config, argv, stdin, stdout, stderr)

    [<CLIEvent>]
    member __.AssemblyReferenceAdded = fsi.AssemblyReferenceAdded

    [<CLIEvent>]
    member __.IncludePathAdded = fsi.IncludePathAdded

    [<CLIEvent>]
    member __.DependencyAdding = fsi.DependencyAdding

    [<CLIEvent>]
    member __.DependencyAdded = fsi.DependencyAdded

    [<CLIEvent>]
    member __.DependencyFailed = fsi.DependencyFailed

    member __.ProvideInput = stdin.ProvideInput

    member __.OutputProduced = outputProduced.Publish

    member __.ErrorProduced = errorProduced.Publish

    member __.Eval(code: string, ?cancellationToken: CancellationToken) =
        let cancellationToken = defaultArg cancellationToken CancellationToken.None
        let ch, errors = fsi.EvalInteractionNonThrowing(code, cancellationToken)
        match ch with
        | Choice1Of2 v -> Ok(v), errors
        | Choice2Of2 ex -> Error(ex), errors

    interface IDisposable with
        member __.Dispose() =
            if captureInput then
                Console.SetIn(savedInput)
            if captureOutput then
                Console.SetOut(savedOutput)
                Console.SetError(savedError)
            stdin.Dispose()
            stdout.Dispose()
            stderr.Dispose()
