using System.ComponentModel;

namespace SysBot.Pokemon
{
    /// <summary>
    /// Console agnostic settings
    /// </summary>
    public abstract class BaseConfig
    {
        protected const string FeatureToggle = nameof(FeatureToggle);
        protected const string Operation = nameof(Operation);
        private const string Debug = nameof(Debug);

        [Category(Debug), Description("Skips creating bots when the program is started; helpful for testing integrations.")]
        public bool SkipConsoleBotCreation { get; set; }

        [Category(Operation)]
        [TypeConverter(typeof(ExpandableObjectConverter))]
        public FolderSettings Folder { get; set; } = new();

        public abstract bool Shuffled { get; }
    }
}
