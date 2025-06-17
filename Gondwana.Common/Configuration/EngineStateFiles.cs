using System.Configuration;

namespace Gondwana.Configuration;

/// <summary>
/// List of <see cref="EngineStateFile"/> instances to load when the Engine initializes
/// </summary>
[ConfigurationCollection(typeof(EngineStateFile), AddItemName = "add", RemoveItemName = "remove", ClearItemsName = "clear")]
public class EngineStateFiles : ConfigurationElementCollection, IEnumerable<EngineStateFile>
{
    [ConfigurationProperty("LoadAtStartup", IsRequired = false, DefaultValue = true)]
    public bool LoadAtStartup
    {
        get { return (bool)base["LoadAtStartup"]; }
        set { base["LoadAtStartup"] = value; }
    }

    protected override ConfigurationElement CreateNewElement()
    {
        return new EngineStateFile();
    }

    protected override object GetElementKey(ConfigurationElement element)
    {
        return ((EngineStateFile)element).ID;
    }

    public new IEnumerator<EngineStateFile> GetEnumerator()
    {
        foreach (var id in this.BaseGetAllKeys())
            yield return (EngineStateFile)this.BaseGet(id);
    }

    public new EngineStateFile this[string id]
    {
        get { return (EngineStateFile)BaseGet(id); }
    }

    public void Add(EngineStateFile file)
    {
        BaseAdd(file);
    }

    public void Clear()
    {
        BaseClear();
    }

    public void Remove(EngineStateFile file)
    {
        BaseRemove(file);
    }

    public void RemoveAt(int index)
    {
        BaseRemove(index);
    }
}
