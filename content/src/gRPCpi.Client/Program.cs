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

        static System.Device.Gpio.GpioController GetController()
        {
            if (RuntimeInformation.OSArchitecture == Architecture.Arm) //Assume we're on the pi
                return new GpioController(PinNumberingScheme.Board);
            else
                return new GpioController(PinNumberingScheme.Board, new ConsoleGPIODriver());
        }

        /// <summary>
        /// Pulses the light a number of times corresponding to the character's alphabet index
        /// </summary>
        /// <param name="reply"></param>
        private static void PulseReply(HelloReply reply)
        {
            using (GpioController controller = GetController())
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

    public class ConsoleGPIODriver : GpioDriver
    {

        List<ConsolePin> pins = new List<ConsolePin>();
        public bool ConsoleOut { get; set; }

        protected override int PinCount => 32;

        public ConsoleGPIODriver(bool consoleOut = false) { ConsoleOut = consoleOut; }
        private ConsolePin FindPin(int pinNumber)
        {
            if (!pins.Any(p => p.PinNumber == pinNumber))
                throw new InvalidOperationException($"so sorry, can't do that - pin number {pinNumber} is not open.");

            var match = pins.First(p => p.PinNumber == pinNumber);
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

        protected override void ClosePin(int pinNumber)
        {
            var match = FindPin(pinNumber);

            pins.Remove(match);
            WriteLine($"{nameof(ClosePin)}: {pinNumber}");
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            WriteLine(nameof(Dispose));
        }

        protected override PinValue Read(int pinNumber)
        {
            var val = FindPin(pinNumber).Value;
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

        protected override void SetPinMode(int pinNumber, PinMode mode)
        {
            var match = FindPin(pinNumber);

            match.Mode = mode;
            WriteLine($"{nameof(SetPinMode)}: {pinNumber}, mode={mode}");
        }

        protected override void Write(int pinNumber, PinValue value)
        {
            var match = FindPin(pinNumber);
            match.Value = value;
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

        Dictionary<int, List<PinChangeEventHandler>> PinNone = new Dictionary<int, List<PinChangeEventHandler>>();
        Dictionary<int, List<PinChangeEventHandler>> PinRising = new Dictionary<int, List<PinChangeEventHandler>>();
        Dictionary<int, List<PinChangeEventHandler>> PinFalling = new Dictionary<int, List<PinChangeEventHandler>>();

        protected override void AddCallbackForPinValueChangedEvent(int pinNumber, PinEventTypes eventTypes, PinChangeEventHandler callback)
        {
            if ((eventTypes | PinEventTypes.None) == PinEventTypes.None)
            {
                if (!PinNone.ContainsKey(pinNumber))
                    PinNone[pinNumber] = new List<PinChangeEventHandler>();

                PinNone[pinNumber].Add(callback);
            }

            if ((eventTypes | PinEventTypes.Rising) == PinEventTypes.Rising)
            {
                if (!PinRising.ContainsKey(pinNumber))
                    PinRising[pinNumber] = new List<PinChangeEventHandler>();

                PinRising[pinNumber].Add(callback);
            }

            if ((eventTypes | PinEventTypes.Falling) == PinEventTypes.Falling)
            {
                if (!PinFalling.ContainsKey(pinNumber))
                    PinFalling[pinNumber] = new List<PinChangeEventHandler>();

                PinFalling[pinNumber].Add(callback);
            }
        }

        protected override int ConvertPinNumberToLogicalNumberingScheme(int pinNumber)
        {
            return pinNumber;
        }

        protected override PinMode GetPinMode(int pinNumber)
        {
            return pins.FirstOrDefault(p => p.PinNumber == pinNumber).Mode ?? throw new Exception($"Pin modenot set for pin {pinNumber}");
        }

        protected override bool IsPinModeSupported(int pinNumber, PinMode mode)
        {
            return true;
        }

        protected override void OpenPin(int pinNumber)
        {
            if (!pins.Any(p => p.PinNumber == pinNumber))
            {
                pins.Add(new ConsolePin 
                { 
                    PinNumber = pinNumber,
                    Value = PinValue.Low
                });
                WriteLine($"{nameof(OpenPin)}: {pinNumber}");
            }
        }

        protected override void RemoveCallbackForPinValueChangedEvent(int pinNumber, PinChangeEventHandler callback)
        {
            if (PinNone.ContainsKey(pinNumber))
            {
                PinNone[pinNumber].Remove(callback);
            }

            if (PinRising.ContainsKey(pinNumber))
            {
                PinRising[pinNumber].Remove(callback);
            }

            if (PinFalling.ContainsKey(pinNumber))
            {
                PinFalling[pinNumber].Remove(callback);
            }
        }

        protected override WaitForEventResult WaitForEvent(int pinNumber, PinEventTypes eventTypes, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }
    }

    internal class ConsolePin
    {
        public int PinNumber { get; set; }
        public PinMode? Mode { get; set; }
        public PinValue Value { get; set; }        
    }
}
