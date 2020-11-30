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

let mutable serverWork = true
let mutable usersLoggedout = 1000
let rand = Random()
let randBool () = 
    let r = rand.NextDouble()
    if r >= 0.6 then true else false    // retweet or tweet
// random.Next(tweets.length) to select random tweet for retweet

let sampleTweets = File.ReadAllLines "quotes.txt"
let generateTweet () =
    let numHashtags = rand.Next(5)
    let numMentions = rand.Next(2)
    let hashtags:string list = [1..numHashtags] |> List.map (fun i-> "hashtag"+string(rand.Next(1,101)))
    let mentions:string list = [1..numMentions] |> List.map (fun i-> "user"+string(rand.Next(1,1001)))
    let tweet:string = sampleTweets.[rand.Next(sampleTweets.Length)]
    [[tweet];hashtags;mentions]
    

let client server userString n (mailbox:Actor<_>) =
    let mutable user = User()
    let mutable maxnum = 10 * int(Math.Round((float(1000-n))/1000.0,0))
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
                // while maxnum > 0 do
                //     let tweetDetails = generateTweet()
                //     if tweets.Count = 0 then
                //         server <! TweetRequest(user, tweetDetails.[0].[0],tweetDetails.[1],tweetDetails.[2],subscribers)
                //     else
                //         if randBool() then server <! RetweetRequest(user,tweets.[rand.Next(tweets.Count)].tweet,subscribers)
                //         else server <! TweetRequest(user, tweetDetails.[0].[0],tweetDetails.[1],tweetDetails.[2],subscribers)
                //     maxnum <- maxnum - 1
                //     Threading.Thread.Sleep 100
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
                usersLoggedout <- usersLoggedout - 1
            | TweetChecker ->
                if user.ID <> -1 then
                    idlestopwatch.Stop()
                    if idlestopwatch.ElapsedMilliseconds > 30000L then 
                        logstopwatch.Stop()
                        server <! LogoutUser(user,logstopwatch.ElapsedMilliseconds)
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
                    
            | _-> printf "Unmatched response %A\n" message
            return! loop ()
        }
    loop()

let server = spawn rs "server" engine
// (*
// 1000 users
let clients = 
    [1 .. 1000]
    |> List.map(fun i ->
        let st = "client"+ string(i)
        let ust = "user" + string(i)
        spawn rs st <| client server ust i)

Threading.Thread.Sleep 1000

// simulation
// for i in [0..9] do
//     clients.[i] <! StartSimulation
//     Threading.Thread.Sleep 100
for c in clients do
    c <! StartSimulation
    Threading.Thread.Sleep 10

while serverWork do
    serverWork <- usersLoggedout <> 0
    // for i in [0..9] do
    //     clients.[i] <! TweetTrigger
    //     clients.[i] <! TweetChecker
    for c in clients do
        c <! TweetTrigger
        c <! TweetChecker
    Threading.Thread.Sleep 10

// *)