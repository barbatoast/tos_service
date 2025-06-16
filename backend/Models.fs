module Models

open System

type User = { Id: Guid; Email: string; Name: string }
type TosVersion = { Id: int; Version: string; Content: string; PublishedAt: DateTime }
type TosAcceptance = {
    Id: int
    UserId: Guid
    TosId: int
    AcceptedAt: DateTime
    UserIp: string
    UserAgent: string
}
