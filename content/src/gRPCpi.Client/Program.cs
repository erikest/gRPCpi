using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Greet;
using Grpc.Core;
using System.Device.Gpio;
using System.Threading;

namespace gRPCpi
{
    public class Program
    {
        static async Task Main(string[] args)
        {
            // Include port of the gRPC server as an application argument
            var port = args.Length > 0 ? args[0] : "50051";

            //CHANGE THIS TO YOUR SERVER ADDRESS
            var serverIpAddress = "192.168.1.244";

            var channel = new Channel($"{serverIpAddress}:" + port, ChannelCredentials.Insecure);
            var client = new Greeter.GreeterClient(channel);

            var reply = await client.SayHelloAsync(new HelloRequest { Name = "GreeterClient" });

            Console.WriteLine("Greeting: " + reply.Message);

            await channel.ShutdownAsync();

            BlinkReply(reply);

            Console.WriteLine("Press any key to exit...");
            Console.ReadKey();
        }

        private static void BlinkReply(HelloReply reply)
        {
            using (GpioController controller = new GpioController(PinNumberingScheme.Board))
            {
                var pin = 40;
                controller.OpenPin(pin, PinMode.Output);

                try
                {
                    foreach (char c in reply.Message)
                    {
                        var delay = (int)c * 4; //Values roughly between 128 to 500
                        controller.Write(pin, PinValue.High);
                        Console.WriteLine($"LED ON for {c}, then hold {delay} ms");
                        Thread.Sleep(delay);
                        controller.Write(pin, PinValue.Low);
                        Console.WriteLine($"LED OFF");
                        Thread.Sleep(100); // 1/10th of a second off between each character's blip
                    }
                }
                finally
                {
                    if (controller.IsPinOpen(pin))
                        controller.ClosePin(pin);
                }
            }
        }
    }
}
