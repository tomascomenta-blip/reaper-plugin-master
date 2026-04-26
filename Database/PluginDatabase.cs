// =============================================================================
// Database/PluginDatabase.cs
// =============================================================================
using System;
using System.Collections.Generic;
using System.IO;
using LiteDB;
using ReaperPluginManager.Models;

namespace ReaperPluginManager.Database
{
    public interface IPluginDatabase : IDisposable
    {
        void   UpsertPlugin(Plugin plugin);
        Plugin? GetPlugin(Guid id);
        IEnumerable<Plugin> GetAllPlugins();
        void   DeletePlugin(Guid id);

        void   UpsertCategory(PluginCategory category);
        IEnumerable<PluginCategory> GetAllCategories();
        void   DeleteCategory(string name);
    }

    public class PluginDatabase : IPluginDatabase
    {
        private readonly LiteDatabase _db;
        private readonly ILiteCollection<Plugin>         _plugins;
        private readonly ILiteCollection<PluginCategory> _categories;

        public PluginDatabase()
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var dir     = Path.Combine(appData, "ReaperPluginManager");
            Directory.CreateDirectory(dir);

            var dbPath = Path.Combine(dir, "plugins.db");
            _db         = new LiteDatabase(dbPath);
            _plugins    = _db.GetCollection<Plugin>("plugins");
            _categories = _db.GetCollection<PluginCategory>("categories");

            // Índices para búsqueda rápida
            _plugins.EnsureIndex(p => p.Name);
            _plugins.EnsureIndex(p => p.Status);
            _plugins.EnsureIndex(p => p.Format);
            _plugins.EnsureIndex(p => p.Category);

            SeedDefaultCategories();
        }

        public void UpsertPlugin(Plugin plugin)             => _plugins.Upsert(plugin);
        public Plugin? GetPlugin(Guid id)                  => _plugins.FindById(id);
        public IEnumerable<Plugin> GetAllPlugins()         => _plugins.FindAll();
        public void DeletePlugin(Guid id)                  => _plugins.Delete(id);

        public void UpsertCategory(PluginCategory cat)              => _categories.Upsert(cat);
        public IEnumerable<PluginCategory> GetAllCategories()       => _categories.FindAll();
        public void DeleteCategory(string name)                     => _categories.Delete(name);

        public void Dispose() => _db.Dispose();

        private void SeedDefaultCategories()
        {
            if (_categories.Count() > 0) return;

            var defaults = new[]
            {
                new PluginCategory { Name = "Sintetizadores",  Color = "#7C4DFF" },
                new PluginCategory { Name = "Efectos",         Color = "#00BCD4" },
                new PluginCategory { Name = "Instrumentos",    Color = "#4CAF50" },
                new PluginCategory { Name = "Mezcla",          Color = "#FF9800" },
                new PluginCategory { Name = "Masterización",   Color = "#F44336" },
                new PluginCategory { Name = "Utilidades",      Color = "#9E9E9E" },
            };

            foreach (var cat in defaults)
                _categories.Upsert(cat);
        }
    }
}
