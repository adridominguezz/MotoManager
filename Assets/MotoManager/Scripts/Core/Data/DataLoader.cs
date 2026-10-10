using System;
using System.Collections.Generic;
using MotoManager.Core.World;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;

namespace MotoManager.Core.Data
{
    public struct DataFile
    {
        public string Name;
        public string Json;

        public DataFile(string name, string json)
        {
            Name = name;
            Json = json;
        }
    }

    /// <summary>Where JSON comes from. Unity uses Resources/TextAssets, tests and tools use the file system.</summary>
    public interface IDataSource
    {
        IEnumerable<DataFile> LoadFolder(string folder);
    }

    public static class JsonSettings
    {
        private static JsonSerializerSettings _settings;

        public static JsonSerializerSettings Settings
        {
            get
            {
                if (_settings == null)
                {
                    _settings = new JsonSerializerSettings
                    {
                        NullValueHandling = NullValueHandling.Ignore,
                        MissingMemberHandling = MissingMemberHandling.Ignore,
                        ObjectCreationHandling = ObjectCreationHandling.Replace,
                        DateFormatString = "yyyy-MM-dd",
                        DateTimeZoneHandling = DateTimeZoneHandling.Unspecified,
                        Formatting = Formatting.None
                    };
                    _settings.Converters.Add(new StringEnumConverter());
                }
                return _settings;
            }
        }

        public static JsonSerializer CreateSerializer()
        {
            return JsonSerializer.Create(Settings);
        }

        public static string Serialize(object o, bool indented = false)
        {
            return JsonConvert.SerializeObject(o, indented ? Formatting.Indented : Formatting.None, Settings);
        }

        public static T Deserialize<T>(string json)
        {
            return JsonConvert.DeserializeObject<T>(json, Settings);
        }

        public static T DeepClone<T>(T o)
        {
            return Deserialize<T>(Serialize(o));
        }
    }

    /// <summary>
    /// Loads every data folder. Each file may contain a single object or an array, so content can be split in
    /// as many files as convenient (one file per team, one per circuit, ...) without touching code.
    /// </summary>
    public static class DataLoader
    {
        public const string Championships = "championships";
        public const string Categories = "categories";
        public const string Rules = "rules";
        public const string BikeClasses = "bikeclasses";
        public const string Circuits = "circuits";
        public const string Calendar = "calendar";
        public const string Manufacturers = "manufacturers";
        public const string Motorcycles = "motorcycles";
        public const string Teams = "teams";
        public const string Riders = "riders";
        public const string Contracts = "contracts";
        public const string Staff = "staff";
        public const string Sponsors = "sponsors";
        public const string Names = "names";
        public const string Development = "development";
        public const string Simulation = "simulation";
        public const string Organisations = "organisations";
        public const string SponsorNames = "sponsornames";
        public const string Merchandise = "merchandise";
        public const string Managers = "managers";

        public static readonly string[] AllFolders =
        {
            Championships, Categories, Rules, BikeClasses, Circuits, Calendar, Manufacturers, Motorcycles, Teams,
            Riders, Contracts, Staff, Sponsors, Names, Development, Simulation, Organisations, SponsorNames, Merchandise, Managers
        };

        public static GameDatabase Load(IDataSource source)
        {
            var db = new GameDatabase();
            var serializer = JsonSettings.CreateSerializer();

            LoadInto(source, Championships, serializer, (ChampionshipDefinition x) => db.Championships[x.Id] = x);
            LoadInto(source, Categories, serializer, (ChampionshipCategory x) => db.Categories[x.Id] = x);
            LoadInto(source, Rules, serializer, (CategoryRules x) => db.Rules[x.Id] = x);
            LoadInto(source, BikeClasses, serializer, (BikeClassDefinition x) => db.BikeClasses[x.Id] = x);
            LoadInto(source, Circuits, serializer, (CircuitDefinition x) => db.Circuits[x.Id] = x);
            LoadInto(source, Calendar, serializer, (CalendarDefinition x) => db.Calendars[x.Id] = x);
            LoadInto(source, Manufacturers, serializer, (Manufacturer x) => db.Manufacturers[x.Id] = x);
            LoadInto(source, Motorcycles, serializer, (Motorcycle x) => db.Motorcycles[x.Id] = x);
            LoadInto(source, Teams, serializer, (Team x) => db.Teams[x.Id] = x);
            LoadInto(source, Riders, serializer, (Rider x) => db.Riders[x.Id] = x);
            LoadInto(source, Contracts, serializer, (ContractDefinition x) => db.Contracts.Add(x));
            LoadInto(source, Staff, serializer, (StaffTemplate x) => db.Staff[x.Id] = x);
            LoadInto(source, Sponsors, serializer, (SponsorTemplate x) => db.Sponsors[x.Id] = x);
            LoadInto(source, Names, serializer, (NamePool x) => db.NamePools[x.Nationality] = x);
            LoadInto(source, Development, serializer, (DevelopmentTemplate x) => db.DevelopmentTemplates[x.Id] = x);
            LoadInto(source, Simulation, serializer, (SimulationTuning x) => db.Tuning = x);
            LoadInto(source, Organisations, serializer, (Organisation x) => db.Organisations[x.Id] = x);
            LoadInto(source, SponsorNames, serializer, (SponsorNameParts x) => db.SponsorNames = x);
            LoadInto(source, Merchandise, serializer, (ProductTemplate x) => db.Products[x.Id] = x);
            LoadInto(source, Managers, serializer, (ManagerBackground x) => db.ManagerBackgrounds[x.Id] = x);
            return db;
        }

        private static void LoadInto<T>(IDataSource source, string folder, JsonSerializer serializer, Action<T> add)
        {
            foreach (var file in source.LoadFolder(folder))
            {
                JToken token;
                try
                {
                    token = JToken.Parse(file.Json);
                }
                catch (Exception e)
                {
                    throw new FormatException("Invalid JSON in " + folder + "/" + file.Name + ": " + e.Message, e);
                }
                try
                {
                    if (token.Type == JTokenType.Array)
                    {
                        foreach (var item in (JArray)token) add(item.ToObject<T>(serializer));
                    }
                    else
                    {
                        add(token.ToObject<T>(serializer));
                    }
                }
                catch (Exception e)
                {
                    throw new FormatException("Cannot read " + folder + "/" + file.Name + " as " + typeof(T).Name + ": " + e.Message, e);
                }
            }
        }
    }
}
