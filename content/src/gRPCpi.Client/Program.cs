using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Greet;
using Grpc.Core;
using System.Device.Gpio;
using System.Threading;
using System.Runtime.InteropServices;

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

            PulseReply(reply);

            Console.WriteLine("Press any key to exit...");
            Console.ReadKey();
        }

        static IGpioController GetController()
        {
            if (RuntimeInformation.OSArchitecture == Architecture.Arm) //Assume we're on the pi
                return new GpioController(PinNumberingScheme.Board);
            else
                return new ConsoleGPIOController();
        }

        /// <summary>
        /// Pulses the light a number of times corresponding to the character's alphabet index
        /// </summary>
        /// <param name="reply"></param>
        private static void PulseReply(HelloReply reply)
        {
            using (IGpioController controller = GetController())
            {
                var onTime = 100;
                var offTime = 50;
                var pin = 40;
                controller.OpenPin(pin, PinMode.Output);

                try
                {
                    foreach (char c in reply.Message)
                    {
                        var pulseCount = Math.Max(c.ToString().ToUpper()[0] - 64, 0);
                        Console.Write(c);
                        for (int i=0;i<pulseCount;i++)
                        {
                            controller.Write(pin, PinValue.High);
                            Console.Write(".");
                            Thread.Sleep(onTime);
                            controller.Write(pin, PinValue.Low);
                            Thread.Sleep(offTime);
                        }
                        Thread.Sleep(500);
                        Console.WriteLine();

                        }
                    }
                finally
                {
                    controller.ClosePin(pin);
                }
            }
        }
    }

    public class ConsoleGPIOController : IGpioController
    {

        List<(int pinNumber, PinValue value, PinMode mode)> pins = new List<(int pinNumber, PinValue value, PinMode mode)>();
        public bool ConsoleOut { get; set; }
        public ConsoleGPIOController(bool consoleOut = false) { ConsoleOut = consoleOut; }
        private (int pinNumber, PinValue value, PinMode mode) FindPin(int pinNumber)
        {
            if (!pins.Any(p => p.pinNumber == pinNumber))
                throw new InvalidOperationException($"so sorry, can't do that - pin number {pinNumber} is not open.");

            var match = pins.First(p => p.pinNumber == pinNumber);
            return match;
        }

        void WriteLine(string message)
        {
            if (ConsoleOut)
                Console.WriteLine(message);
        }

        void Write(string message)
        {
            if (ConsoleOut)
                Console.Write(message);
        }

        public void ClosePin(int pinNumber)
        {
            var match = FindPin(pinNumber);

            pins.Remove(match);
            WriteLine($"{nameof(ClosePin)}: {pinNumber}");
        }

        public void Dispose()
        {
            WriteLine(nameof(Dispose));
        }

        public void OpenPin(int pinNumber, PinMode mode)
        {
            if (!pins.Any(p => p.pinNumber == pinNumber))
            {
                pins.Add((pinNumber, PinValue.Low, mode));
                WriteLine($"{nameof(OpenPin)}: {pinNumber}, mode={mode}");
            }
        }

        public PinValue Read(int pinNumber)
        {
            var val = FindPin(pinNumber).value;
            WriteLine($"{nameof(Read)}: {pinNumber}, value={val}");
            return val;

        }

        public void Read(Span<PinValuePair> pinValuePairs)
        {
            WriteLine($"{nameof(Read)}(pinValuePairs):");
            for (int i = 0; i < pinValuePairs.Length; i++)
            {
                var pair = pinValuePairs[i];                
                pinValuePairs[i] = new PinValuePair(pair.PinNumber, Read(pair.PinNumber));
            }
        }

        public void SetPinMode(int pinNumber, PinMode mode)
        {
            var match = FindPin(pinNumber);

            match.mode = mode;
            WriteLine($"{nameof(SetPinMode)}: {pinNumber}, mode={mode}");
        }

        public void Write(int pinNumber, PinValue value)
        {
            var match = FindPin(pinNumber);
            match.value = value;
            WriteLine($"{nameof(Write)}: {pinNumber}, value={value}");
        }

        public void Write(ReadOnlySpan<PinValuePair> pinValuePairs)
        {
            WriteLine($"{nameof(Write)}(pinValuePairs):");
            for (int i = 0; i < pinValuePairs.Length; i++)
            {
                var pair = pinValuePairs[i];
                Write(pair.PinNumber, pair.PinValue);
            }
        }
    }
}
