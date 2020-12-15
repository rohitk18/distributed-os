open System.Collections.Generic

type User(id: int, un: string) =
    member this.ID: int = id
    member this.Username: string = un
    new() = User(-1, "")

type Hashtag(id: int, hashtag: string) =
    member this.ID: int = id
    member this.Value: string = hashtag

type Tweet(id: int, t: string,h:Hashtag list, m:User list) =
    member this.ID: int = id
    member this.Value: string = t
    member this.Hashtags: Hashtag list = h
    member this.Mentions: User list = m
    new(id,t) = Tweet(id,t,[],[])

type TweetMessage =
    struct
        val tweet:Tweet
        val user:User
        val retweet:bool
        val mention:bool
        new(t:Tweet,u:User,r:bool,m:bool) = {tweet=t;user=u;retweet=r;mention=m}
    end

type ServerRequest =
    | LoginUser of username: string
    | LogoutUser of user:User * timeElapsed:int
    | RegisterUser of username:string
    | SubscribeUser of cuser: User * suser: User
    | TweetRequest of cuser: User * tweet: string * hashtags: string list * mentions: string list * subscribers: List<User>
    | RetweetRequest of cuser: User * tweet: Tweet * subscribers:List<User>
    | SimulatorStats

type ServerResponse =
    | StartSimulation
    | EndSimulation
    | UserLogged of user: User * subscribers: List<User> * tweetMessages: List<TweetMessage>
    | UserNotFound
    | UserLoggedOut
    | NotAuthorised
    | UserRegistered of user:User
    | UserExists of user:User
    | UserSubscribed
    | SubscriberNotFound
    | AlreadySubscribed
    | TweetSent
    | TweetUpdate of tweet:Tweet * user:User * retweet:bool
    | TweetChecker
    | TweetTrigger
    | StartRegisterSim

// json types for serialization
type JUser = {
    id: int
    username: string
}
type JHashtag = {
    id: int
    value: string
}