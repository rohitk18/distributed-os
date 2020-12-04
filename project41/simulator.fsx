#load "Bootstrap.fsx"
#load "typedef.fsx"
#load "engine.fsx"

open System
open System.Diagnostics
open System.Collections.Generic
open System.IO
open Akka.Actor
open Akka.FSharp
open Typedef
open Engine

#time
let mutable serverWork = true
let numUsers = int(fsi.CommandLineArgs.[1])
if numUsers < 100 then
    printfn "Please enter value greater than 100"
    Environment.Exit 0
// let numUsers = 1000
let mutable usersLoggedout = numUsers + 1
printfn "Preparing twitter database"
Engine.twitsetdb(numUsers)
printfn "Database ready"
let rand = Random()
let randBool () = 
    let r = rand.NextDouble()
    if r >= 0.6 then true else false    // retweet or tweet

let sampleTweets = File.ReadAllLines "quotes.txt"
let generateTweet () =
    let numHashtags = rand.Next(5)
    let numMentions = rand.Next(2)
    let hashtags:string list = [1..numHashtags] |> List.map (fun i-> "hashtag"+string(rand.Next(1,101)))
    let mentions:string list = [1..numMentions] |> List.map (fun i-> "user"+string(rand.Next(1,numUsers + 1)))
    let tweet:string = sampleTweets.[rand.Next(sampleTweets.Length)]
    [[tweet];hashtags;mentions]
    
let client server counterActor userString n (mailbox:Actor<_>) =
    let mutable user = User()
    let mutable maxnum = 10 * int(Math.Round((float(numUsers-n))/float(numUsers),0))
    let mutable tweets = new List<TweetMessage>()
    let mutable subscribers = new List<User>()
    let logstopwatch = Stopwatch()
    let idlestopwatch = Stopwatch()

    let rec loop () =
        actor{
            let! message = mailbox.Receive()
            match message with
            | StartSimulation ->
                server <! LoginUser(userString)
            | UserLogged(u,subs,tweetsList) ->
                user <- u
                subscribers <- subs
                tweets <- tweetsList
                logstopwatch.Start()
                idlestopwatch.Start()
                let tweetDetails = generateTweet()
                server <! TweetRequest(user, tweetDetails.[0].[0],tweetDetails.[1],tweetDetails.[2],subscribers)
                maxnum <- maxnum - 1
            | TweetUpdate (tweet,user,retweet) ->
                let mutable mentioned = false
                for mention in tweet.Mentions do
                    if mention.ID = user.ID then mentioned <- true
                tweets.Add(TweetMessage(tweet,user,retweet,mentioned))
                idlestopwatch.Restart()
            | TweetSent -> ()
            | UserLoggedOut ->
                user <- User()
                tweets.Clear()
                counterActor <! UserLoggedOut
            | TweetChecker ->
                if user.ID <> -1 then
                    idlestopwatch.Stop()
                    if idlestopwatch.ElapsedMilliseconds > 30000L then 
                        logstopwatch.Stop()
                        server <! LogoutUser(user,int(logstopwatch.ElapsedMilliseconds))
                    else
                        idlestopwatch.Start()
            | TweetTrigger ->
                if user.ID <> -1 && maxnum > 0 then
                    let tweetDetails = generateTweet()
                    if tweets.Count = 0 then
                        server <! TweetRequest(user, tweetDetails.[0].[0],tweetDetails.[1],tweetDetails.[2],subscribers)
                    else
                        if randBool() then server <! RetweetRequest(user,tweets.[rand.Next(tweets.Count)].tweet,subscribers)
                        else server <! TweetRequest(user, tweetDetails.[0].[0],tweetDetails.[1],tweetDetails.[2],subscribers)
                    maxnum <- maxnum - 1
            | StartRegisterSim ->
                logstopwatch.Start()
                server <! RegisterUser(userString)
            | UserRegistered(u) ->
                user <- u
                // for simulation only
                let subs = [User(1,"user1");User(2,"user2");User(3,"user3");User(4,"user4");User(5,"user5");]
                for sub in subs do
                    server <! SubscribeUser(user,sub)
                logstopwatch.Stop()
                server <! LogoutUser(user,int(logstopwatch.ElapsedMilliseconds))
            | _-> printf "Unmatched response %A\n" message
            return! loop ()
        }
    loop()

let counter (mailbox:Actor<_>) = 
    let rec loop () =
        actor {
            let! message = mailbox.Receive()
            match message with
            | UserLoggedOut -> 
                usersLoggedout <- usersLoggedout - 1
            | _ -> printf "Unmatched response %A\n" message
            return! loop ()
        }
    loop()

let server = spawn rs "server" engine
let simcounter = spawn rs "counter" counter

let clients = 
    [1 .. numUsers]
    |> List.map(fun i ->
        let st = "client"+ string(i)
        let ust = "user" + string(i)
        spawn rs st <| client server simcounter ust i)

let newuserid = numUsers + 1
let newuserClient = spawn rs "newclient" <| client server simcounter "newuser" newuserid
newuserClient <! StartRegisterSim

Threading.Thread.Sleep 1000

// simulation
for c in clients do
    c <! StartSimulation
    Threading.Thread.Sleep 10

while serverWork do
    serverWork <- usersLoggedout <> 0
    for c in clients do
        c <! TweetTrigger
        c <! TweetChecker
    Threading.Thread.Sleep 10

printfn "************************************************************************************"
#time
server <! SimulatorStats
Threading.Thread.Sleep 1000