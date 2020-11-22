#load "Bootstrap.fsx"
#load "database.fsx"

// open System.Diagnostics
open System
// open System.Linq;
open System.Collections.Generic
open System.Data
open Akka.FSharp
open Akka.Actor
open Database

type User(id: int, un: string) =
    member this.ID: int = id
    member this.Username: string = un
    new() = User(-1, "")

type Hashtag(id: int, hashtag: string) =
    member this.ID: int = id
    member this.Value: string = hashtag

type Tweet(id: int, t: string) =
    member this.ID: int = id
    member this.Value: string = t
    member this.Hashtags: Hashtag list = []
    member this.Mentions: User list = []

type ServerRequest =
    | TestRequest
    | LoginUser of username: string
    | RegisterUser
    | SubscribeUser of cuser: User * suser: User
    | Tweet of cuser: User * tweet: string * hashtags: string list * mentions: string list
    | Retweet of cuser: User * tweet: Tweet

type ServerResponse =
    | UserLogged of user: User
    | UserNotFound
    | NotAuthorised
    | UserSubscribed
    | SubscriberNotFound
    | AlreadySubscribed
    | TweetSent

let rs = ActorSystem.Create("Twitter")

let db = Database.db
Database.makeDatabase ()

let engine (mailbox: Actor<_>) =
    let mutable loggedInUsers = new HashSet<string>()   

    let rec loop () =
        actor {
            let! message = mailbox.Receive()

            match message with
            | TestRequest -> printfn "Test Request successful"
            | LoginUser (username) ->
                printfn "LoginUser execute %A" username
                let table = db.Tables.["Users"]
                let expression = "username = '" + username + "'"
                let dataRows = table.Select(expression)
                if dataRows.Length = 0 then
                    mailbox.Sender() <! UserNotFound
                // printfn "%A" UserNotFound
                else
                    let id = Convert.ToInt32 dataRows.[0].["id"]

                    let un =
                        Convert.ToString dataRows.[0].["username"]

                    let user: User = User(id, un)
                    loggedInUsers.Add(username) |> ignore
                    mailbox.Sender() <! UserLogged(user)
            // send list of subscribers, tweets, retweets
            | SubscribeUser (c, s) ->
                if loggedInUsers.Contains(c.Username) then
                    let table = db.Tables.["Users"]
                    let exp = "id = '" + string (c.ID) + "'"
                    let dataRows = table.Select(exp)
                    if dataRows.Length = 0 then
                        mailbox.Sender() <! SubscriberNotFound
                    else
                        let table = db.Tables.["Subscribers"]

                        let exp =
                            "userid = '"
                            + string (c.ID)
                            + "' AND subscriberid = '"
                            + string (s.ID)
                            + "'"

                        let dataRows = table.Select(exp)
                        if dataRows.Length = 0 then
                            mailbox.Sender() <! AlreadySubscribed
                        else
                            let row = table.NewRow()
                            row.["userid"] <- c.ID
                            row.["subscriberid"] <- s.ID
                            table.Rows.Add(row)
                            mailbox.Sender() <! UserSubscribed
                else
                    mailbox.Sender() <! NotAuthorised
            | Tweet (c, tweet, hashtags, mentions) ->
                printfn "working %A" c.Username
                if loggedInUsers.Contains(c.Username) then
                    let table = db.Tables.["Tweets"]
                    let row = table.NewRow()
                    row.["tweet"] <- tweet
                    row.["userid"] <- c.ID
                    table.Rows.Add row
                    printfn "tid:%A tweet:%A" row.["id"] row.["tweet"]
                    // let htable = db.Tables.["Hashtags"]
                    // let httable = db.Tables.["Hashtag-Tweet"]
                    // for hashtag in tweet.Hashtags do
                    //     let exp = "hashtag = '"+hashtag+"'"
                    //     let dataRows = htable.Select(exp)
                    //     if dataRows.Length = 0 then
                    //         let row = htable.NewRow()
                    //         row.["hashtag"] <- hashtag
                    //         table.Rows.Add row
                    //     let dataRows = htable.Select(exp)
                    //     let h = Hashtag(dataRows.[0].["id"],dataRows.[0].["hashtag"])
                    //     let row = httable.NewRow()
                    //     row.["hashtagid"] <- h.ID
                    //     row.["tweetid"] <-
                    // let mtable = db.Tables.["Mentions"]
                    // for user in tweet.Mentions do
                    //     let exp = "username = '"+user+"'"

                    //     let row = mtable.NewRow()
                    //     row.["tweetid"] <- tweet.ID
                    //     row.["userid"] <- user.ID
                    //     mtable.Rows.Add row
                    mailbox.Sender() <! TweetSent
                else
                    mailbox.Sender() <! NotAuthorised
            | Retweet (c, tweet) ->
                if loggedInUsers.Contains(c.Username) then
                    // let num = db.Tables.["Tweets"].Rows.Count
                    let table = db.Tables.["Retweets"]
                    let row = table.NewRow()
                    row.["tweetid"] <- tweet.ID
                    row.["userid"] <- c.ID
                    table.Rows.Add row
                    mailbox.Sender() <! TweetSent
                else
                    mailbox.Sender() <! NotAuthorised
            | _ -> printfn "request unknown"

            return! loop ()
        }

    loop ()

// let server = spawn rs "server" engine
// printfn "%A" rs
// printfn "%A" server
// while engineStop do
//     Threading.Thread.Sleep 1000    
// rs.Shutdown()

(*let mutable luser: User = new User()

let reqList: ServerRequest list =
    [ LoginUser("user100")
      Tweet(luser, "This is a demo tweet", [], []) ]

for call in reqList do
    let req: Async<ServerResponse> = server <? call
    let res = Async.RunSynchronously req
    match res with
    | UserLogged (user) ->
        printfn "id:%A, username:%A" user.ID user.Username
        luser <- user
    | _ -> printfn "%A" res
// let req1:Async<ServerResponse> = server <? Tweet(luser,"This is a demo tweet",[],[])
// let res1 = Async.RunSynchronously req1
// match res1 with
// | TweetSent -> printfn "%A" res
// | _ -> printfn "%A" res
// Threading.Thread.Sleep 1000
*)
