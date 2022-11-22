public class ScriptableManager : Singleton<ScriptableManager> {

    public PrefabList       prefabs;
    public Powerup[]        powerups;
    public PlayerColorSet[] skins;
    public CharacterData[]  characters;
    public Team[]           teams;

    public void Awake() {
        if (!InstanceCheck())
            return;

        Instance = this;
    }
}
