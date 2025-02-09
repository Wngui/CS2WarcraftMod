﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Utils;
using Dapper;
using Microsoft.Data.Sqlite;
using WarcraftPlugin.Models;

namespace WarcraftPlugin.Core
{
    internal class Database
    {
        private SqliteConnection _connection;

        internal void Initialize(string directory)
        {
            _connection =
                new SqliteConnection(
                    $"Data Source={Path.Join(directory, "database.db")}");

            _connection.Execute(@"
                CREATE TABLE IF NOT EXISTS `players` (
	                `steamid` UNSIGNED BIG INT NOT NULL,
	                `currentRace` VARCHAR(32) NOT NULL,
                  `name` VARCHAR(64),
	                PRIMARY KEY (`steamid`));");

            _connection.Execute(@"
                CREATE TABLE IF NOT EXISTS `raceinformation` (
                  `steamid` UNSIGNED BIG INT NOT NULL,
                  `racename` VARCHAR(32) NOT NULL,
                  `currentXP` INT NULL DEFAULT 0,
                  `currentLevel` INT NULL DEFAULT 1,
                  `amountToLevel` INT NULL DEFAULT 100,
                  `ability1level` TINYINT NULL DEFAULT 0,
                  `ability2level` TINYINT NULL DEFAULT 0,
                  `ability3level` TINYINT NULL DEFAULT 0,
                  `ability4level` TINYINT NULL DEFAULT 0,
                  PRIMARY KEY (`steamid`, `racename`));
                ");
        }

        internal bool PlayerExistsInDatabase(ulong steamid)
        {
            return _connection.ExecuteScalar<int>("select count(*) from players where steamid = @steamid",
                new { steamid }) > 0;
        }

        internal void AddNewPlayerToDatabase(CCSPlayerController player)
        {
            var defaultClass = WarcraftPlugin.Instance.classManager.GetDefaultClass();
            Console.WriteLine($"Adding client to database {player.SteamID}");
            _connection.Execute(@"
            INSERT INTO players (`steamid`, `currentRace`)
            VALUES(@steamid, @className)",
                new { steamid = player.SteamID, className = defaultClass.InternalName });
        }

        internal WarcraftPlayer LoadPlayerFromDatabase(CCSPlayerController player, XpSystem xpSystem)
        {
            var dbPlayer = _connection.QueryFirstOrDefault<DatabasePlayer>(@"
            SELECT * FROM `players` WHERE `steamid` = @steamid",
                new { steamid = player.SteamID });

            if (dbPlayer == null)
            {
                AddNewPlayerToDatabase(player);
                dbPlayer = _connection.QueryFirstOrDefault<DatabasePlayer>(@"
                    SELECT * FROM `players` WHERE `steamid` = @steamid",
                    new { steamid = player.SteamID });
            }

            // If the class no longer exists, set it to the default class
            if (!WarcraftPlugin.Instance.classManager.GetAllClasses().Any(x => x.InternalName == dbPlayer.CurrentRace))
            {
                var defaultClass = WarcraftPlugin.Instance.classManager.GetDefaultClass();
                dbPlayer.CurrentRace = defaultClass.InternalName;
                player.PrintToChat($"{ChatColors.Green}Current class is{ChatColors.Red} disabled{ChatColors.Green}! Now playing as {defaultClass.DisplayName}.{ChatColors.Default}");
            }

            var raceInformationExists = _connection.ExecuteScalar<int>(@"
            select count(*) from `raceinformation` where steamid = @steamid AND racename = @racename",
                new { steamid = player.SteamID, racename = dbPlayer.CurrentRace }
            ) > 0;

            if (!raceInformationExists)
            {
                _connection.Execute(@"
                insert into `raceinformation` (steamid, racename)
                values (@steamid, @racename);",
                    new { steamid = player.SteamID, racename = dbPlayer.CurrentRace });
            }

            var raceInformation = _connection.QueryFirst<DatabaseClassInformation>(@"
            SELECT * from `raceinformation` where `steamid` = @steamid AND `racename` = @racename",
                new { steamid = player.SteamID, racename = dbPlayer.CurrentRace });

            var wcPlayer = new WarcraftPlayer(player);
            wcPlayer.LoadFromDatabase(raceInformation, xpSystem);
            WarcraftPlugin.Instance.SetWcPlayer(player, wcPlayer);

            return wcPlayer;
        }

        internal List<DatabaseClassInformation> LoadClassInformationFromDatabase(CCSPlayerController player)
        {
            var raceInformation = _connection.Query<DatabaseClassInformation>(@"
            SELECT * from `raceinformation` where `steamid` = @steamid",
                new { steamid = player.SteamID });

            return raceInformation.AsList();
        }

        internal void SavePlayerToDatabase(CCSPlayerController player)
        {
            var wcPlayer = WarcraftPlugin.Instance.GetWcPlayer(player);
            Server.PrintToConsole($"Saving {player.PlayerName} to database...");

            var raceInformationExists = _connection.ExecuteScalar<int>(@"
            select count(*) from `raceinformation` where steamid = @steamid AND racename = @racename",
                new { steamid = player.SteamID, racename = wcPlayer.className }
            ) > 0;

            if (!raceInformationExists)
            {
                _connection.Execute(@"
                insert into `raceinformation` (steamid, racename)
                values (@steamid, @racename);",
                    new { steamid = player.SteamID, racename = wcPlayer.className });
            }

            _connection.Execute(@"
                UPDATE `raceinformation` SET `currentXP` = @currentXp,
                 `currentLevel` = @currentLevel,
                 `ability1level` = @ability1Level,
                 `ability2level` = @ability2Level,
                 `ability3level` = @ability3Level,
                 `ability4level` = @ability4Level,
                 `amountToLevel` = @amountToLevel WHERE `steamid` = @steamid AND `racename` = @racename;",
                new
                {
                    wcPlayer.currentXp,
                    wcPlayer.currentLevel,
                    ability1Level = wcPlayer.GetAbilityLevel(0),
                    ability2Level = wcPlayer.GetAbilityLevel(1),
                    ability3Level = wcPlayer.GetAbilityLevel(2),
                    ability4Level = wcPlayer.GetAbilityLevel(3),
                    wcPlayer.amountToLevel,
                    steamid = player.SteamID,
                    racename = wcPlayer.className
                });
        }

        internal void SaveClients()
        {
            var playerEntities = Utilities.FindAllEntitiesByDesignerName<CCSPlayerController>("cs_player_controller");
            foreach (var player in playerEntities)
            {
                if (!player.IsValid) continue;

                var wcPlayer = WarcraftPlugin.Instance.GetWcPlayer(player);
                if (wcPlayer == null) continue;

                SavePlayerToDatabase(player);
            }
        }

        internal void SaveCurrentClass(CCSPlayerController player)
        {
            var wcPlayer = WarcraftPlugin.Instance.GetWcPlayer(player);

            _connection.Execute(@"
            UPDATE `players` SET `currentRace` = @currentRace, `name` = @name WHERE `steamid` = @steamid;",
                new
                {
                    currentRace = wcPlayer.className,
                    name = player.PlayerName,
                    steamid = player.SteamID
                });
        }
    }

    internal class DatabasePlayer
    {
        internal ulong SteamId { get; set; }
        internal string CurrentRace { get; set; }
        internal string Name { get; set; }
    }

    internal class DatabaseClassInformation
    {
        internal ulong SteamId { get; set; }
        internal string RaceName { get; set; }
        internal int CurrentXp { get; set; }
        internal int CurrentLevel { get; set; }
        internal int AmountToLevel { get; set; }
        internal int Ability1Level { get; set; }
        internal int Ability2Level { get; set; }
        internal int Ability3Level { get; set; }
        internal int Ability4Level { get; set; }
    }
}