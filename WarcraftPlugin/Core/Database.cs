using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
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

            var maxAbilities = WarcraftPlugin.Instance.classManager.GetAllClasses().Max(c => c.Abilities.Count);

            var abilityColumns = new List<string>();
            for (int i = 1; i <= maxAbilities; i++)
            {
                abilityColumns.Add($"`ability{i}level` TINYINT NULL DEFAULT 0");
            }

            _connection.Execute($@"
                CREATE TABLE IF NOT EXISTS `raceinformation` (
                  `steamid` UNSIGNED BIG INT NOT NULL,
                  `racename` VARCHAR(32) NOT NULL,
                  `currentXP` INT NULL DEFAULT 0,
                  `currentLevel` INT NULL DEFAULT 1,
                  `amountToLevel` INT NULL DEFAULT 100,
                  {string.Join(",\n                  ", abilityColumns)},
                  PRIMARY KEY (`steamid`, `racename`));
                ");

            var existingCols = _connection.Query<string>("SELECT name FROM pragma_table_info('raceinformation')").AsList();
            for (int i = 1; i <= maxAbilities; i++)
            {
                var colName = $"ability{i}level";
                if (!existingCols.Contains(colName))
                {
                    _connection.Execute($"ALTER TABLE `raceinformation` ADD COLUMN `{colName}` TINYINT NULL DEFAULT 0;");
                }
            }
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
                player.PrintToChat(" "+ WarcraftPlugin.Instance.Localizer["class.disabled", defaultClass.LocalizedDisplayName]);
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

            var raceRow = _connection.QueryFirst<dynamic>(@"
            SELECT * from `raceinformation` where `steamid` = @steamid AND `racename` = @racename",
                new { steamid = player.SteamID, racename = dbPlayer.CurrentRace });

            var raceDict = (IDictionary<string, object>)raceRow;
            var raceInformation = new ClassInformation
            {
                SteamId = Convert.ToUInt64(raceDict["steamid"]),
                RaceName = (string)raceDict["racename"],
                CurrentXp = Convert.ToInt32(raceDict["currentXP"]),
                CurrentLevel = Convert.ToInt32(raceDict["currentLevel"]),
                AmountToLevel = Convert.ToInt32(raceDict["amountToLevel"])
            };

            for (int i = 1; ; i++)
            {
                var col = $"ability{i}level";
                if (!raceDict.ContainsKey(col))
                    break;
                raceInformation.AbilityLevels.Add(Convert.ToInt32(raceDict[col]));
            }

            var wcPlayer = new WarcraftPlayer(player);
            wcPlayer.LoadClassInformation(raceInformation, xpSystem);
            WarcraftPlugin.Instance.SetWcPlayer(player, wcPlayer);

            return wcPlayer;
        }

        internal List<ClassInformation> LoadClassInformationFromDatabase(CCSPlayerController player)
        {
            var rows = _connection.Query(@"
            SELECT * from `raceinformation` where `steamid` = @steamid",
                new { steamid = player.SteamID });

            var list = new List<ClassInformation>();
            foreach (IDictionary<string, object> row in rows)
            {
                var info = new ClassInformation
                {
                    SteamId = Convert.ToUInt64(row["steamid"]),
                    RaceName = (string)row["racename"],
                    CurrentXp = Convert.ToInt32(row["currentXP"]),
                    CurrentLevel = Convert.ToInt32(row["currentLevel"]),
                    AmountToLevel = Convert.ToInt32(row["amountToLevel"])
                };

                for (int i = 1; ; i++)
                {
                    var col = $"ability{i}level";
                    if (!row.ContainsKey(col))
                        break;
                    info.AbilityLevels.Add(Convert.ToInt32(row[col]));
                }

                list.Add(info);
            }

            return list;
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

            var abilityCount = wcPlayer.GetClass().Abilities.Count;
            var abilitySet = new List<string>();
            var parameters = new DynamicParameters();
            parameters.Add("currentXp", wcPlayer.currentXp);
            parameters.Add("currentLevel", wcPlayer.currentLevel);
            parameters.Add("amountToLevel", wcPlayer.amountToLevel);
            parameters.Add("steamid", player.SteamID);
            parameters.Add("racename", wcPlayer.className);

            for (int i = 0; i < abilityCount; i++)
            {
                var column = $"ability{i + 1}level";
                abilitySet.Add($"`{column}` = @a{i}");
                parameters.Add($"a{i}", wcPlayer.GetAbilityLevel(i));
            }

            var sql = $@"UPDATE `raceinformation` SET `currentXP` = @currentXp,
                 `currentLevel` = @currentLevel,
                 {string.Join(",\n                 ", abilitySet)},
                 `amountToLevel` = @amountToLevel WHERE `steamid` = @steamid AND `racename` = @racename;";

            _connection.Execute(sql, parameters);
        }

        internal void SaveClients()
        {
            foreach (var player in Utilities.GetPlayers())
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

        internal void ResetClients()
        {
            _connection.Execute(@"
                DELETE FROM `players`;");

            _connection.Execute(@"
                DELETE FROM `raceinformation`;");
        }
    }

    internal class DatabasePlayer
    {
        // Dapper returns integer values from SQLite as long (Int64) which
        // cannot be automatically cast to ulong. Using a signed integer here
        // avoids InvalidCastException when mapping query results.
        internal long SteamId { get; set; }
        internal string CurrentRace { get; set; }
        internal string Name { get; set; }
    }

    internal class ClassInformation
    {
        internal ulong SteamId { get; set; }
        internal string RaceName { get; set; }
        internal int CurrentXp { get; set; }
        internal int CurrentLevel { get; set; }
        internal int AmountToLevel { get; set; }
        internal List<int> AbilityLevels { get; set; } = [];
    }
}