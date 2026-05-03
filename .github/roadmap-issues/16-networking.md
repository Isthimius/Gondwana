---
title: "feat: Gondwana.Networking — client/server message loop and lobby primitives"
---
## Summary
This is listed in the README roadmap as _"Initial client/server networking support."_ Both FlatRedBall and GameMaker provide multiplayer/network primitives. This issue tracks creating a `Gondwana.Networking` package as the starting point for networked games.

## Non-Goals (v1)
- No relay/matchmaking server infrastructure
- No authoritative server physics (client-side prediction is the game's responsibility)
- No cloud saves or leaderboards

## Scope of Work

### Core Transport
```csharp
public interface IGameTransport
{
    Task ConnectAsync(string host, int port, CancellationToken ct);
    Task SendAsync(GameMessage msg, CancellationToken ct);
    IAsyncEnumerable<GameMessage> ReceiveAsync(CancellationToken ct);
    void Disconnect();
}
```
Implementations: `TcpGameTransport` (reliable, ordered), `UdpGameTransport` (unreliable, unordered).

### Messaging
- `GameMessage` — typed byte-array envelope with a `ushort MessageType` header
- `MessageRouter` — dispatches incoming messages to handlers registered by type ID

### Lobby / Room
- `LobbyHost` — creates a TCP listener, manages peer connections, broadcasts events
- `LobbyClient` — connects to a host, sends/receives lobby events
- `GameLobby` — tracks connected peers and their metadata (name, ready state)
- Events: `PlayerJoined`, `PlayerLeft`, `LobbyReady`

### Engine Integration
Received messages arrive on a background thread; they must be queued and dispatched on the engine cycle thread:
```csharp
// In network receive callback:
engine.Dispatcher.InvokeOnCycle(() => messageRouter.Dispatch(msg));
```

## Acceptance Criteria
- [ ] Two instances of a game can exchange `GameMessage`s over localhost TCP
- [ ] `LobbyHost` broadcasts `PlayerJoined` to all existing clients when a new one connects
- [ ] Messages enqueued from the receive thread are dispatched safely within the engine cycle
- [ ] A minimal two-player demo (e.g., synchronized sprite positions) works over localhost

## Key Files / References
- README roadmap entry: _"Initial client/server networking support"_
- `Gondwana/EngineDispatcher.cs`
- `Gondwana/IEngineDispatcher.cs`
