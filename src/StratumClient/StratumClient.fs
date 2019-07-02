namespace TcpEcho

open System

open System
open Newtonsoft.Json

type BlockchainTransactionGet = {
    [<JsonProperty(PropertyName = "id")>]
    Id: int
    [<JsonProperty(PropertyName = "result")>]
    Result: string
}

type BlockchainEstimateFee = {
    [<JsonProperty(PropertyName = "id")>]
    Id: int
    [<JsonProperty(PropertyName = "result")>]
    Result: double
}

type Request = {
    [<JsonProperty(PropertyName = "id")>]
    Id: int
    [<JsonProperty(PropertyName = "method")>]
    Method: string
    [<JsonProperty(PropertyName = "params")>]
    Params: seq<obj>
}

type StratumClient(endpoint: string, port: int) =
    inherit JsonRpcClient(endpoint, port)

    static member Deserialize<'T> json: 'T =
        if String.IsNullOrWhiteSpace(json) then failwith "Invalid json format, the server response was empty"
        try
            JsonConvert.DeserializeObject<'T> json
        with
        | ex ->
            failwithf "Problem deserializing <<<<<<<<%s>>>>>>>>" json

    member this.Call(json: string) = async {
        let! result = json |> this.CallAsync |> Async.Catch
        match result with
        | Choice1Of2 x -> return x
        | Choice2Of2 ex ->
            match ex with
            // The data may be incomplete, corrupted, or just fine so lets try parse it anyways
            | IncompleteResponseException response -> return response
            | ex -> return raise ex
    }

    member this.BlockchainTransactionGet(id: int, transactionId: string) = Async.StartAsTask(async {
        let request: Request = {
            Id = id
            Method = "blockchain.transaction.get"
            Params = [transactionId]
        }
        let json = JsonConvert.SerializeObject(request)
        let! returnedJson = this.Call json
        let obj = StratumClient.Deserialize<BlockchainTransactionGet> returnedJson
        return obj
    })
    member this.BlockchainEstimateFee(id: int, blocks: int) = Async.StartAsTask(async {
        let request: Request = {
            Id = id
            Method = "blockchain.estimatefee"
            Params = [blocks]
        }
        let json = JsonConvert.SerializeObject(request)
        let! returnedJson = this.Call json
        let obj = StratumClient.Deserialize<BlockchainEstimateFee> returnedJson
        return obj
    })

