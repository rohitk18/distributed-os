namespace TwitterAPI

open System
open Microsoft.AspNetCore
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Hosting
open Microsoft.AspNetCore.Http
open Microsoft.Extensions.Configuration
open Microsoft.Extensions.DependencyInjection
open Microsoft.Extensions.Hosting
open WebSharper.AspNetCore

type Website(config: IConfiguration) =
    inherit SiteletService<Model.EndPoint>()

    let corsAllowedOrigins =
        config.GetSection("allowedOrigins").AsEnumerable()
        |> Seq.map (fun kv -> kv.Value)
        |> List.ofSeq

    override val Sitelet = Site.Main corsAllowedOrigins
    // override val Sitelet = Site.Main

type Startup() =

    member this.ConfigureServices(services: IServiceCollection) =
        services.AddSitelet<Website>() |> ignore
        // services.AddSitelet(Site.Main) |> ignore
            // .AddAuthentication("WebSharper")
            // .AddCookie("WebSharper", fun options -> ())
        // |> ignore

    member this.Configure(app: IApplicationBuilder, env: IWebHostEnvironment) =
        if env.IsDevelopment() then app.UseDeveloperExceptionPage() |> ignore

        app.UseStaticFiles()
            .UseWebSharper()
            .Run(fun context ->
                context.Response.StatusCode <- 404
                context.Response.WriteAsync("Page not found"))

module Program =
    let BuildWebHost args =
        WebHost
            .CreateDefaultBuilder(args)
            .UseStartup<Startup>()
            .Build()

    [<EntryPoint>]
    let main args =
        BuildWebHost(args).Run()
        0
