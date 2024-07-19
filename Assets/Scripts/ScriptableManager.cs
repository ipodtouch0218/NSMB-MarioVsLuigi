public class ScriptableManager : Singleton<ScriptableManager> {

    // TODO public PrefabList prefabs;
    public PowerupAsset[] powerups;
    public PlayerColorSet[] skins;
    public CharacterData[] characters;
    public Team[] teams;
    public LoopingMusicData[]  alternatingStageMusic;

    public void Awake() {
        Set(this);
    }
}
