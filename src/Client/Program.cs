using System;
using System.Threading.Tasks;

namespace TcpEcho {
    class Program {
        static async Task Main (string[] args) {
            var argsLength = args.Length;
            if (argsLength < 3) {
                Console.Error.WriteLine ("Provide an endpoint, a port and a method. E.g.: "
                    + "client.exe some.domain.com 99999 some.method");
                return;
            }

            var endpoint = args[0];
            var method = args[2];
            var port = int.Parse (args[1]);

            var client = new StratumClient (endpoint, port);

            switch (method) {
                case "blockchain.transaction.get":
                    if (argsLength != 5) {
                        Console.Error.WriteLine ("Provide an id and a transaction id. E.g.: "
                            + "client.exe some.domain.com 99999 some.method 99 someTransactionId");
                        return;
                    }
                    var result = await client.BlockchainTransactionGet (int.Parse (args[3]), args[4]);
                    Console.WriteLine(result.Result);
                    break;
                default:
                    Console.Error.WriteLine ("Unknown method. Provide a valid Ethereum method");
                    break;
            }
        }
    }
}