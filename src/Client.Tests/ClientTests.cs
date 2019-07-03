using System;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Diagnostics;
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

        private async Task LoopThroughElectrumServers (Func<TcpEcho.StratumClient,Task> action,
                                                       uint repeatTimes = 0,
                                                       List<Exception> exceptionsSoFar = null) {
            var successfulCount = 0;
            var exceptions = exceptionsSoFar == null ? new List<Exception>() : exceptionsSoFar;
            Console.WriteLine();

            for (int i = 0; i < servers.Length; i++) {
                Console.Write($"Trying to query '{servers[i]}'... ");
                try {
                    var client = new TcpEcho.StratumClient (servers[i], 50001);
                    await action(client);
                    Console.WriteLine("success");
                    successfulCount++;
                }
                catch (Exception ex)
                {
                    if (ex is TcpEcho.CommunicationUnsuccessfulException || ex.InnerException is TcpEcho.CommunicationUnsuccessfulException)
                        Console.Error.WriteLine("failure (unreliable server)");
                    else
                    {
                        Console.Error.WriteLine("failure (buggy client)");
                        exceptions.Add(ex);
                    }
                }
            }
            var successRatePercentage = 100.0 * successfulCount / servers.Length;
            Console.WriteLine($"Success rate: {successRatePercentage}% ({successfulCount} out of {servers.Length})");

            if (repeatTimes > 0) {
                await LoopThroughElectrumServers(action, repeatTimes - 1, exceptions);
                Assert.That (successfulCount, Is.GreaterThan(0));
            }
            else {
                Assert.That(exceptions.Count, Is.EqualTo(0),
                            "There were some exceptions: " + Environment.NewLine + Environment.NewLine +
                            String.Join(Environment.NewLine + Environment.NewLine, exceptions.Select(ex => ex.ToString())) +
                            Environment.NewLine + Environment.NewLine);
                Assert.That(successfulCount, Is.GreaterThan(0));
            }
        }

        [Test]
        public async Task ConnectWithElectrumServersTransactionGet () {
            await LoopThroughElectrumServers(async client => {
                var result = await client.BlockchainTransactionGet (
                    17,
                    "2f309ef555110ab4e9c920faa2d43e64f195aa027e80ec28e1d243bd8929a2fc"
                );
                Assert.That(result, Is.Not.Null);
            }, 2);
        }

        [Test]
        public async Task ConnectWithElectrumServersEstimateFee () {
            await LoopThroughElectrumServers(async client => {
                var result = await client.BlockchainEstimateFee (17, 6);
                Assert.That(result, Is.Not.Null);
            }, 2);
        }

        [Test]
        public async Task ProperNonEternalTimeout()
        {
            var someRandomIP = "52.1.57.181";
            bool? succesful = null;

            var stopWatch = new Stopwatch();
            stopWatch.Start();
            try
            {
                var client = new TcpEcho.StratumClient(someRandomIP, 50001);
                var result = await client.BlockchainTransactionGet(
                    17,
                    "2f309ef555110ab4e9c920faa2d43e64f195aa027e80ec28e1d243bd8929a2fc"
                );
                succesful = true;
            }
            catch
            {
                succesful = false;
                stopWatch.Stop();
            }
            Assert.That(succesful.HasValue, Is.EqualTo(true), "test is broken?");
            Assert.That(succesful.Value, Is.EqualTo(false), "IP is not too random? port was open actually!");
            Assert.That(stopWatch.Elapsed, Is.LessThan(TimeSpan.FromSeconds(2)));
        }
    }
}
