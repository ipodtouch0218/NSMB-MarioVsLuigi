public class ScriptableManager : Singleton<ScriptableManager> {

    // TODO public PrefabList prefabs;
    public PlayerColorSet[] skins;

    public void Awake() {
        Set(this);
    }
}
