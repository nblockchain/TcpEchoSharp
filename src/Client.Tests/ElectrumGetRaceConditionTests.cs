using NUnit.Framework;

namespace Client.Tests {
	[TestFixture]
	public class ElectrumGetRaceConditionTests {
		[TestCase("electrumx-core.1209k.com")]
		[TestCase("electrum.coineuskal.com")]
		public void RaceConditionHappens(string server) {
			var client = new TcpEcho.StratumClient (server, 50001);
			var result = client.BlockchainTransactionGet (
				17,
				"2f309ef555110ab4e9c920faa2d43e64f195aa027e80ec28e1d243bd8929a2fc"
			).Result;
			Assert.That(result, Is.Not.Null);
		}
	}
}