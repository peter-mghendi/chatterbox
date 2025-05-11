module Chatterbox.Data.Context

open Npgsql
open SqlHydra.Query
open Chatterbox.Data

let datasource =
    let builder = NpgsqlDataSourceBuilder("Server=127.0.0.1;Port=5434;Database=chatterbox_db;User Id=postgres;Password=password;")
    builder.MapEnum<``public``.subscription_status>("public.subscription_status") |> ignore   
    builder.Build()
    
let openContext()=
    let compiler = SqlKata.Compilers.PostgresCompiler()
    let connection = datasource.OpenConnection()
    let ctx = new QueryContext(connection, compiler)
    #if DEBUG
    ctx.Logger <- printfn "SQL: %O" 
    #endif
    ctx