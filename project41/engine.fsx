#load "Bootstrap.fsx"
#load "typedef.fsx"
#load "database.fsx"

// open System.Diagnostics
open System
// open System.Linq;
open System.Collections.Generic
open System.Data
open Akka.FSharp
open Akka.Actor
open Typedef
open Database

let rs = ActorSystem.Create("Twitter")

let db = Database.db
Database.makeDatabase ()

let getTweetFromDB(id) =
    let ttable = db.Tables.["Tweets"]
    let utable = db.Tables.["Users"]
    let htable = db.Tables.["Hashtags"]
    let httable = db.Tables.["Hashtag-Tweet"]
    let mtable = db.Tables.["Mentions"]
    let exp = "id = '"+string(id)+"'"
    let rows = ttable.Select(exp)
    // if rows.Length > 0 then  // run function only when tweet is available; so before running function, use select function to check whether data exists or not
    let row = rows.[0]
    let hashtags = httable.Select("tweetid = '"+string(id)+"'") 
                    |> Array.toList
                    |> List.map(fun htrow ->
                        let hrow = htable.Select("id = '" + string(htrow.["hashtagid"]) + "'").[0]
                        Hashtag(int(string(hrow.["id"])),string(hrow.["hashtag"]))
                        )
    let mentions = mtable.Select("tweetid = '"+string(id)+"'")
                    |> Array.toList
                    |> List.map(fun mrow ->
                        let urow = utable.Select("id = '" + string(mrow.["userid"]) + "'").[0]
                        User(int(string(urow.["id"])),string(urow.["username"]))
                    )
    Tweet(id,string(row.["tweet"]),hashtags,mentions)

let collectTweets (uid:int) =
    let exp = "subscriberid = '"+string(uid)+"'"
    let sids = db.Tables.["Subscribers"].Select(exp) |> Array.map(fun row -> row.["userid"]) |> Array.toList
    let utable = db.Tables.["Users"]
    let ttable = db.Tables.["Tweets"]
    let rtable = db.Tables.["Retweets"]
    let mtable = db.Tables.["Mentions"]
    let tids = HashSet<int>()
    let rids = HashSet<int>()
    let tweetmsglist:List<TweetMessage> = List<TweetMessage>()
    for sid in sids do
        let exp = "id = '"+string(sid)+"'"
        let dataRows = utable.Select(exp)
        let suser:User = User(int(string(dataRows.[0].["id"])),string(dataRows.[0].["username"]))
        let exp = "userid = '"+string(sid)+"'"
        let dataRows = ttable.Select(exp)
        for row in dataRows do
            tids.Add(int(string(row.["id"]))) |> ignore
            let tweet:Tweet = getTweetFromDB(int(string(row.["id"])))
            let mentions = tweet.Mentions |> List.map (fun m -> m.ID)
            let mentioned:bool = List.contains uid mentions
            let tmsg:TweetMessage = TweetMessage(tweet,suser,false,mentioned)
            tweetmsglist.Add(tmsg)
        let dataRows = rtable.Select(exp)
        for row in dataRows do
            rids.Add(int(string(row.["tweetid"]))) |> ignore
            let tweet:Tweet = getTweetFromDB(int(string(row.["tweetid"])))
            let mentions = tweet.Mentions |> List.map (fun m -> m.ID)
            let mentioned:bool = List.contains uid mentions
            let tmsg:TweetMessage = TweetMessage(tweet,suser,true,mentioned)
            tweetmsglist.Add(tmsg)
    let exp = "userid = '"+string(uid)+"'"
    let mrows = mtable.Select(exp)
    for mrow in mrows do
        let tid = int(string(mrow.["tweetid"]))
        if tids.Contains(tid) = false then
        //     for t in tweetmsglist do
        //         if t.tweet.ID = tid then t.mention <- true
        // else
            let tweet:Tweet = getTweetFromDB(tid)
            let usid = int(string(ttable.Select("id = '"+string(tweet.ID)+"'").[0].["userid"]))
            let urow = utable.Select("id = '"+string(usid)+"'").[0]
            let u = User(int(string(urow.["id"])),string(urow.["username"]))
            let tmsg:TweetMessage = TweetMessage(tweet,u,false,true)
            tweetmsglist.Add(tmsg)
    tweetmsglist

let engine (mailbox: Actor<_>) =
    let mutable loggedInUsers = new Dictionary<int,IActorRef>()

    let rec loop () =
        actor {
            let! message = mailbox.Receive()

            match message with
            | LoginUser (username) ->
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
                    // let tweets = new List<TweetMessage>()
                    let subtable = db.Tables.["Subscribers"]
                    let subscribers = new List<User>()
                    let subRows = subtable.Select("userid = '"+string(user.ID)+"'")
                    for subRow in subRows do
                        let dataRows = db.Tables.["Users"].Select("id = '"+string(subRow.["subscriberid"])+"'")
                        if dataRows.Length > 0 then
                            let sub = User(int(string(dataRows.[0].["id"])),string(dataRows.[0].["username"]))
                            subscribers.Add(sub)                        
                    // let tweets = collectTweets(user.ID)
                    loggedInUsers.Add(id,mailbox.Sender()) |> ignore
                    let tweetList = collectTweets(id)
                    printfn "%A: Logged in. Got %A tweets." username tweetList.Count
                    mailbox.Sender() <! UserLogged(user,subscribers,tweetList)
            | LogoutUser (u,t) ->
                if loggedInUsers.ContainsKey(u.ID) then
                    loggedInUsers.Remove(u.ID) |> ignore
                    printfn "%A: Logged Out. Log Session time is %A milliseconds" u.Username t
                    mailbox.Sender() <! UserLoggedOut
            // send list of subscribers, tweets, retweets
            | SubscribeUser (c, s) ->
                if loggedInUsers.ContainsKey(c.ID) then
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
                // else
                //     mailbox.Sender() <! NotAuthorised
            | TweetRequest (c, tweet, hashtags, mentions, subs) ->
                // printfn "working %A %A" c.Username <| loggedInUsers.Contains(c.Username)
                if loggedInUsers.ContainsKey(c.ID) then
                    let table = db.Tables.["Tweets"]
                    let tweetrow = table.NewRow()
                    tweetrow.["tweet"] <- tweet
                    tweetrow.["userid"] <- c.ID
                    table.Rows.Add tweetrow
                    // printfn "tid:%A tweet:%A" tweetrow.["id"] tweetrow.["tweet"]
                    let mutable tweetHashtags = []
                    let mutable tweetMentions = []
                    let htable = db.Tables.["Hashtags"]
                    let httable = db.Tables.["Hashtag-Tweet"]
                    for hashtag in hashtags do
                        let exp = "hashtag = '"+hashtag+"'"
                        let dataRows = htable.Select(exp)
                        if dataRows.Length = 0 then
                            let row = htable.NewRow()
                            row.["hashtag"] <- hashtag
                            htable.Rows.Add row
                        let dataRows = htable.Select(exp)
                        let h = Hashtag(int(string(dataRows.[0].["id"])),string(dataRows.[0].["hashtag"]))
                        tweetHashtags <- h :: tweetHashtags
                        let row = httable.NewRow()
                        row.["hashtagid"] <- h.ID
                        row.["tweetid"] <- tweetrow.["id"]
                    let mtable = db.Tables.["Mentions"]
                    for user in mentions do
                        let exp = "username = '"+user+"'"
                        let utable = db.Tables.["Users"]
                        let dataRows = utable.Select(exp)
                        if dataRows.Length <> 0 then
                            let row = mtable.NewRow()
                            let u = User(int(string(dataRows.[0].["id"])),string(dataRows.[0].["username"]))
                            tweetMentions <- u :: tweetMentions
                            row.["tweetid"] <- tweetrow.["id"]
                            row.["userid"] <- u.ID
                            mtable.Rows.Add row
                    let tweetObject = Tweet(int(string(tweetrow.["id"])),string(tweet),tweetHashtags,tweetMentions)
                    // let sids = db.Tables.["Subscribers"].Select("userid = '"+string(c.ID)+"'") |> Array.map(fun row -> int(string(row.["userid"]))) |> Array.toList
                    // for sid in sids do
                        // if loggedInUsers.ContainsKey(sid) then
                            // loggedInUsers.[sid] <! TweetUpdate(tweetObject,c,false)
                    let mutable i = 0
                    for sub in subs do
                        if loggedInUsers.ContainsKey(sub.ID) then
                            loggedInUsers.[sub.ID] <! TweetUpdate(tweetObject,c,false)
                            i <- i + 1
                    printfn "%A: Tweet sent to %A subscribers who are logged in currently" c.Username i
                    mailbox.Sender() <! TweetSent
                // else
                //     mailbox.Sender() <! NotAuthorised
            | RetweetRequest (c, tweet,subs) ->
                if loggedInUsers.ContainsKey(c.ID) then
                    // let num = db.Tables.["Tweets"].Rows.Count
                    let table = db.Tables.["Retweets"]
                    let row = table.NewRow()
                    row.["tweetid"] <- tweet.ID
                    row.["userid"] <- c.ID
                    table.Rows.Add row
                    // let sids = db.Tables.["Subscribers"].Select("userid = '"+string(c.ID)+"'") |> Array.map(fun row -> int(string(row.["userid"]))) |> Array.toList
                    // for sid in sids do
                    //     if loggedInUsers.ContainsKey(sid) then
                    //         loggedInUsers.[sid] <! TweetUpdate(tweet,c,true)
                    let mutable i = 0
                    for sub in subs do
                        if loggedInUsers.ContainsKey(sub.ID) then
                            loggedInUsers.[sub.ID] <! TweetUpdate(tweet,c,true)
                            i <- i + 1
                    printfn "%A: Retweet sent to %A subscribers who are logged in currently" c.Username i
                    mailbox.Sender() <! TweetSent
                // else
                //     mailbox.Sender() <! NotAuthorised
            | _ -> printfn "request unknown"

            return! loop ()
        }
    loop ()
(*
let server = spawn rs "server" engine

let mutable luser: User = new User()

let reqList: ServerRequest list =
    [ LoginUser("user100")
      Tweet(luser, "This is a demo tweet", [], []) ]

for call in reqList do
    let req: Async<ServerResponse> = server <? call
    printfn "call"
    let res = Async.RunSynchronously req
    match res with
    | UserLogged (user) ->
        printfn "id:%A, username:%A" user.ID user.Username
        luser <- user
        printfn "%A" luser.Username
    | TweetSent -> printfn "%A" res
    | _ -> printfn "%A" res
    Threading.Thread.Sleep 100
// let req1:Async<ServerResponse> = server <? Tweet(luser,"This is a demo tweet",[],[])
// let res1 = Async.RunSynchronously req1
// match res1 with
// | TweetSent -> printfn "%A" res
// | _ -> printfn "%A" res

let mutable serverStop = true
while serverStop do
    Threading.Thread.Sleep 1000
Threading.Thread.Sleep 1000

*)