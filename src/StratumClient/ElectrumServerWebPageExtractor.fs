namespace ElectrumServerDiscovery

open System
open System.Collections.Generic
open HtmlAgilityPack

module ElectrumServerWebPageExtractor =

    let ExtractServerListFromWebPage (): seq<ElectrumServer> =
        let url = "https://1209k.com/bitcoin-eye/ele.php"
        let web = HtmlWeb()
        let doc = web.Load url
        let firstTable = doc.DocumentNode.SelectNodes("//table").[0]
        let tableBody = firstTable.SelectSingleNode "tbody"
        let servers = tableBody.SelectNodes "tr"
        seq {
            for i in 0..(servers.Count - 1) do
                let server = servers.[i]
                let serverProperties = server.SelectNodes "td"

                if serverProperties.Count = 0 then
                    failwith "Unexpected property count: 0"
                let fqdn = serverProperties.[0].InnerText

                if serverProperties.Count < 2 then
                    failwithf "Unexpected property count in server %s: %i" fqdn serverProperties.Count
                let port = Int32.Parse serverProperties.[1].InnerText

                if serverProperties.Count < 3 then
                    failwithf "Unexpected property count in server %s:%i: %i" fqdn port serverProperties.Count
                let portType = serverProperties.[2].InnerText

                let encrypted =
                    match portType with
                    | "ssl" -> true
                    | "tcp" -> false
                    | _ -> failwithf "Got new unexpected port type: %s" portType
                let privatePort =
                    if encrypted then
                        Some port
                    else
                        None
                let unencryptedPort =
                    if encrypted then
                        None
                    else
                        Some port

                yield
                    {
                        Fqdn = fqdn
                        Pruning = None
                        PrivatePort = privatePort
                        UnencryptedPort = unencryptedPort
                        Version = None
                    }
        }

    let private FilterCompatibleServer (electrumServer: ElectrumServer) =
        try
            electrumServer.CheckCompatibility()
            true
        with
        | :? NotSupportedException -> false

    let ExtractServerList () =
        ExtractServerListFromWebPage ()
            |> Seq.filter FilterCompatibleServer
