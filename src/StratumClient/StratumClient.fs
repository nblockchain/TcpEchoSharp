namespace TcpEcho

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
    inherit Client(endpoint, port)
    member this.Call(json: string) = base.Call(json)
    member this.BlockchainTransactionGet(id: int, transactionId: string) = Async.StartAsTask(async {
        let request: Request = {
            Id = id
            Method = "blockchain.transaction.get"
            Params = [transactionId]
        }
        let json = JsonConvert.SerializeObject(request)
        let! returnedJson = this.Call json |> Async.AwaitTask
        let obj = JsonConvert.DeserializeObject<BlockchainTransactionGet> returnedJson
        return obj
    })
    member this.BlockchainEstimateFee(id: int, blocks: int) = Async.StartAsTask(async {
        let request: Request = {
            Id = id
            Method = "blockchain.estimatefee"
            Params = [blocks]
        }
        let json = JsonConvert.SerializeObject(request)
        let! returnedJson = this.Call json |> Async.AwaitTask
        let obj = JsonConvert.DeserializeObject<BlockchainEstimateFee> returnedJson
        return obj
    })

