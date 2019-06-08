using System;
using System.Buffers;
using System.IO;
using System.IO.Pipelines;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace TcpEcho {
    public class BlockchainTransactionGet {
        [JsonProperty ("id")]
        public int Id { get; set; }

        [JsonProperty ("result")]
        public string Result { get; set; }
    }

    public class StratumClient : Client {
        public StratumClient (string _endpoint, int _port) : base (_endpoint, _port) { }

        public async Task<BlockchainTransactionGet> BlockchainTransactionGet (int id, string transactionId) {
            var stringBuilder = new StringBuilder();
            var stringWriter = new StringWriter(stringBuilder);
            var jsonWriter = new JsonTextWriter(stringWriter);
            jsonWriter.WriteStartObject();
            jsonWriter.WritePropertyName("id");
            jsonWriter.WriteValue(id);
            jsonWriter.WritePropertyName("method");
            jsonWriter.WriteValue("blockchain.transaction.get");
            jsonWriter.WritePropertyName("params");
            jsonWriter.WriteStartArray();
            jsonWriter.WriteValue(transactionId);
            jsonWriter.WriteEnd();
            jsonWriter.WriteEndObject();
            var json = stringBuilder.ToString();
            var returnedJson = await Call (json);
            var obj = JsonConvert.DeserializeObject<BlockchainTransactionGet> (returnedJson);
            return obj;
        }
    }
}
