# WintunSharp

A modern .NET wrapper for [wintun](https://www.wintun.net/), providing a clean managed API for creating and communicating with virtual network adapters on Windows.

## Features

- **Create & Open Adapters** — Create new wintun virtual network interfaces or open existing ones by name
- **Synchronous I/O** — Send and receive raw IP packets with `TryReceive()` and `Send()`
- **Asynchronous I/O** — Async packet streaming via `ChannelReader<byte[]>` with cancellation support
- **Event-Driven** — Exposes the wintun read wait event for efficient, non-blocking operation
- **Driver Management** — Query driver version and delete the wintun driver when needed
- **Null-Safe** — Full nullable reference type annotations

## Requirements

- **Windows 7 or later** (wintun is Windows-only)
- **.NET Standard 2.0+** (compatible with .NET Framework, .NET Core, and .NET 5+)
- **wintun.dll** must be available in the application directory or system PATH

> **Note:** wintunSharp does not bundle wintun.dll. Download it from [https://www.wintun.net/](https://www.wintun.net/) and place `wintun.dll` next to your executable or in a location on the system PATH.

## Quick Start

### Create an Adapter and Send a Packet

```csharp
using WintunSharp;

// Create a virtual network adapter
using var adapter = new WintunAdapter("MyTunnel", "ExampleTunnel");
Console.WriteLine($"Adapter LUID: {adapter.Luid:X16}");

// Start a session for packet I/O
using var session = adapter.StartSession();

// Send a raw IPv4 packet
byte[] packet = /* ... build your IP packet ... */;
session.Send(packet);
```

### Receive Packets (Synchronous)

```csharp
while (session.TryReceive(out var packet))
{
    Console.WriteLine($"Received {packet.Length} bytes");
    // Process packet...
}
```

### Receive Packets (Asynchronous)

```csharp
var (cts, reader) = session.ReceiveAsync(bufferSize: 16);

await foreach (var packet in reader.ReadAllAsync())
{
    Console.WriteLine($"Received {packet.Length} bytes");
    // Process packet...
}

// Stop the async reader when done
cts.Cancel();
```

### Open an Existing Adapter

```csharp
using var adapter = WintunAdapter.Open("MyTunnel");
```

### Query Driver Version

```csharp
int version = WintunAdapter.DriverVersion;
Console.WriteLine($"wintun driver version: 0x{version:X}");
```

## API Reference

### `WintunAdapter`

| Member | Description |
|--------|-------------|
| `WintunAdapter(name, tunnelType, requestedGuid?)` | Create a new virtual adapter |
| `WintunAdapter.Open(name)` | Open an existing adapter by name |
| `.StartSession(ringCapacity?)` | Start a packet I/O session (default 1 MiB ring) |
| `.Luid` | Kernel LUID identifying the adapter |
| `WintunAdapter.DriverVersion` | Currently installed wintun driver version |
| `WintunAdapter.DeleteDriver()` | Delete the wintun driver entirely |

### `WintunSession`

| Member | Description |
|--------|-------------|
| `.Send(packet)` | Send a raw IP packet (max 65535 bytes) |
| `.TryReceive(out packet)` | Try to receive the next packet from the ring buffer |
| `.ReceiveAsync(bufferSize?, linkedToken?)` | Start an async packet reader returning `(CancellationTokenSource, ChannelReader<byte[]>)` |
| `.ReadEvent` | OS event signaled when packets are available |

## Configuration

### Ring Buffer Capacity

The ring buffer capacity controls how much data can be queued for send/receive operations. Valid range: **128 KiB — 64 MiB** (default: 1 MiB).

```csharp
// Use a larger ring for high-throughput scenarios
using var session = adapter.StartSession(4 * 1024 * 1024); // 4 MiB
```

### Custom Adapter GUID

Optionally specify a device instance GUID for the network connection:

```csharp
var guid = new Guid("xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx");
using var adapter = new WintunAdapter("MyTunnel", "ExampleTunnel", guid);
```

## Example Project

See [`WintunSharp.Example/Program.cs`](WintunSharp.Example/Program.cs) for a complete demonstration covering adapter creation, packet send/receive, async channels, and cancellation.

Run it with:

```bash
dotnet run --project WintunSharp.Example
```

## Architecture

```
┌─────────────────┐   ┌──────────────────┐   ┌─────────────┐
│  WintunAdapter  │ > │  WintunSession   │ > │ wintun.dll  │
│  (adapter mgmt) │   │  (packet I/O)    │   │  (native)   │
└─────────────────┘   └──────────────────┘   └─────────────┘
                              │
                      ┌───────┴────────┐
                      │                │
               TryReceive()   ReceiveAsync()
               (sync)         (async channel)
```

## License

This project is provided as-is. Check the repository for license details.
