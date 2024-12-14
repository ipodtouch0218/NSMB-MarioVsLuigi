public class ScriptableManager : Singleton<ScriptableManager> {

    // TODO public PrefabList prefabs;
    public PaletteSet[] skins;

    public void Awake() {
        Set(this);
    }
}
