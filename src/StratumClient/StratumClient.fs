namespace TcpEcho

open System.IO
open System.Text
open Newtonsoft.Json

type BlockchainTransactionGet = {
    [<JsonProperty(PropertyName = "id")>]
    mutable Id: int
    [<JsonProperty(PropertyName = "result")>]
    mutable Result: string
}

type StratumClient(endpoint: string, port: int) =
    inherit Client(endpoint, port)
    member this.Call(json: string) = base.Call(json)
    member this.BlockchainTransactionGet(id: int, transactionId: string) = Async.StartAsTask(async {
        let stringBuilder = new StringBuilder()
        let stringWriter = new StringWriter(stringBuilder)
        let jsonWriter = new JsonTextWriter(stringWriter)

        // Is there a more elegant way to do this?
        // In Rust you could derive(Serialize) on a type and have this code
        // generated. Being able to use the |> operator (or something similar)
        // would also be nice.
        jsonWriter.WriteStartObject()
        jsonWriter.WritePropertyName("id")
        jsonWriter.WriteValue(id)
        jsonWriter.WritePropertyName("method")
        jsonWriter.WriteValue("blockchain.transaction.get")
        jsonWriter.WritePropertyName("params")
        jsonWriter.WriteStartArray()
        jsonWriter.WriteValue(transactionId)
        jsonWriter.WriteEnd()
        jsonWriter.WriteEndObject()

        let json = stringBuilder.ToString()
        let! returnedJson = this.Call(json) |> Async.AwaitTask
        let obj = JsonConvert.DeserializeObject<BlockchainTransactionGet>(returnedJson)
        return obj
    })

