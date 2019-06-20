namespace TcpEcho

open System
open System.Buffers
open System.Net.Sockets
open System.IO.Pipelines
open System.Text
open System.Threading.Tasks

type CommunicationUnsuccessfulException(msg: string, innerException: Exception) =
    inherit Exception(msg, innerException)

[<AbstractClass>]
type Client(endpoint: string, port: int) =
    let minimumBufferSize = 1024

    let mkString (buffer: ReadOnlySequence<byte>) =
        seq { for segment in buffer -> segment.ToArray() |> Encoding.UTF8.GetString } |> Seq.fold (+) ""

    let withTimeout (timeout : int) (xAsync: Async<'a>) = async {
        try
            let! completor = Async.StartChild(xAsync, timeout)
            let! value = completor
            return Some value
        with
            :? TimeoutException -> return None
    }

    let rec writeToPipeAsync (writer: PipeWriter) (socket: Socket) = async {
        let segment = Array.zeroCreate<byte> minimumBufferSize |> ArraySegment
        let! read = socket.ReceiveAsync(segment, SocketFlags.None) |> Async.AwaitTask |> withTimeout socket.ReceiveTimeout
        match read with
        | Some 0 | None ->
            return writer.Complete()
        | Some bytesRead ->
            segment.Array.CopyTo(writer.GetMemory(bytesRead))
            writer.Advance bytesRead
            let! flusher = writer.FlushAsync().AsTask() |> Async.AwaitTask
            if flusher.IsCompleted then
                return writer.Complete()
            else
                return! writeToPipeAsync writer socket
    }

    let rec readFromPipeAsync (reader: PipeReader) str = async {
        let! result = reader.ReadAsync().AsTask() |> Async.AwaitTask
        let buffer: ReadOnlySequence<byte> = result.Buffer
        reader.AdvanceTo(buffer.End, buffer.End)

        let totalString = str + mkString buffer
        match result.IsCompleted with
        | true ->
            reader.Complete()
            return totalString
        | false ->
            return! readFromPipeAsync reader totalString
    }

    let CallImplAsync (json: string) =
        async {
            use socket = new Socket(SocketType.Stream, ProtocolType.Tcp)
            socket.ReceiveTimeout <- 500
            do! socket.ConnectAsync(endpoint, port) |> Async.AwaitTask

            let bytes = UTF8Encoding.UTF8.GetBytes(json + Environment.NewLine)

            socket.Send(bytes) |> ignore

            let pipe = Pipe()

            let! _ = writeToPipeAsync pipe.Writer socket |> Async.StartChild
            return! readFromPipeAsync pipe.Reader ""
        }

    abstract member CallAsync: string -> Async<string>
    abstract member CallAsyncAsTask: string -> Task<string>

    default __.CallAsync (json: string) =
        async {
            try
                return! CallImplAsync json
            with
            | :? AggregateException as ae when ae.Flatten().InnerExceptions |> Seq.exists (fun x -> x :? SocketException) ->
                return raise <| CommunicationUnsuccessfulException(ae.Message, ae)
            | :? SocketException as ex ->
                return raise <| CommunicationUnsuccessfulException(ex.Message, ex)
        }

    default this.CallAsyncAsTask (json: string) =
        this.CallAsync json |> Async.StartAsTask
