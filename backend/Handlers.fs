module Handlers

open Giraffe
open Microsoft.AspNetCore.Http
open System

let getDbConnectionString (ctx: HttpContext) =
    ctx.Items["connectionString"] :?> string

let loginHandler: HttpHandler =
    fun next ctx -> task {
        let! data = ctx.BindJsonAsync<{| username: string; password: string |}>()
        return! json {| token = "1234567890" |} next ctx
    }

let addUserHandler: HttpHandler =
    fun next ctx -> task {
        let! data = ctx.BindJsonAsync<{| email: string; name: string |}>()
        let id = Database.addUser (getDbConnectionString ctx) data.email data.name
        return! json {| id = id |} next ctx
    }

let publishTosHandler: HttpHandler =
    fun next ctx -> task {
        let! data = ctx.BindJsonAsync<{| version: string; content: string |}>()
        Database.publishTos (getDbConnectionString ctx) data.version data.content
        return! json {| status = "ok" |} next ctx
    }

let acceptTosHandler : HttpHandler =
    fun next ctx -> task {
        let! data = ctx.BindJsonAsync<{| userId: string; tosId: int|}>()
        let connStr = getDbConnectionString ctx

        match Guid.TryParse data.userId with
        | true, guid ->
            let ip =
                match ctx.Connection.RemoteIpAddress with
                | null -> "unknown"
                | addr -> addr.ToString()
            let ua = ctx.Request.Headers["User-Agent"].ToString()

            match Database.acceptTos connStr guid data.tosId ip ua with
            | Ok () ->
                return! json {| status = "accepted" |} next ctx

            | Error msg ->
                return! RequestErrors.badRequest (text msg) next ctx

        | false, _ ->
            return! RequestErrors.badRequest (text "Invalid GUID format") next ctx
    }

let listUsersHandler: HttpHandler =
    fun next ctx -> task {
        let result = Database.listUsers (getDbConnectionString ctx)
        return! json result next ctx
    }
