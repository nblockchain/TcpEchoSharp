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

    let withTimeout (timeout : int) (xAsync: Async<'a>) = async {
        let read = async {
            let! value = xAsync
            return value |> Result |> Some
        }
        let delay = async {
            do! Async.Sleep(timeout)
            return Some Timeout
        }

        let! result = Async.Choice([read; delay])
        match result with
        | Some x -> return x
        | None -> return Timeout
    }

    let rec writeToPipeAsync (writer: PipeWriter) (socket: Socket) = async {
        try
            let segment = Array.zeroCreate<byte> minimumBufferSize |> ArraySegment
            let! read = socket.ReceiveAsync(segment, SocketFlags.None) |> Async.AwaitTask |> withTimeout socket.ReceiveTimeout

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
        with ex -> return writer.Complete(ex)
    }

    let rec readFromPipeAsync (reader: PipeReader) (sb: StringBuilder) = async {
        try
            let! result = reader.ReadAsync().AsTask() |> Async.AwaitTask

            let buffer: ReadOnlySequence<byte> = result.Buffer

            buffer |> ref |> BuffersExtensions.ToArray |> Encoding.UTF8.GetString |> sb.Append |> ignore

            reader.AdvanceTo(buffer.End)

            match result.IsCompleted with
            | true ->
                reader.Complete()
                return sb.ToString()
            | false ->
                return! readFromPipeAsync reader sb
        with
            // If we got an TimeoutException anywhere in the exception chain
            | :? AggregateException as ae when ae.Flatten().InnerExceptions |> Seq.exists (fun x -> x :? TimeoutException) ->
                return raise <| IncompleteResponseException (sb.ToString())
    }

    let CallImplAsync (json: string) =
        async {
            use socket = new Socket(SocketType.Stream, ProtocolType.Tcp)
            socket.ReceiveTimeout <- 500
            socket.SendTimeout <- 500
            do! socket.ConnectAsync(endpoint, port) |> Async.AwaitTask

            let segment = UTF8Encoding.UTF8.GetBytes(json + Environment.NewLine) |> ArraySegment

            let! send = socket.SendAsync(segment, SocketFlags.None) |> Async.AwaitTask |> withTimeout socket.SendTimeout
            match send with
            | Result _ ->
                let pipe = Pipe()

                let! _ = writeToPipeAsync pipe.Writer socket |> Async.StartChild
                return! readFromPipeAsync pipe.Reader (StringBuilder())
            | Timeout -> return raise (CommunicationUnsuccessfulException("Socket send timed out", null))
        }

    abstract member CallAsync: string -> Async<string>
    abstract member CallAsyncAsTask: string -> Task<string>

    default __.CallAsync (json: string) =
        async {
            try
                return! CallImplAsync json
            with
            | IncompleteResponseException response as ex when String.IsNullOrWhiteSpace(response) ->
                return raise <| CommunicationUnsuccessfulException("Empty response from socket", ex)
            | IncompleteResponseException _ as ex ->
                return raise <| ex
            | :? AggregateException as ae when ae.Flatten().InnerExceptions |> Seq.exists (fun x -> x :? SocketException) ->
                return raise <| CommunicationUnsuccessfulException(ae.Message, ae)
            | :? SocketException as ex ->
                return raise <| CommunicationUnsuccessfulException(ex.Message, ex)
        }

    default this.CallAsyncAsTask (json: string) =
        this.CallAsync json |> Async.StartAsTask
