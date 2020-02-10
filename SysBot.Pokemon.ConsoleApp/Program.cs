﻿using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using PKHeX.Core;

namespace SysBot.Pokemon.ConsoleApp
{
    internal static class Program
    {
        private const string PathSurprise = "Surprise";
        private const string PathLinkCode = "LinkCode";
        private const string PathShinyEgg = "ShinyEgg";

        private static async Task Main(string[] args)
        {
            if (args.Length > 1)
                await LaunchViaArgs(args).ConfigureAwait(false);
            else
                await LaunchWithoutArgs().ConfigureAwait(false);

            Console.WriteLine("No bots are currently running. Press any key to exit.");
            Console.ReadKey();
        }

        private static async Task LaunchViaArgs(string[] args)
        {
            Console.WriteLine("Starting up single-bot environment from provided arguments.");
            var BotTypes = typeof(Program).GetFields(BindingFlags.NonPublic | BindingFlags.FlattenHierarchy | BindingFlags.Static)
                .Where(z => z.Name.StartsWith("Path"))
                .Select(z => z.GetRawConstantValue()).ToArray();
            // Launch a single bot.
            var type = args[1];
            var config = args[2];
            var lines = File.ReadAllLines(config);
            var task = GetBotsWithConfigs(Array.IndexOf(BotTypes, type), new[] { lines });
            await task.ConfigureAwait(false);
        }

        private static async Task LaunchWithoutArgs()
        {
            Console.WriteLine("Starting up multi-bot environment.");
            var task0 = GetBotTask(PathSurprise, 0, out var count0);
            var task1 = GetBotTask(PathLinkCode, 1, out var count1);
            var task2 = GetBotTask(PathShinyEgg, 2, out var count2);

            int botCount = count0 + count1 + count2;

            if (botCount == 0)
            {
                Console.WriteLine("No bots started. Verify folder configs.");
                return;
            }

            var tasks = new[] { task0, task1, task2 };
            await Task.WhenAll(tasks).ConfigureAwait(false);
        }

        private static Task GetBotTask(string path, int botType, out int count)
        {
            Directory.CreateDirectory(path);
            var files = Directory.GetFiles(path, "bot*.txt", SearchOption.TopDirectoryOnly);
            count = files.Length;
            if (count == 0)
                return Task.CompletedTask;

            var configs = files.Select(File.ReadAllLines).ToArray();

            Console.WriteLine($"Found {count} config(s) in {path}. Creating bot(s)...");
            return GetBotsWithConfigs(botType, configs);
        }

        private static Task GetBotsWithConfigs(int botType, string[][] configs)
        {
            return botType switch
            {
                2 => DoShinyEggFinder(configs),
                1 => DoTradeHubMulti(PokeRoutineType.LinkTrade, configs),
                0 => DoTradeHubMulti(PokeRoutineType.SurpriseTrade, configs),
                _ => DoTradeHubMulti(PokeRoutineType.Idle, configs),
            };
        }

        private static async Task DoTradeHubMulti(PokeRoutineType initialRoutineType = PokeRoutineType.Idle, params string[][] lines)
        {
            // Default Bot: Code Trade bots. See associated files.
            var token = CancellationToken.None;

            var first = lines[0];
            var hubRandomPath = first[3];
            Console.WriteLine($"Creating a hub for {lines.Length} bot(s) with random distribution from the following path: {hubRandomPath}");
            var hub = new PokeTradeHub<PK8>();

            const string hubcfg = "hub.txt";
            if (File.Exists(hubcfg))
            {
                Console.WriteLine($"{hubcfg} found. Updating hub settings");
                var txt = File.ReadAllLines(hubcfg);
                hub.Config.MinTradeCode = int.Parse(txt[0]);
                hub.Config.MaxTradeCode = int.Parse(txt[1]);
            }

            Task[] threads = new Task[lines.Length + 1]; // hub as last thread
            for (int i = 0; i < lines.Length; i++)
                threads[i] = PokeTradeBotUtil.RunBotAsync(lines[i], hub, initialRoutineType, token);
            threads[threads.Length - 1] = hub.MonitorTradeQueueAddIfEmpty(hubRandomPath, token);

            await Task.WhenAll(threads).ConfigureAwait(false);
        }

        private static async Task DoShinyEggFinder(params string[][] lines)
        {
            // Shiny Egg receiver bots. See associated files.
            var token = CancellationToken.None;
            Console.WriteLine($"Creating {lines.Length} bot(s) for Finding Eggs.");

            Task[] threads = new Task[lines.Length];
            for (int i = 0; i < lines.Length; i++)
                threads[i] = EggBotUtil.RunBotAsync(lines[i], token);

            await Task.WhenAll(threads).ConfigureAwait(false);
        }
    }
}