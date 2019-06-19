using System;
using System.Threading.Tasks;
using NUnit.Framework;

namespace Client.Tests {
    [TestFixture]
    public class ClientTest {
        public static readonly string[] servers = {
            "185.64.116.15",
            "2azzarita.hopto.org",
            "4cii7ryno5j3axe4.onion",
            "4yi77lkjgy4bwtj3.onion",
            "52.1.56.181",
            "btc.jochen-hoenicke.de",
            "dedi.jochen-hoenicke.de",
            "electrum.coineuskal.com",
            "electrum.petrkr.net",
            "electrum.vom-stausee.de",
            "electrumx-core.1209k.com",
            "ip101.ip-54-37-91.eu",
            "ip119.ip-54-37-91.eu",
            "ip120.ip-54-37-91.eu",
            "ip239.ip-54-36-234.eu",
            "j5jfrdthqt5g25xz.onion",
            "kciybn4d4vuqvobdl2kdp3r2rudqbqvsymqwg4jomzft6m6gaibaf6yd.onion",
            "kirsche.emzy.de",
            "n3o2hpi5xnf3o356.onion",
            "ndndword5lpb7eex.onion",
            "ozahtqwp25chjdjd.onion",
            "sslnjjhnmwllysv4.onion",
            "wofkszvyz7mhn3bb.onion",
            "xray587.startdedicated.de",
            "y4td57fxytoo5ki7.onion",
        };

        [Test]
        public async Task ConnectWithElectrumServersTransactionGet () {
            var hasAtLeastOneSuccessful = false;
            for (int i = 0; i < servers.Length; i++) {
                try {
                    var client = new TcpEcho.StratumClient (servers[i], 50001);
                    var result = await client.BlockchainTransactionGet (
                        17,
                        "2f309ef555110ab4e9c920faa2d43e64f195aa027e80ec28e1d243bd8929a2fc"
                    );
                    Console.Error.WriteLine (result.Result); // Using stderr to show into the console
                    hasAtLeastOneSuccessful = true;
                    break;
                } catch (TcpEcho.ConnectionUnsuccessfulException error) {
                    Console.Error.WriteLine ($"Couldn't request {servers[i]}: {error}");
                }
                catch (AggregateException aggEx)
                {
                    if (!(aggEx.InnerException is TcpEcho.ConnectionUnsuccessfulException))
                        throw;
                }
            }
            Assert.AreEqual (hasAtLeastOneSuccessful, true);
        }

        [Test]
        public async Task ConnectWithElectrumServersEstimateFee () {
            var hasAtLeastOneSuccessful = false;
            for (int i = 0; i < servers.Length; i++) {
                try {
                    var client = new TcpEcho.StratumClient (servers[i], 50001);
                    var result = await client.BlockchainEstimateFee (17, 6);
                    Console.Error.WriteLine (result.Result); // Using stderr to show into the console
                    hasAtLeastOneSuccessful = true;
                    break;
                } catch (Exception error) {
                    Console.Error.WriteLine ($"Couldn't request {servers[i]}: {error}");
                }
            }
            Assert.AreEqual (hasAtLeastOneSuccessful, true);
        }
    }
}
