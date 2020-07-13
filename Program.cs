using System;
using System.IO;
using System.Collections.Generic;
using System.IO.Ports;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Text;
using SAE.J2534;
using System.Reflection;
using System.Runtime.Remoting.Messaging;
using System.Linq.Expressions;

/*  This is used to brute force the key for E38 ECU's...  
 *  using dll from https://github.com/BrianHumlicek/J2534-Sharp
 *  Simple to use, start, select seed to start with and connect to the MDI..
 *  
 * It does tell you when it finds the key that unlocks the ecu and also writes it to a file
 * todo: should I make it write every key it tries as a log?? that's a big log..
 * 
 * 
 */

namespace E38_brute_force
{
    class Program
    {
        public static Boolean unlocked = false;
       // public static int seed;
        public static String keystring;
        public static int key;
        public static String Vin;
        static String DllFileName = APIFactory.GetAPIinfo().First().Filename;
        public static String message;

        static void Main(string[] args)
        {
            
            Console.WriteLine("E38 unlocker ");
            Console.WriteLine("Enter key to start with. 0x0000 for full range ");
            keystring = Console.ReadLine();
            if (keystring == "") keystring = "0000";
            key = Convert.ToInt32(keystring,16);
            if (key < 0x0000 || key>0xffff)
            {
                Console.WriteLine("please enter a key between 0x0000 and 0xffff");
                Main(args);
            }
            Console.WriteLine($"Starting brute force with {key:X4}");  //Console.WriteLine($"{i:X4}");
            Channel Channel = APIFactory.GetAPI(DllFileName).GetDevice().GetChannel(Protocol.ISO15765, Baud.ISO15765, ConnectFlag.CAN_29BIT_ID);
            Channel.StartMsgFilter(new MessageFilter(UserFilterType.STANDARDISO15765, new byte[] { 0x00, 0x00, 0x07, 0xE0 }));
            System.Threading.Thread.Sleep(50);
            Channel.SendMessage(new byte[] { 0x00, 0x00, 0x07, 0xE0, 0x09, 0x02 });
            GetMessageResults Response = Channel.GetMessages(3,500);
            Vin = HexStringToString(BitConverter.ToString(Response.Messages[2].Data).Replace("-", "").Substring(14, 34));
            Console.WriteLine($"Vin is {Vin}");
            if (Vin.Length != 17)
            {
                Console.WriteLine("Vin is not correct, problem.. exiting");
                Main(args);
            }
            Console.WriteLine("please make note of last key tried if you need to stop, then start with last key tried to continue");
            for (int trykey = key; trykey < 0x10000; trykey++)
            {
                Channel.SendMessage(new byte[] { 0x00, 0x00, 0x07, 0xE0, 0x27, 0x01 });
                GetMessageResults Seedrequest = Channel.GetMessages(2);
                message = (BitConverter.ToString(Seedrequest.Messages[1].Data).Replace("-", ""));
                Console.WriteLine($"Response from Seed request is {message}");
                if (message.Contains("6701"))
                {
                   
                    Console.WriteLine("Seed request response OK");   // key = 0x1234
                   
                    keystring = trykey.ToString("X4");
                    byte key1 = Convert.ToByte(keystring.Substring(0, 2), 16);
                    byte key2 = Convert.ToByte(keystring.Substring(2, 2), 16);
                   
                    Channel.SendMessage(new byte[] { 0x00, 0x00, 0x07, 0xE0, 0x27, 0x02, key1, key2 });
                    GetMessageResults Unlocked = Channel.GetMessages(2);
                    message = (BitConverter.ToString(Unlocked.Messages[1].Data).Replace("-", ""));
                    if (!message.Contains("670"))
                    {
                        Console.WriteLine($"Error!! Unlock failed using {key1:X2} {key2:X2} for key, Response was {message}");
                        System.Threading.Thread.Sleep(9980);
                    }
                    else
                    {
                        Console.WriteLine("!!!!! Unlocked- OK");
                        Console.WriteLine($"Key used to unlock was  0x{trykey:X4}");
                        File.WriteAllText(AppDomain.CurrentDomain.BaseDirectory + @"\" + "key.txt", $"Key used to unlock was  0x{trykey:X4}");
                        Console.WriteLine("Press any key to close, make sure you note the key used!!! ");
                        Console.WriteLine("Key is also saved as key.txt");
                        Console.ReadKey();
                        Environment.Exit(0);

                    }

                }

                else
                {
                    Console.WriteLine($"Error!! Response from Seed request is {message}");
                    System.Threading.Thread.Sleep(9980);
                    trykey--; // try the same key again
                              // do we quit or keep on going?? 
                }

            }

            APIFactory.StaticDispose();
            // Channel.ClearMsgFilters();

            Console.WriteLine("Press any key to end ");
            Console.ReadKey();
        }

        static string HexStringToString(string HexString)
        {
            string stringValue = "";
            for (int i = 0; i < HexString.Length / 2; i++)
            {
                string hexChar = HexString.Substring(i * 2, 2);
                int hexValue = Convert.ToInt32(hexChar, 16);
                stringValue += Char.ConvertFromUtf32(hexValue);
            }
            return stringValue;
        }


    }
    
        
    
}
