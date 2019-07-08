
using System;
using System.Buffers;
using System.IO.Pipelines;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;


namespace TcpEcho {

    public class CommunicationUnsuccessfulException : Exception
    {
        internal CommunicationUnsuccessfulException(string msg, Exception innerException) : base(msg, innerException)
        {
        }
    }

    public abstract class JsonRpcClient {
        private const int minimumBufferSize = 1024;
        private readonly TimeSpan tcpTimeout = TimeSpan.FromSeconds(1);
        private readonly string endpoint;
        private readonly int port;

        public JsonRpcClient(string endpoint, int port) {
            this.endpoint = endpoint;
            this.port = port;
        }

        private async Task<string> CallImpl (string json) {
            using (Socket socket = new Socket (SocketType.Stream, ProtocolType.Tcp)) {
                socket.ReceiveTimeout = (int)tcpTimeout.TotalMilliseconds;

                var connectTimeOut = Task.Delay (tcpTimeout);
                var completedConnTask = await Task.WhenAny (connectTimeOut, socket.ConnectAsync (endpoint, port));
                if (completedConnTask == connectTimeOut) {
                    throw new TimeoutException("connect timed out");
                }

                byte[] bytesToSend = Encoding.UTF8.GetBytes (json + Environment.NewLine);
                socket.Send (bytesToSend);

                var pipe = new Pipe ();
                var writing = WriteToPipeAsync (socket, pipe.Writer);
                var reading = ReadFromPipeAsync (pipe.Reader);

                var readAndWriteTask = Task.WhenAll (reading, writing);
                var readTimeOut = Task.Delay (tcpTimeout);
                var completedReadTask = await Task.WhenAny (readTimeOut, readAndWriteTask);
                if (completedReadTask == readTimeOut) {
                    throw new TimeoutException("reading/writing from socket timed out");
                }

                return await reading;
            }
        }

        protected async Task<string> Call(string json)
        {
            try
            {
                var reply = await CallImpl(json);
                if (String.IsNullOrEmpty(reply)) {
                    throw new Exception("Some problem while reading reply: it was empty");
                }
                return reply;
            }
            catch (SocketException ex)
            {
                throw new CommunicationUnsuccessfulException(ex.Message, ex);
            }
            catch (TimeoutException ex)
            {
                throw new CommunicationUnsuccessfulException(ex.Message, ex);
            }
        }

        private static async Task WriteToPipeAsync (Socket socket, PipeWriter writer) {
            await Task.Yield();
            while (true)
            {
                if (socket.Available > 0)
                {
                    var memory = writer.GetMemory(minimumBufferSize);
                    var bytesRead = await socket.ReceiveAsync(memory, SocketFlags.None);
                    writer.Advance(bytesRead);
                }

                var flushResult = await writer.FlushAsync ();
                if (flushResult.IsCompleted) {
                    break;
                }
            }

            writer.Complete ();
        }

        private static async Task<string> ReadFromPipeAsync (PipeReader reader) {
            var strResult = String.Empty;
            while (true) {
                ReadResult result = await reader.ReadAsync ();
                ReadOnlySequence<byte> buffer = result.Buffer;

                var content = UTF8String (buffer);
                strResult += content;
                reader.AdvanceTo (buffer.End, buffer.End);

                if (result.IsCompleted || strResult.EndsWith("\n")) {
                    break;
                }
            }

            reader.Complete();
            return strResult;
        }

        private static string UTF8String (ReadOnlySequence<byte> buffer) {
            var result = String.Empty;
            foreach (ReadOnlyMemory<byte> segment in buffer)
            {
#if NETCOREAPP2_1
                result += Encoding.UTF8.GetString (segment.Span);
#else
                result += Encoding.UTF8.GetString (segment);
#endif
            }
            return result;
        }
    }
}


#if NET461

internal static class Extensions
{
    public static Task<int> ReceiveAsync(this Socket socket, Memory<byte> memory, SocketFlags socketFlags)
    {
        var arraySegment = GetArray(memory);
        return SocketTaskExtensions.ReceiveAsync(socket, arraySegment, socketFlags);
    }

    public static string GetString(this Encoding encoding, ReadOnlyMemory<byte> memory)
    {
        var arraySegment = GetArray(memory);
        return encoding.GetString(arraySegment.Array, arraySegment.Offset, arraySegment.Count);
    }

    private static ArraySegment<byte> GetArray(Memory<byte> memory)
    {
        return GetArray((ReadOnlyMemory<byte>)memory);
    }

    private static ArraySegment<byte> GetArray(ReadOnlyMemory<byte> memory)
    {
        if (!MemoryMarshal.TryGetArray(memory, out var result))
        {
            throw new InvalidOperationException("Buffer backed by array was expected");
        }

        return result;
    }
}

#endif
