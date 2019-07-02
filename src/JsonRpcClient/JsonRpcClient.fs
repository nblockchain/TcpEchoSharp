namespace TcpEcho

open System
open System.Buffers
open System.Net.Sockets
open System.IO.Pipelines
open System.Text
open System.Threading.Tasks

[<AbstractClass>]
type Client(endpoint: string, port: int) =
#if NETCOREAPP2_1
    let toString (segment : ReadOnlyMemory<byte>) = Encoding.UTF8.GetString segment.Span

    let receiveAsync (socket: Socket) (buffer: Memory<byte>) (flags: SocketFlags) = SocketTaskExtensions.ReceiveAsync(socket, buffer, flags) |> fun s -> s.AsTask()
#else
    let toString (segment : ReadOnlyMemory<byte>) = segment.ToArray() |> Encoding.UTF8.GetString

    let toArray (memory: ReadOnlyMemory<byte>) =
        match System.Runtime.InteropServices.MemoryMarshal.TryGetArray(memory) with
        | true, segment -> segment
        | false, _ -> failwith "Buffer backed by array was expected"

    let receiveAsync (socket: Socket) (buffer: Memory<byte>) (flags: SocketFlags) =
        let segment = buffer |> Memory.op_Implicit |> toArray
        SocketTaskExtensions.ReceiveAsync(socket, segment, flags)
#endif

    let minimumBufferSize = 1024

    let mkString (buffer: ReadOnlySequence<byte>) =
        seq { for segment in buffer -> toString segment } |> Seq.fold (+) ""

    let rec writeToPipeAsync (writer: PipeWriter) (socket: Socket) = async {
        let memory = writer.GetMemory(minimumBufferSize)

        let! read = receiveAsync socket memory SocketFlags.None |> Async.AwaitTask
        match read with
        | 0 -> return writer.Complete()
        | bytesRead ->
            writer.Advance bytesRead
            let! flusher = writer.FlushAsync().AsTask() |> Async.AwaitTask
            if flusher.IsCompleted then return writer.Complete()

        match socket.Available with
        | 0 -> return writer.Complete()
        | _ -> return! writeToPipeAsync writer socket
    }

    let rec readFromPipeAsync (reader: PipeReader) str = async {
        let! result = reader.ReadAsync().AsTask() |> Async.AwaitTask
        let buffer : ReadOnlySequence<byte> = result.Buffer
        reader.AdvanceTo(buffer.End, buffer.End)

        let totalString = str + mkString buffer
        match result.IsCompleted with
        | true ->
            reader.Complete()
            return totalString
        | false ->
            return! readFromPipeAsync reader totalString
    }
    abstract member Call: string -> Task<string>
    default __.Call (json: string) =
        async {
            use socket = new Socket(SocketType.Stream, ProtocolType.Tcp)
            do! socket.ConnectAsync(endpoint, port) |> Async.AwaitTask

            let bytes = UTF8Encoding.UTF8.GetBytes(json + Environment.NewLine)

            socket.Send(bytes) |> ignore

            let pipe = Pipe()

            let writer = writeToPipeAsync pipe.Writer socket |> Async.StartAsTask
            let reader = readFromPipeAsync pipe.Reader "" |> Async.StartAsTask

            do! [ writer :> Task; reader :> Task] |> Task.WhenAll |> Async.AwaitTask
            return! Async.AwaitTask reader
        } |> Async.StartAsTask
