using Newtonsoft.Json;
using System;
using System.Buffers;
using System.IO.Pipelines;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;


namespace TcpEcho {
    public abstract class Client {
        private const int minimumBufferSize = 1024;
        private string endpoint = "";
        private int port = 0;

        public Client (string _endpoint, int _port) {
            endpoint = _endpoint;
            port = _port;
        }

        protected async Task<string> Call (string json) {
            using (Socket socket = new Socket (SocketType.Stream, ProtocolType.Tcp)) {
                socket.ReceiveTimeout = 500;

                await socket.ConnectAsync (endpoint, port);

                byte[] bytesToSend = UTF8Encoding.UTF8.GetBytes (json + Environment.NewLine);
                socket.Send (bytesToSend);

                var pipe = new Pipe ();
                var writing = WriteToPipeAsync (socket, pipe.Writer);
                var reading = ReadFromPipeAsync (pipe.Reader);

                await Task.WhenAll (reading, writing);

                return await reading;
            }
        }

        private static async Task WriteToPipeAsync (Socket socket, PipeWriter writer) {
            int read = 0;
            FlushResult result;

            do {
                Memory<byte> memory = writer.GetMemory (minimumBufferSize);

                if ((read = await socket.ReceiveAsync(memory, SocketFlags.None)) == 0) {
                    break;
                }

                writer.Advance (read);

                if ((result = await writer.FlushAsync ()).IsCompleted) {
                    break;
                }
            }
            while (socket.Available > 0);

            writer.Complete ();
        }

        private static async Task<string> ReadFromPipeAsync (PipeReader reader) {
            var strResult = "";
            while (true) {
                ReadResult result = await reader.ReadAsync ();
                ReadOnlySequence<byte> buffer = result.Buffer;

                var content = UTF8String (buffer);
                strResult += content;
                reader.AdvanceTo (buffer.End, buffer.End);

                if (result.IsCompleted) {
                    break;
                }
            }

            return strResult;
        }

        private static string UTF8String (ReadOnlySequence<byte> buffer) {
            var result = "";
            foreach (ReadOnlyMemory<byte> segment in buffer)
            {
#if NETCOREAPP2_1
                result += UTF8Encoding.UTF8.GetString (segment.Span);
#else
                result += UTF8Encoding.UTF8.GetString (segment);
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