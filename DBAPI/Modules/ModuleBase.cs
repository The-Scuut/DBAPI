namespace DBAPI.Modules;

public abstract class ModuleBase
{
    public virtual ModuleInfo Info { get; }

    public virtual void Enable() { }
    public virtual void Disable() { }
}