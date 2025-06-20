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

let tryParsePage (query: IQueryCollection) (key: string) : int =
    match query.TryGetValue(key) with
    | true, v ->
        match Int32.TryParse(v.ToString()) with
        | true, value -> value
        | false, _ -> 1
    | false, _ -> 1

let listUsersHandler : HttpHandler =
    fun next ctx -> task {
        let page = tryParsePage ctx.Request.Query "page"
        let pageSize = 10

        let connStr = getDbConnectionString ctx
        let users = Database.listUsers connStr page pageSize
        let total = Database.getUserCount connStr
        let totalPages = (total + pageSize - 1) / pageSize

        return! json {| users = users; totalPages = totalPages |} next ctx
    }

// Firms

let addFirmHandler: HttpHandler =
    fun next ctx -> task {
        let! data = ctx.BindJsonAsync<{| name: string |}>()
        let id = Database.addLawFirm (getDbConnectionString ctx) data.name
        return! json {| id = id |} next ctx
    }

let listFirmsHandler : HttpHandler =
    fun next ctx -> task {
        let page = tryParsePage ctx.Request.Query "page"
        let pageSize = 10
        let query = ctx.TryGetQueryStringValue("query") |> Option.defaultValue ""

        let connStr = getDbConnectionString ctx
        let firms = Database.listLawFirms connStr page pageSize query
        let total = Database.getLawFirmCount connStr query
        let totalPages = (total + pageSize - 1) / pageSize

        return! json {| firms = firms; totalPages = totalPages |} next ctx
    }

// Lawyer Detail

let getLawyerHandler (id: string) : HttpHandler =
    fun next ctx -> task {
        let connStr = getDbConnectionString ctx
        match Database.getLawyerById connStr id with
        | Some lawyer ->
            return! json lawyer next ctx
        | None ->
            ctx.SetStatusCode 404
            return! json {| error = "Lawyer not found" |} next ctx
    }
