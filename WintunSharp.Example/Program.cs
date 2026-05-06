namespace WintunSharp.Example
{
    internal class Program
    {
        static async Task Main(string[] args)
        {
            Console.WriteLine("=== WintunWrapper Example ===");
            Console.WriteLine();

            // -------------------------------------------------------------------
            // 1. Query driver version (no adapter needed)
            // -------------------------------------------------------------------
            Console.WriteLine($"WinTun driver version: 0x{WintunAdapter.DriverVersion:X}");
            Console.WriteLine();

            // -------------------------------------------------------------------
            // 2. Create an adapter
            // -------------------------------------------------------------------
            string name = $"BijouNet-Example";
            Console.WriteLine($"Creating adapter \"{name}\"...");
            using var adapter = new WintunAdapter(name, "ExampleTunnel");
            Console.WriteLine($"  LUID: {adapter.Luid:X16}");
            Console.WriteLine();

            // -------------------------------------------------------------------
            // 3. Start a session
            // -------------------------------------------------------------------
            Console.WriteLine("Starting session (1 MiB ring)...");
            using var session = adapter.StartSession(1 * 1024 * 1024);
            Console.WriteLine($"  Read event available: {session.ReadEvent is not null}");
            Console.WriteLine();

            // -------------------------------------------------------------------
            // 4. Send a packet (synchronous)
            // -------------------------------------------------------------------
            Console.WriteLine("Sending example IPv4 packet...");
            byte[] ipv4Packet = new byte[20];
            ipv4Packet[0] = 0x45; // Version 4, IHL 5
            ipv4Packet[4] = 1;    // Total length high byte (20)
            ipv4Packet[5] = 20;   // Total length low byte
            session.Send(ipv4Packet);
            Console.WriteLine("  Packet sent successfully");
            Console.WriteLine();

            // -------------------------------------------------------------------
            // 5. TryReceive on empty ring (no incoming traffic expected)
            // -------------------------------------------------------------------
            Console.WriteLine("Checking for incoming packets...");
            if (session.TryReceive(out var packet))
                Console.WriteLine($"  Received {packet!.Length} bytes");
            else
                Console.WriteLine("  No packets available (expected — no sender connected)");
            Console.WriteLine();

            // -------------------------------------------------------------------
            // 6. Async receive with cancellation
            // -------------------------------------------------------------------
            Console.WriteLine("Starting async receiver (will cancel after 2 seconds)...");
            var (cts, reader) = session.ReceiveAsync(bufferSize: 8);

            await Task.Delay(2000);
            cts.Cancel();
            Console.WriteLine("  Cancellation requested — waiting for channel to complete...");

            try
            {
                await foreach (var pkt in reader.ReadAllAsync())
                    Console.WriteLine($"  Received {pkt.Length} bytes via async channel");
            }
            catch (OperationCanceledException)
            {
                // Normal exit when cancellation fires during iteration
            }

            Console.WriteLine("  Async receiver completed cleanly");
            Console.WriteLine();

            // -------------------------------------------------------------------
            // 7. Open the same adapter by name (demonstrates shared handle)
            // -------------------------------------------------------------------
            Console.WriteLine($"Opening existing adapter \"{name}\"...");
            using var opened = WintunAdapter.Open(name);
            Console.WriteLine($"  LUID matches: {opened.Luid == adapter.Luid}");
            Console.WriteLine();

            // -------------------------------------------------------------------
            // 8. Constants reference
            // -------------------------------------------------------------------
            Console.WriteLine("Constants:");
            Console.WriteLine($"  MaxPacketSize:     {WintunSession.MaxPacketSize,6} (0x{WintunSession.MaxPacketSize:X}) bytes");
            Console.WriteLine($"  MinRingCapacity:   {128 * 1024,6} (0x20000) bytes");
            Console.WriteLine($"  MaxRingCapacity:   {64 * 1024 * 1024,6} (0x4000000) bytes");
            Console.WriteLine();

            // Adapter disposed automatically via using statement above
            Console.WriteLine("Adapter disposed. Example complete.");

        }
    }
}
