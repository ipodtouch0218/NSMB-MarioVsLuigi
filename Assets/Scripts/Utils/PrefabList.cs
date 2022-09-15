using UnityEngine;

public static class PrefabList {

    public static FireballMover Fireball { get; } =
        ((GameObject) Resources.Load("Prefab/Fireball")).GetComponent<FireballMover>();
    public static FireballMover Iceball { get; } =
        ((GameObject) Resources.Load("Prefab/Iceball")).GetComponent<FireballMover>();
    public static LooseCoin LooseCoin { get; } =
        ((GameObject) Resources.Load("Prefabs/LooseCoin")).GetComponent<LooseCoin>();
    public static StarBouncer BigStar { get; } =
        ((GameObject) Resources.Load("Prefabs/BigStar")).GetComponent<StarBouncer>();


    public static MovingPowerup Powerup_Star { get; } =
        ((GameObject) Resources.Load("Prefabs/Powerups/Star")).GetComponent<MovingPowerup>();
    public static MovingPowerup Powerup_1Up { get; } =
        ((GameObject) Resources.Load("Prefabs/Powerups/1-Up")).GetComponent<MovingPowerup>();

}