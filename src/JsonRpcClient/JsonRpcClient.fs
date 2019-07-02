namespace TcpEcho

open System
open System.Buffers
open System.Net.Sockets
open System.IO.Pipelines
open System.Text
open System.Threading.Tasks

type CommunicationUnsuccessfulException(msg: string, innerException: Exception) =
    inherit Exception(msg, innerException)

exception IncompleteResponseException of string
type TimeoutOrResult<'a> = Timeout | Result of 'a

[<AbstractClass>]
type Client(endpoint: string, port: int) =
    let minimumBufferSize = 1024

    let mkString (buffer: ReadOnlySequence<byte>) =
        seq { for segment in buffer -> segment.ToArray() |> Encoding.UTF8.GetString } |> Seq.fold (+) ""

    let withTimeout (timeout : int) (xTask: Task<'a>) = async {
        let delay = Task.Delay(timeout)
        let! timoutTask = Task.WhenAny(xTask, delay) |> Async.AwaitTask
        return if timoutTask = delay then Timeout else Result xTask.Result
    }

    let rec writeToPipeAsync (writer: PipeWriter) (socket: Socket) = async {
        let segment = Array.zeroCreate<byte> minimumBufferSize |> ArraySegment
        let! read = socket.ReceiveAsync(segment, SocketFlags.None) |> withTimeout socket.ReceiveTimeout
        match read with
        | Timeout ->
            return writer.Complete(TimeoutException("Socket read timed out"))
         | Result 0 ->
            return writer.Complete()
        | Result bytesRead ->
            segment.Array.CopyTo(writer.GetMemory(bytesRead))
            writer.Advance bytesRead
            let! flusher = writer.FlushAsync().AsTask() |> Async.AwaitTask
            if flusher.IsCompleted then
                return writer.Complete()
            else
                return! writeToPipeAsync writer socket 
    }

    let rec readFromPipeAsync (reader: PipeReader) str = async {
        let! result = reader.ReadAsync().AsTask() |> Async.AwaitTask |> Async.Catch
        match result with
        | Choice1Of2 result ->
            let buffer: ReadOnlySequence<byte> = result.Buffer
            reader.AdvanceTo(buffer.End, buffer.End)

            let totalString = str + mkString buffer
            match result.IsCompleted with
            | true ->
                reader.Complete()
                return totalString
            | false ->
                return! readFromPipeAsync reader totalString
        | Choice2Of2 _ -> // AggregateException, so we cant match on TimeoutException
            return raise <| IncompleteResponseException str
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
