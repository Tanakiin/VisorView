using System;
using System.IO;
using System.Text.Json;

namespace VisorView
{
    public static class StateStore
    {
        static string FilePath =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                         "VisorView", "state.json");

        static readonly JsonSerializerOptions Opts = new()
        {
            WriteIndented = true
        };

        public static AppState LoadOrDefault()
        {
            try
            {
                if (!File.Exists(FilePath)) return new AppState();
                var json = File.ReadAllText(FilePath);
                var s = JsonSerializer.Deserialize<AppState>(json, Opts);
                return s ?? new AppState();
            }
            catch
            {
                return new AppState();
            }
        }

        public static void Save(AppState state)
        {
            try
            {
                var dir = Path.GetDirectoryName(FilePath);
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir!);

                var json = JsonSerializer.Serialize(state, Opts);
                File.WriteAllText(FilePath, json);
            }
            catch { }
        }

        public static void Clear()
        {
            try
            {
                if (File.Exists(FilePath)) File.Delete(FilePath);
            }
            catch { }
        }
    }
}
