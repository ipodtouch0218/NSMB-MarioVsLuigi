public class PiranhaSpawnpoint : EnemySpawnpoint {

    private PiranhaPlantController plant;

    public void Awake() {
        plant = GetComponent<PiranhaPlantController>();
    }

    public override bool AttemptSpawning() {
        plant.Respawn();
        return true;
    }
}
