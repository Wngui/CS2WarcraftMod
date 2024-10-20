using System;
using System.Collections.Generic;
using System.IO;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using Dapper;
using Microsoft.Data.Sqlite;

namespace WarcraftPlugin.Core
{
    public class Database
    {
        private SqliteConnection _connection;

        public void Initialize(string directory)
        {
            _connection =
                new SqliteConnection(
                    $"Data Source={Path.Join(directory, "database.db")}");

            _connection.Execute(@"
                CREATE TABLE IF NOT EXISTS `players` (
	                `steamid` UNSIGNED BIG INT NOT NULL,
	                `currentRace` VARCHAR(32) NOT NULL DEFAULT 'barbarian',
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

        public bool PlayerExistsInDatabase(ulong steamid)
        {
            return _connection.ExecuteScalar<int>("select count(*) from players where steamid = @steamid",
                new { steamid }) > 0;
        }

        public void AddNewPlayerToDatabase(CCSPlayerController player)
        {
            Console.WriteLine($"Adding client to database {player.SteamID}");
            _connection.Execute(@"
            INSERT INTO players (`steamid`, `currentRace`)
	        VALUES(@steamid, 'ranger')",
                new { steamid = player.SteamID });
        }

        public WarcraftPlayer LoadPlayerFromDatabase(CCSPlayerController player, XpSystem xpSystem)
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

        public List<DatabaseClassInformation> LoadClassInformationFromDatabase(CCSPlayerController player)
        {
            var raceInformation = _connection.Query<DatabaseClassInformation>(@"
            SELECT * from `raceinformation` where `steamid` = @steamid",
                new { steamid = player.SteamID });

            return raceInformation.AsList();
        }

        public void SavePlayerToDatabase(CCSPlayerController player)
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

        public void SaveClients()
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

        public void SaveCurrentClass(CCSPlayerController player)
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

    public class DatabasePlayer
    {
        public ulong SteamId { get; set; }
        public string CurrentRace { get; set; }
        public string Name { get; set; }
    }

    public class DatabaseClassInformation
    {
        public ulong SteamId { get; set; }
        public string RaceName { get; set; }
        public int CurrentXp { get; set; }
        public int CurrentLevel { get; set; }
        public int AmountToLevel { get; set; }
        public int Ability1Level { get; set; }
        public int Ability2Level { get; set; }
        public int Ability3Level { get; set; }
        public int Ability4Level { get; set; }
    }
}