using System;
using System.Buffers;
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
            var json = "{ \"id\": " + id + ", " +
                "\"method\": \"blockchain.transaction.get\", " +
                "\"params\": [\"" + transactionId + "\"] }";
            var returnedJson = await Call (json);
            var obj = JsonConvert.DeserializeObject<BlockchainTransactionGet> (returnedJson);
            return obj;
        }
    }
}