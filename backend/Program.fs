open System
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Hosting
open Microsoft.Extensions.Hosting
open Microsoft.Extensions.DependencyInjection
open Giraffe
open Microsoft.AspNetCore.Cors.Infrastructure
open System.Threading.Tasks
open Microsoft.AspNetCore.Http

open Handlers

let configureCors (builder : CorsPolicyBuilder) =
    builder
        .WithOrigins("http://127.0.0.1:8000", "http://localhost:8000")
        .AllowAnyMethod()
        .AllowAnyHeader()
        .AllowCredentials()
    |> ignore

let configureServices (services : IServiceCollection) =
    services.AddCors()    |> ignore
    services.AddGiraffe() |> ignore

let webApp: HttpHandler =
    choose [
        POST >=> choose [
            route "/login" >=> loginHandler
            route "/user/add" >=> addUserHandler
            route "/firm/add" >=> addFirmHandler
            route "/tos/publish" >=> publishTosHandler
            route "/tos/accept" >=> acceptTosHandler
        ]
        GET >=> choose [
            route "/users" >=> listUsersHandler
            route "/firms" >=> listFirmsHandler
            routef "/users/%s" getLawyerHandler
        ]
    ]

let configureApp (app : IApplicationBuilder) =
    app.Use(fun (ctx : HttpContext) (next : RequestDelegate) ->
        task {
            ctx.Items["connectionString"] <- "tos.db"
            return! next.Invoke(ctx)
        } :> Task)
    |> ignore

    app.UseCors(configureCors)
       .UseGiraffe webApp

[<EntryPoint>]
let main _ =
    Database.initDb "tos.db"

    let port = Environment.GetEnvironmentVariable "PORT" |> Option.ofObj |> Option.map int |> Option.defaultValue 5000
    let host = Environment.GetEnvironmentVariable "HOST" |> Option.ofObj |> Option.defaultValue "127.0.0.1"

    Host.CreateDefaultBuilder()
        .ConfigureWebHostDefaults(fun webHostBuilder ->
            webHostBuilder
                .UseKestrel(fun opts -> opts.Listen(System.Net.IPAddress.Parse(host), port))
                .Configure(configureApp)
                .ConfigureServices(configureServices)
                |> ignore
        )
        .Build()
        .Run()
    0
