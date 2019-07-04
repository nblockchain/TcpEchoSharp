namespace ElectrumServerDiscovery

open System
open System.Net


type ElectrumServer =
    {
        Fqdn: string
        Pruning: string
        PrivatePort: Option<int>
        UnencryptedPort: Option<int>
        Version: string
    }
    member self.CheckCompatibility (): unit =
        if self.UnencryptedPort.IsNone then
            raise <| NotSupportedException("TLS not yet supported")
        if self.Fqdn.EndsWith ".onion" then
            raise <| NotSupportedException("Tor(onion) not yet supported")

module ElectrumJsonFileParser =

    open FSharp.Data
    open FSharp.Data.JsonExtensions

    let private ExtractServerListFromGeeWallet () =
        use webClient = new WebClient()
        let serverListInJson =
            webClient.DownloadString
                "https://gitlab.com/knocte/geewallet/raw/stable/src/GWallet.Backend/UtxoCoin/btc-servers.json"
        let serversParsed = FSharp.Data.JsonValue.Parse serverListInJson
        let servers =
            seq {
                for (key,value) in serversParsed.Properties do
                    let maybeUnencryptedPort = value.TryGetProperty "t"
                    let unencryptedPort =
                        match maybeUnencryptedPort with
                        | None -> None
                        | Some portAsString -> Some (Int32.Parse (portAsString.AsString()))
                    let maybeEncryptedPort = value.TryGetProperty "s"
                    let encryptedPort =
                        match maybeEncryptedPort with
                        | None -> None
                        | Some portAsString -> Some (Int32.Parse (portAsString.AsString()))
                    yield { Fqdn = key;
                            Pruning = value?pruning.AsString();
                            PrivatePort = encryptedPort;
                            UnencryptedPort = unencryptedPort;
                            Version = value?version.AsString(); }
            }
        servers |> List.ofSeq

    let private FilterCompatibleServer (electrumServer: ElectrumServer) =
        try
            electrumServer.CheckCompatibility()
            true
        with
        | :? NotSupportedException -> false

    let ExtractServerList () =
        ExtractServerListFromGeeWallet ()
            |> List.filter FilterCompatibleServer
