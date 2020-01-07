﻿using System;
using System.Collections.Generic;
using CommandLine;
using RePKG.Command;

namespace RePKG
{
    internal class Program
    {
        public static bool Closing;

        private static void Main(string[] args)
        {
            Console.CancelKeyPress += Cancel;

            if (args.Length > 0 && args[0] == "interactive")
            {
                InteractiveConsole();
                return;
            }

            Parser.Default.ParseArguments<ExtractOptions, InfoOptions>(args)
                .WithParsed<ExtractOptions>(Extract.Action)
                .WithParsed<InfoOptions>(Info.Action)
                .WithNotParsed(NotParsedAction);
        }

        private static void Cancel(object sender, ConsoleCancelEventArgs e)
        {
            Closing = true;
            e.Cancel = true;
            Console.WriteLine("Terminating...");
        }

        private static void InteractiveConsole()
        {
            Console.WriteLine("RePKG started in interactive mode. You can now type commands");
            Console.WriteLine("Type \"help\" for commands");
            
            string line;

            while (!string.IsNullOrEmpty(line = Console.ReadLine()))
            {
                var interactiveArgs = line.SplitArguments();

                Parser.Default.ParseArguments<ExtractOptions, InfoOptions>(interactiveArgs)
                    .WithParsed<ExtractOptions>(Extract.Action)
                    .WithParsed<InfoOptions>(Info.Action)
                    .WithNotParsed(NotParsedAction);
            }
        }


        private static void NotParsedAction(IEnumerable<Error> errors)
        {
            /*foreach (var error in errors)
            {
                if (error.Tag == ErrorType.HelpRequestedError)
                    continue;

                Console.WriteLine(error.Tag);
            }*/
        }
    }
}
